// Run with: node --test photo-worker/test/*.test.js   (Node 20+, no packages)
import { test } from "node:test";
import assert from "node:assert/strict";
import worker, { englishDish, ingredientWords, prompt, isAllowedOrigin } from "../src/index.js";

const DB = "https://db.test";
const catalog = {
  en_n12: { name: "Beef kofta with bulgur salad", language: "en", ingredientLines: [{ id: "mincedBeef" }, { id: "bulgur" }] },
  n12: { name: "كفتة مشوية مع سلطة برغل", language: "ar", ingredientLines: [{ id: "mincedBeef" }] },
  m3: { name: "ملوخية", language: "ar", ingredients: ["ملوخية", "دجاج"] },
  old: { name: "Gone", language: "en", isArchived: true },
  done: { name: "Soup", language: "en" }
};
const photos = { done: "data:image/jpeg;base64,xx" };

function setup() {
  const calls = [];
  globalThis.fetch = async url => {
    const m = String(url).match(/\/(MealCatalog|MealPhotos)\/([^.]+)\.json/);
    const value = m ? (m[1] === "MealCatalog" ? catalog : photos)[m[2]] ?? null : null;
    return new Response(JSON.stringify(m && m[1] === "MealPhotos" && value ? true : value), { status: 200 });
  };
  const env = {
    FIREBASE_URL: DB,
    AI: { run: async (model, input) => { calls.push({ model, input }); return model.includes("m2m100") ? { translated_text: "Molokhia; chicken" } : { image: "QUJD" }; } },
    PER_VISITOR: { limit: async () => ({ success: true }) }
  };
  return { env, calls };
}

const post = (body, origin = "https://meal-planner-af799.web.app") =>
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
});
