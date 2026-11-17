// Run with: node --test photo-worker/test/*.test.js   (Node 20+, no packages)
import { test } from "node:test";
import assert from "node:assert/strict";
import worker, { cleanSteps, isOwner, englishDish, englishTwinId, ingredientWords, prompt, isAllowedOrigin, isUserKey, mondayOf, addDays } from "../src/index.js";

const DB = "https://db.test";
const catalog = {
  en_n12: { name: "Beef kofta with bulgur salad", language: "en", ingredientLines: [{ id: "mincedBeef" }, { id: "bulgur" }] },
  n12: { name: "كفتة مشوية مع سلطة برغل", language: "ar", ingredientLines: [{ id: "mincedBeef" }] },
  m12: { name: "يبرق", language: "ar", ingredients: ["ورق عنب", "رز", "لحمة"] },
  n7: { name: "جبنة قريش مع توت", language: "ar", ingredientLines: [{ id: "cottageCheese" }] },
  en_n7: { name: "Cottage cheese and berries", language: "en", ingredientLines: [{ id: "cottageCheese" }, { id: "berries" }] },
  u1: { name: "مقلوبة باذنجان", language: "ar", ingredients: ["باذنجان", "رز"] },
  old: { name: "Gone", language: "en", isArchived: true },
  done: { name: "Soup", language: "en" }
};
const photos = { done: "data:image/jpeg;base64,xx", en_n7: "data:image/jpeg;base64,ENGLISH" };
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
    if (m && u.includes("shallow=true") && value) value = true; // shallow reads
    if (u.includes("/PhotoQuota/")) value = quota[u] ?? null;
    return new Response(JSON.stringify(value), { status: 200 });
  };
  const env = {
    FIREBASE_URL: DB,
    AI: { run: async (model, input) => {
      calls.push({ model, input });
      if (model.includes("llama")) return { response: '{"name": "Aubergine maqluba", "look": "upside-down rice cake with fried aubergine on top"}' };
      if (model.includes("m2m100")) return { translated_text: "Maqluba; aubergine" };
      return { image: "QUJD" };
    } },
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

test("the owner's code lifts the weekly limit, a wrong or short one does not", async () => {
  const monday = mondayOf(new Date());
  const owner = setup({ [`${DB}/PhotoQuota/rami/${monday}.json`]: 10 });
  owner.env.OWNER_CODE = "a-long-private-code";
  const res = await worker.fetch(post({ mealId: "en_n12", ownerCode: "a-long-private-code" }), owner.env);
  assert.equal(res.status, 200);
  assert.equal((await res.json()).owner, true);
  assert.equal((await worker.fetch(post({ mealId: "en_n12", ownerCode: "a-long-private-cod3" }), owner.env)).status, 429);
  assert.equal(isOwner({ OWNER_CODE: "short" }, "short"), false);
  assert.equal(isOwner({}, ""), false);
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

test("an Arabic meal takes its English twin's photo without drawing or counting", async () => {
  const { env, calls, writes } = setup();
  const body = await (await worker.fetch(post({ mealId: "n7" }), env)).json();
  assert.equal(body.image, "data:image/jpeg;base64,ENGLISH");
  assert.equal(body.reused, true);
  assert.equal(body.twin, "en_n7");
  assert.equal(calls.length, 0);
  assert.equal(writes.length, 0);
});

test("without a twin photo it draws from the English meal and names it", async () => {
  const { env, calls, writes } = setup();
  const body = await (await worker.fetch(post({ mealId: "n12" }), env)).json();
  assert.equal(body.twin, "en_n12");
  assert.equal(calls.length, 1);
  assert.match(calls[0].input.prompt, /"Beef kofta with bulgur salad".*minced beef, bulgur/);
  assert.equal(writes.length, 1);
});

test("Arabic classics use the curated English name and look", async () => {
  const { env, calls } = setup();
  await worker.fetch(post({ mealId: "m12" }), env);
  assert.equal(calls.length, 1);
  assert.match(calls[0].input.prompt, /"Stuffed vine leaves \(yabraq\)": rolled grape leaves stuffed with rice and meat/);
});

test("other meals ask Llama for the English name, then fall back to a translation", async () => {
  const { env, calls } = setup();
  const dish = await englishDish(env, "u1", catalog.u1);
  assert.equal(dish.name, "Aubergine maqluba");
  assert.match(prompt(dish), /"Aubergine maqluba": upside-down rice cake with fried aubergine on top/);
  assert.match(calls[0].input.messages[1].content, /Arabic dish: مقلوبة باذنجان/);

  env.AI.run = async (model, input) => { calls.push({ model }); if (model.includes("llama")) throw new Error("down"); return { translated_text: "Maqluba; aubergine" }; };
  assert.equal((await englishDish(env, "u1", catalog.u1)).name, "Maqluba");
});

test("New photo redraws a meal that already has one, and counts", async () => {
  const { env, calls, writes } = setup();
  assert.equal((await worker.fetch(post({ mealId: "done" }), env)).status, 409);
  const res = await worker.fetch(post({ mealId: "done", replace: true }), env);
  assert.equal(res.status, 200);
  assert.equal(calls.length, 1);
  assert.equal(writes.length, 1);
  // A redraw of an Arabic meal draws anew rather than taking the twin's photo.
  const again = setup();
  const body = await (await worker.fetch(post({ mealId: "n7", replace: true }), again.env)).json();
  assert.equal(body.reused, undefined);
  assert.equal(again.calls.length, 1);
});

test("helpers", () => {
  assert.deepEqual(ingredientWords({ ingredientLines: [{ id: "chickenBreast" }, { id: "rice" }] }), ["chicken breast", "rice"]);
  assert.deepEqual(ingredientWords({ ingredients: ["Reis", "زيت"] }), ["Reis"]);
  assert.match(prompt({ name: "Foul", ingredients: [] }), /^A realistic.*"Foul"\. One serving.*No text/);
  assert.ok(isAllowedOrigin("https://meal-planner-af799--pr23-claude-x.web.app"));
  assert.ok(!isAllowedOrigin("https://meal-planner-af799.web.app.evil.com"));
  assert.equal(englishTwinId("de_n3", { language: "de" }), "en_n3");
  assert.equal(englishTwinId("en_n3", { language: "en" }), null);
  assert.equal(englishTwinId("m5", { language: "ar" }), null);
  assert.ok(isUserKey("rami13195_at_gmail_dot_com") && isUserKey("guest_3f2a-1b") && !isUserKey("a.b") && !isUserKey(""));
  assert.equal(mondayOf(new Date("2026-11-22T23:30:00Z")), "2026-11-16"); // Sunday night still counts for that week
  assert.equal(mondayOf(new Date("2026-11-23T00:10:00Z")), "2026-11-23");
});

const stepsReq = (body, origin = "https://meal-planner-af799.web.app") =>
  new Request("https://w.test/steps", { method: "POST", headers: { Origin: origin, "Content-Type": "application/json" }, body: JSON.stringify({ user: "rami", ...body }) });

test("/steps writes short steps in the meal's own language", async () => {
  const { env, calls } = setup();
  env.AI.run = async (model, input) => { calls.push({ model, input }); return { response: 'Sure: {"steps":[{"text":"لف ورق العنب","minutes":null},{"text":"اطبخه","minutes":45}]}' }; };
  const res = await worker.fetch(stepsReq({ mealId: "m12" }), env);
  assert.equal(res.status, 200);
  assert.deepEqual((await res.json()).steps, [{ text: "لف ورق العنب", minutes: null }, { text: "اطبخه", minutes: 45 }]);
  assert.match(calls[0].input.messages[0].content, /written in Arabic/);
  assert.match(calls[0].input.messages[1].content, /يبرق \(in English: Stuffed vine leaves/);
});

test("/steps falls back to the small model, and checks the request like /photo", async () => {
  const { env, calls } = setup();
  env.AI.run = async model => { calls.push(model); if (model.includes("70b")) throw new Error("busy"); return { response: '{"steps":["a","b"]}' }; };
  assert.equal((await worker.fetch(stepsReq({ mealId: "en_n12" }), env)).status, 200);
  assert.equal(calls.length, 2);
  assert.equal((await worker.fetch(stepsReq({ mealId: "en_n12" }, "https://evil.example"), env)).status, 403);
  assert.equal((await worker.fetch(stepsReq({ mealId: "en_n12", user: "nobody" }), env)).status, 403);
  catalog.withSteps = { name: "X", language: "en", steps: [{ text: "a" }] };
  assert.equal((await worker.fetch(stepsReq({ mealId: "withSteps" }), env)).status, 409);
  assert.equal((await worker.fetch(stepsReq({ mealId: "withSteps", replace: true }), env)).status, 200);
});

test("cleanSteps keeps only usable steps", () => {
  assert.equal(cleanSteps("no json"), null);
  assert.equal(cleanSteps('{"steps":["only one"]}'), null);
  assert.deepEqual(cleanSteps('{"steps":[{"text":" a ","minutes":5000},{"text":"b","minutes":3},{"text":""}]}'), [{ text: "a", minutes: null }, { text: "b", minutes: 3 }]);
});
