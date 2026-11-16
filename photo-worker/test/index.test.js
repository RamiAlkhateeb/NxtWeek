// Run with: node --test photo-worker/test/*.test.js   (Node 20+, no packages)
import { test } from "node:test";
import assert from "node:assert/strict";
import worker, { englishDish, ingredientWords, prompt, isAllowedOrigin, isUserKey, mondayOf, addDays } from "../src/index.js";

const DB = "https://db.test";
const catalog = {
  en_n12: { name: "Beef kofta with bulgur salad", language: "en", ingredientLines: [{ id: "mincedBeef" }, { id: "bulgur" }] },
  n12: { name: "كفتة مشوية مع سلطة برغل", language: "ar", ingredientLines: [{ id: "mincedBeef" }] },
  m3: { name: "ملوخية", language: "ar", ingredients: ["ملوخية", "دجاج"] },
  old: { name: "Gone", language: "en", isArchived: true },
  done: { name: "Soup", language: "en" }
};
const photos = { done: "data:image/jpeg;base64,xx" };
const users = { rami: { username: "rami" }, guest_1: {} };

function setup(quota = {}) {
  const calls = [];
  const writes = [];
  globalThis.fetch = async (url, init) => {
    const u = decodeURIComponent(String(url));
    if (init?.method === "PUT") { writes.push({ u, body: JSON.parse(init.body) }); quota[u] = JSON.parse(init.body); return new Response("null"); }
    let value = null;
    const m = u.match(/\/(MealCatalog|MealPhotos|users)\/([^/.]+)\.json/);
    if (m) value = ({ MealCatalog: catalog, MealPhotos: photos, users })[m[1]][m[2]] ?? null;
    if (m && m[1] !== "MealCatalog" && value) value = true; // shallow reads
    if (u.includes("/PhotoQuota/")) value = quota[u] ?? null;
    return new Response(JSON.stringify(value), { status: 200 });
  };
  const env = {
    FIREBASE_URL: DB,
    AI: { run: async (model, input) => { calls.push({ model, input }); return model.includes("m2m100") ? { translated_text: "Molokhia; chicken" } : { image: "QUJD" }; } },
    PER_VISITOR: { limit: async () => ({ success: true }) }
  };
  return { env, calls, writes, quota };
}

const post = (body, origin = "https://meal-planner-af799.web.app") => req({ user: "rami", ...body }, origin);
const req = (body, origin) =>
  new Request("https://w.test/photo", { method: "POST", headers: { Origin: origin, "Content-Type": "application/json" }, body: JSON.stringify(body) });

test("draws a catalog meal without a photo and returns a JPEG data URL", async () => {
  const { env, calls } = setup();
  const res = await worker.fetch(post({ mealId: "en_n12" }), env);
  assert.equal(res.status, 200);
  assert.equal((await res.json()).image, "data:image/jpeg;base64,QUJD");
  assert.equal(res.headers.get("Access-Control-Allow-Origin"), "https://meal-planner-af799.web.app");
  assert.match(calls[0].input.prompt, /Beef kofta with bulgur salad.*minced beef, bulgur/);
});

test("refuses unknown, archived and already pictured meals, odd ids and foreign sites", async () => {
  const { env } = setup();
  assert.equal((await worker.fetch(post({ mealId: "nope" }), env)).status, 404);
  assert.equal((await worker.fetch(post({ mealId: "old" }), env)).status, 404);
  assert.equal((await worker.fetch(post({ mealId: "done" }), env)).status, 409);
  assert.equal((await worker.fetch(post({ mealId: "../x" }), env)).status, 400);
  assert.equal((await worker.fetch(post({ mealId: "en_n12" }, "https://evil.example"), env)).status, 403);
});

test("each account gets ten photos a week; only drawn pictures count", async () => {
  const monday = mondayOf(new Date());
  const { env, writes } = setup();
  const res = await worker.fetch(post({ mealId: "en_n12" }), env);
  const body = await res.json();
  assert.equal(body.used, 1);
  assert.equal(body.limit, 10);
  assert.deepEqual(writes, [{ u: `${DB}/PhotoQuota/rami/${monday}.json`, body: 1 }]);

  const full = setup({ [`${DB}/PhotoQuota/rami/${monday}.json`]: 10 });
  const refused = await worker.fetch(post({ mealId: "en_n12" }), full.env);
  assert.equal(refused.status, 429);
  assert.deepEqual(await refused.json(), { error: "weekly_limit", used: 10, limit: 10, resetsOn: addDays(monday, 7) });
  assert.equal(full.calls.length, 0);
  assert.equal(full.writes.length, 0);

  const failing = setup();
  failing.env.AI.run = async () => ({});
  assert.equal((await worker.fetch(post({ mealId: "en_n12" }), failing.env)).status, 502);
  assert.equal(failing.writes.length, 0);
});

test("the account must exist and be sent", async () => {
  const { env } = setup();
  assert.equal((await worker.fetch(post({ mealId: "en_n12", user: "nobody" }), env)).status, 403);
  assert.equal((await worker.fetch(req({ mealId: "en_n12" }, "https://meal-planner-af799.web.app"), env)).status, 400);
  assert.equal((await worker.fetch(post({ mealId: "en_n12", user: "a/b" }), env)).status, 400);
  assert.equal((await worker.fetch(post({ mealId: "en_n12", user: "guest_1" }), env)).status, 200);
});

test("a busy visitor and a spent daily allowance read as 429", async () => {
  const { env } = setup();
  env.PER_VISITOR.limit = async () => ({ success: false });
  assert.deepEqual(await (await worker.fetch(post({ mealId: "en_n12" }), env)).json(), { error: "busy" });
  const second = setup().env;
  second.AI.run = async () => { throw new Error("4006: you have used up your daily free allocation of 10,000 neurons"); };
  const res = await worker.fetch(post({ mealId: "en_n12" }), second);
  assert.equal(res.status, 429);
  assert.deepEqual(await res.json(), { error: "daily_limit" });
});

test("non-English meals are described in English: seed twin first, else a translation", async () => {
  const { env, calls } = setup();
  assert.equal((await englishDish(env, DB, "n12", catalog.n12)).name, "Beef kofta with bulgur salad");
  assert.equal(calls.length, 0);
  const molokhia = await englishDish(env, DB, "m3", catalog.m3);
  assert.equal(molokhia.name, "Molokhia");
  assert.deepEqual(molokhia.ingredients, ["chicken"]);
  assert.equal(calls[0].input.source_lang, "arabic");
});

test("helpers", () => {
  assert.deepEqual(ingredientWords({ ingredientLines: [{ id: "chickenBreast" }, { id: "rice" }] }), ["chicken breast", "rice"]);
  assert.deepEqual(ingredientWords({ ingredients: ["Reis", "زيت"] }), ["Reis"]);
  assert.match(prompt({ name: "Foul", ingredients: [] }), /^A realistic.*"Foul"\. One serving.*No text/);
  assert.ok(isAllowedOrigin("https://meal-planner-af799--pr23-claude-x.web.app"));
  assert.ok(!isAllowedOrigin("https://meal-planner-af799.web.app.evil.com"));
  assert.ok(isUserKey("rami13195_at_gmail_dot_com") && isUserKey("guest_3f2a-1b") && !isUserKey("a.b") && !isUserKey(""));
  assert.equal(mondayOf(new Date("2026-11-22T23:30:00Z")), "2026-11-16"); // Sunday night still counts for that week
  assert.equal(mondayOf(new Date("2026-11-23T00:10:00Z")), "2026-11-23");
});
