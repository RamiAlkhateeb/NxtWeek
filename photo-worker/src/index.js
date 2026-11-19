// Makdous meal photos: POST /photo { "mealId": "...", "user": "<account key>" }
//   → { "image": "data:image/jpeg;base64,...", "used": n, "limit": 10 }.
//
// Only meals that exist in the shared catalog and have no shared photo yet are drawn, so the endpoint
// cannot be used to draw anything else, nor to redraw what everyone already has. Each account (users/{key})
// may have WEEKLY_LIMIT photos drawn per week, Monday to Sunday in UTC, counted at PhotoQuota/{key}/{monday}.
// The app shrinks the picture and stores it at MealPhotos/{mealId} itself.
//
// A meal in Arabic or German that also exists in English (the seeded n1–n30 come in all three) takes the English
// meal's photo when there is one: nothing is drawn and nothing is counted. Otherwise the picture is drawn from the
// English meal, and the reply names it ("twin") so the app stores the photo for both. "replace": true redraws a
// meal that already has a photo ("New photo"); it counts like any other.

import { ARABIC_SEED_DISHES } from "./dishes.js";

const IMAGE_MODEL = "@cf/black-forest-labs/flux-1-schnell";
const TEXT_MODEL = "@cf/meta/llama-3.1-8b-instruct";
// Steps read better from the larger model; the small one is the fallback.
const STEPS_MODELS = ["@cf/meta/llama-3.3-70b-instruct-fp8-fast", TEXT_MODEL];
const LANGUAGE_NAMES = { ar: "Arabic", en: "English", de: "German" };
const TRANSLATE_MODEL = "@cf/meta/m2m100-1.2b";

const ALLOWED_ORIGINS = [
  /^https:\/\/meal-planner-af799\.(web\.app|firebaseapp\.com)$/,
  /^https:\/\/meal-planner-af799--[a-z0-9-]+\.web\.app$/, // Firebase preview channels
  /^http:\/\/localhost(:\d+)?$/
];

export default {
  async fetch(request, env) {
    const origin = request.headers.get("Origin") || "";
    const cors = corsHeaders(origin);
    if (request.method === "OPTIONS") return new Response(null, { status: 204, headers: cors });

    const url = new URL(request.url);
    if (url.pathname === "/steps") return steps(request, env, origin, cors);
    if (url.pathname === "/details") return details(request, env, origin, cors);
    if (url.pathname === "/store") return store(request, env, origin, cors);
    if (url.pathname !== "/photo") return json({ error: "not_found" }, 404, cors);
    if (request.method !== "POST") return json({ error: "method" }, 405, cors);
    if (!isAllowedOrigin(origin)) return json({ error: "origin" }, 403, cors);

    let mealId = "", user = "", replace = false, ownerCode = "";
    try {
      const body = await request.json();
      mealId = String(body.mealId || "");
      user = String(body.user || "");
      replace = body.replace === true;
      ownerCode = String(body.ownerCode || "");
    } catch { /* handled below */ }
    if (!/^[A-Za-z0-9_-]{1,64}$/.test(mealId) || !isUserKey(user)) return json({ error: "bad_request" }, 400, cors);

    if (env.PER_VISITOR) {
      const visitor = request.headers.get("CF-Connecting-IP") || "unknown";
      const { success } = await env.PER_VISITOR.limit({ key: visitor });
      if (!success) return json({ error: "busy" }, 429, cors);
    }

    const db = env.FIREBASE_URL.replace(/\/$/, "");
    const userPath = encodeURIComponent(user);
    if (!(await readJson(`${db}/users/${userPath}.json?shallow=true`))) return json({ error: "unknown_user" }, 403, cors);

    const limit = weeklyLimit(env);
    const owner = isOwner(env, ownerCode);
    const monday = mondayOf(new Date());
    const quotaUrl = `${db}/PhotoQuota/${userPath}/${monday}.json`;
    const used = Number(await readJson(quotaUrl)) || 0;
    if (!owner && used >= limit) return json({ error: "weekly_limit", used, limit, resetsOn: addDays(monday, 7) }, 429, cors);

    const meal = await readJson(`${db}/MealCatalog/${mealId}.json`);
    if (!meal || meal.isArchived) return json({ error: "unknown_meal" }, 404, cors);
    if (!replace && await readJson(`${db}/MealPhotos/${mealId}.json?shallow=true`)) return json({ error: "exists" }, 409, cors);

    // The same dish in English: reuse its photo, or draw from it.
    const twinId = englishTwinId(mealId, meal);
    const twin = twinId ? await readJson(`${db}/MealCatalog/${twinId}.json`) : null;
    if (twin && !replace) {
      const twinPhoto = await readJson(`${db}/MealPhotos/${twinId}.json`);
      if (typeof twinPhoto === "string" && twinPhoto.startsWith("data:image/"))
        return json({ image: twinPhoto, reused: true, twin: twinId, used, limit }, 200, cors);
    }

    try {
      const dish = twin ? { name: twin.name, ingredients: ingredientWords(twin) } : await englishDish(env, mealId, meal);
      const result = await env.AI.run(IMAGE_MODEL, { prompt: prompt(dish), steps: 4 });
      if (!result?.image) return json({ error: "no_image" }, 502, cors);
      // Only a picture actually drawn counts against the week.
      await fetch(quotaUrl, { method: "PUT", body: JSON.stringify(used + 1) });
      return json({ image: `data:image/jpeg;base64,${result.image}`, twin: twin ? twinId : undefined, used: used + 1, limit, owner: owner || undefined }, 200, cors);
    } catch (e) {
      // On the free plan Workers AI stops for the day once the allowance is used (error 4006).
      const text = String(e?.message || e);
      if (/4006|allocation|neurons|quota/i.test(text)) return json({ error: "daily_limit" }, 429, cors);
      return json({ error: "model_failed", detail: text.slice(0, 200) }, 502, cors);
    }
  }
};

/** POST /steps { mealId, user, replace? } → { steps: [{ text, minutes }] }: short cooking steps in the meal's own
 *  language from Llama. Same checks as /photo (site, account, minute limit); a meal with steps is only redone when
 *  "replace" is sent. Text is cheap, so there is no weekly count. The app saves the steps to the catalog. */
async function steps(request, env, origin, cors) {
  if (request.method !== "POST") return json({ error: "method" }, 405, cors);
  if (!isAllowedOrigin(origin)) return json({ error: "origin" }, 403, cors);
  let mealId = "", user = "", replace = false;
  try { const b = await request.json(); mealId = String(b.mealId || ""); user = String(b.user || ""); replace = b.replace === true; } catch { /* below */ }
  if (!/^[A-Za-z0-9_-]{1,64}$/.test(mealId) || !isUserKey(user)) return json({ error: "bad_request" }, 400, cors);
  if (env.PER_VISITOR) {
    const { success } = await env.PER_VISITOR.limit({ key: request.headers.get("CF-Connecting-IP") || "unknown" });
    if (!success) return json({ error: "busy" }, 429, cors);
  }
  const db = env.FIREBASE_URL.replace(/\/$/, "");
  if (!(await readJson(`${db}/users/${encodeURIComponent(user)}.json?shallow=true`))) return json({ error: "unknown_user" }, 403, cors);
  const meal = await readJson(`${db}/MealCatalog/${mealId}.json`);
  if (!meal || meal.isArchived) return json({ error: "unknown_meal" }, 404, cors);
  if (!replace && Array.isArray(meal.steps) && meal.steps.length) return json({ error: "exists" }, 409, cors);

  const language = LANGUAGE_NAMES[meal.language || "ar"] || "Arabic";
  const hint = (meal.language || "ar") === "ar" && ARABIC_SEED_DISHES[mealId] ? ` (in English: ${ARABIC_SEED_DISHES[mealId][0]})` : "";
  const ingredients = [...(meal.ingredientLines || []).map(l => l.name), ...(meal.ingredients || [])].filter(Boolean).slice(0, 12);
  const messages = [
    { role: "system", content: `You write short home-cooking steps. Reply with JSON only: {"steps":[{"text": string, "minutes": number or null}]}. ` +
      `4 to 8 steps, each one short sentence, written in ${language}. "minutes" is how long that step takes when it involves waiting or cooking, else null.` },
    { role: "user", content: `Dish: ${meal.name}${hint}\nIngredients: ${ingredients.join(", ") || "unknown"}` }
  ];
  for (const model of STEPS_MODELS) {
    try {
      const out = await env.AI.run(model, { messages, max_tokens: 700 });
      const parsed = cleanSteps(out?.response);
      if (parsed) return json({ steps: parsed }, 200, cors);
    } catch (e) {
      if (/4006|allocation|neurons|quota/i.test(String(e?.message || e))) return json({ error: "daily_limit" }, 429, cors);
    }
  }
  return json({ error: "model_failed" }, 502, cors);
}

/** Checks shared by the text endpoints: method, site, a JSON body with a valid account, the minute limit and an
 *  existing account. Returns { body } or { error: Response }. */
async function textRequest(request, env, origin, cors) {
  if (request.method !== "POST") return { error: json({ error: "method" }, 405, cors) };
  if (!isAllowedOrigin(origin)) return { error: json({ error: "origin" }, 403, cors) };
  let body = null;
  try { body = await request.json(); } catch { /* below */ }
  const user = String(body?.user || "");
  if (!body || !isUserKey(user)) return { error: json({ error: "bad_request" }, 400, cors) };
  if (env.PER_VISITOR) {
    const { success } = await env.PER_VISITOR.limit({ key: request.headers.get("CF-Connecting-IP") || "unknown" });
    if (!success) return { error: json({ error: "busy" }, 429, cors) };
  }
  const db = env.FIREBASE_URL.replace(/\/$/, "");
  if (!(await readJson(`${db}/users/${encodeURIComponent(user)}.json?shallow=true`))) return { error: json({ error: "unknown_user" }, 403, cors) };
  return { body };
}

const isDailyLimit = e => /4006|allocation|neurons|quota/i.test(String(e?.message || e));

/** POST /details { user, name, type, language, ingredients[] } → { details }: ingredients per serving (with an English
 *  camelCase id, unit and aisle), servings, minutes and nutrition per serving, for the app's users without a Gemini key.
 *  The meal is sent rather than read, because "Recalculate" asks for a cleared copy that is not saved yet. Nothing is
 *  saved here; the app checks the numbers (MealDetailsMapper.Validate) and fills the meal. */
async function details(request, env, origin, cors) {
  const { body, error } = await textRequest(request, env, origin, cors);
  if (error) return error;
  const name = String(body.name || "").trim();
  const ingredients = Array.isArray(body.ingredients) ? body.ingredients.map(i => String(i ?? "").trim()) : [];
  if (!name || name.length > 120 || ingredients.length > 30 || ingredients.some(i => i.length > 80)) return json({ error: "bad_request" }, 400, cors);
  const lang = LANGUAGE_NAMES[String(body.language || "ar")] ? String(body.language || "ar") : "ar";
  const type = String(body.type || "").slice(0, 20);
  const mealId = String(body.mealId || "");
  const hint = lang === "ar" && ARABIC_SEED_DISHES[mealId] ? ` (in English: ${ARABIC_SEED_DISHES[mealId][0]})` : "";
  const known = ingredients.filter(Boolean).length ? `\nThe cook listed these ingredients; keep them and add amounts: ${ingredients.filter(Boolean).join(", ")}` : "";
  const messages = [
    { role: "system", content: "You are a nutrition assistant for a home-cooking meal planner. Reply with JSON only: " +
      '{"ingredients":[{"id": English camelCase like "chickenBreast", "name": the ingredient as a shopper writes it, "quantity": number per serving, "unit": "g"|"ml"|"pc", "aisle": "produce"|"meat"|"dairy"|"pantry"|"bakery"|"other"}], ' +
      '"servings": how many servings the recipe makes, "minutes": total cooking time, "kcal", "protein", "carbs", "fat", "fibre": per serving (grams for the last four)}. ' +
      `Ingredient names are in ${LANGUAGE_NAMES[lang]}. Use typical home amounts for ONE serving; 4*protein + 4*carbs + 9*fat should be close to kcal.` },
    { role: "user", content: `Dish: ${name}${hint}${type ? ` (category: ${type})` : ""}${known}` }
  ];
  for (const model of STEPS_MODELS) {
    try {
      const out = await env.AI.run(model, { messages, max_tokens: 900 });
      const parsed = cleanDetails(out?.response);
      if (parsed) return json({ details: parsed }, 200, cors);
    } catch (e) {
      if (isDailyLimit(e)) return json({ error: "daily_limit" }, 429, cors);
    }
  }
  return json({ error: "model_failed" }, 502, cors);
}

const UNITS = ["g", "ml", "pc"];
const AISLES = ["produce", "meat", "dairy", "pantry", "bakery", "other"];
const finite = (v, max = 5000) => { const n = Number(v); return Number.isFinite(n) && n >= 0 && n <= max ? n : null; };

/** The model's reply as { ingredients, servings, minutes, kcal, protein, carbs, fat, fibre }, or null when unusable.
 *  Units and aisles outside the lists are mapped or dropped; whether the numbers are plausible is the app's call. */
export function cleanDetails(reply) {
  const text = typeof reply === "string" ? reply : JSON.stringify(reply ?? "");
  const found = text.match(/\{[\s\S]*\}/);
  if (!found) return null;
  let d;
  try { d = JSON.parse(found[0]); } catch { return null; }
  const unitOf = u => { const v = String(u || "").toLowerCase(); return UNITS.includes(v) ? v : /^(gram|grams)$/.test(v) ? "g" : /^(piece|pieces|pcs)$/.test(v) ? "pc" : null; };
  const ingredients = (Array.isArray(d.ingredients) ? d.ingredients : [])
    .map(i => ({
      id: String(i?.id || "").trim().slice(0, 60),
      name: String(i?.name || "").trim().slice(0, 80),
      quantity: finite(i?.quantity),
      unit: unitOf(i?.unit),
      aisle: AISLES.includes(String(i?.aisle || "").toLowerCase()) ? String(i.aisle).toLowerCase() : "other"
    }))
    .filter(i => i.name && i.quantity > 0 && i.unit)
    .slice(0, 25);
  const kcal = finite(d.kcal), protein = finite(d.protein, 500), carbs = finite(d.carbs, 1000), fat = finite(d.fat, 500), fibre = finite(d.fibre, 200);
  if (kcal === null || protein === null || carbs === null || fat === null) return null;
  const servings = Number.isInteger(d.servings) && d.servings > 0 && d.servings <= 20 ? d.servings : 1;
  const minutes = Number.isInteger(d.minutes) && d.minutes > 0 && d.minutes <= 600 ? d.minutes : 0;
  return { ingredients, servings, minutes, kcal, protein, carbs, fat, fibre: fibre ?? 0 };
}

/** POST /store { user, name } → { domain, emoji }: the store chain's website and an emoji, for its logo; empty strings
 *  when the model does not know it. The app cleans both again (StoreLogo.Clean). */
async function store(request, env, origin, cors) {
  const { body, error } = await textRequest(request, env, origin, cors);
  if (error) return error;
  const name = String(body.name || "").trim();
  if (!name || name.length > 60) return json({ error: "bad_request" }, 400, cors);
  try {
    const out = await env.AI.run(TEXT_MODEL, {
      messages: [
        { role: "system", content: 'You name shops. Reply with JSON only: {"domain": the chain\'s main website domain like "lidl.de", or "" if it is not a known chain, "emoji": one emoji for the kind of shop}.' },
        { role: "user", content: `Shop on a family's shopping list: ${name}` }
      ],
      max_tokens: 60
    });
    return json(cleanStore(out?.response), 200, cors);
  } catch (e) {
    if (isDailyLimit(e)) return json({ error: "daily_limit" }, 429, cors);
    return json({ error: "model_failed" }, 502, cors);
  }
}

/** { domain, emoji } from the model's reply: a plain lowercase domain or "", one short emoji or "". */
export function cleanStore(reply) {
  const text = typeof reply === "string" ? reply : JSON.stringify(reply ?? "");
  const found = text.match(/\{[\s\S]*\}/);
  let d = {};
  try { d = found ? JSON.parse(found[0]) : {}; } catch { d = {}; }
  let domain = String(d.domain || "").trim().toLowerCase().replace(/^[a-z]+:\/\//, "").split(/[/?#]/)[0].replace(/^www\./, "");
  if (!/^(?=.{4,63}$)([a-z0-9]([a-z0-9-]*[a-z0-9])?\.)+[a-z]{2,24}$/.test(domain)) domain = "";
  const emoji = String(d.emoji || "").trim();
  return { domain, emoji: emoji.length <= 16 && !/[\p{L}\p{N}\s]/u.test(emoji) ? emoji : "" };
}

/** The model's reply as 2–12 steps of at most 240 characters, with minutes 1–600 or null; null when unusable. */
export function cleanSteps(reply) {
  const text = typeof reply === "string" ? reply : JSON.stringify(reply ?? "");
  const found = text.match(/\{[\s\S]*\}/);
  if (!found) return null;
  let list;
  try { list = JSON.parse(found[0]).steps; } catch { return null; }
  if (!Array.isArray(list)) return null;
  const steps = list
    .map(s => ({ text: String(s?.text ?? s ?? "").trim().slice(0, 240), minutes: Number.isInteger(s?.minutes) && s.minutes > 0 && s.minutes <= 600 ? s.minutes : null }))
    .filter(s => s.text.length > 0)
    .slice(0, 12);
  return steps.length >= 2 ? steps : null;
}

/** The English meal that is the same dish: the seeded n1–n30 exist as n12 (ar), en_n12 and de_n12. */
export function englishTwinId(mealId, meal) {
  if ((meal?.language || "ar") === "en") return null;
  const m = mealId.match(/^(?:de_)?(n\d+)$/);
  return m ? `en_${m[1]}` : null;
}

/** The dish in English with how it looks, for a meal that has no English twin: the curated table for the Arabic
 *  classics (m1–m49), else Llama's common English name and description, else a plain translation. */
export async function englishDish(env, mealId, meal) {
  const lang = meal.language || "ar";
  const ingredients = ingredientWords(meal);
  if (lang === "en") return { name: meal.name, ingredients };
  if (lang === "ar" && ARABIC_SEED_DISHES[mealId]) {
    const [name, look] = ARABIC_SEED_DISHES[mealId];
    return { name, look, ingredients };
  }
  const source = lang === "de" ? "German" : "Arabic";
  const raw = [meal.name, ...(meal.ingredients || []).slice(0, 8)];
  try {
    const out = await env.AI.run(TEXT_MODEL, {
      messages: [
        { role: "system", content: "You name dishes for a food photographer. Reply with JSON only: {\"name\": the dish's usual English name, \"look\": one short sentence on how it is plated and what it looks like}." },
        { role: "user", content: `${source} dish: ${meal.name}\nIngredients: ${raw.slice(1).join(", ") || "unknown"}` }
      ],
      max_tokens: 120
    });
    const found = String(out?.response || "").match(/\{[\s\S]*\}/);
    const parsed = found ? JSON.parse(found[0]) : null;
    if (parsed?.name) return { name: String(parsed.name).slice(0, 80), look: parsed.look ? String(parsed.look).slice(0, 200) : undefined, ingredients };
  } catch { /* fall back to a translation */ }
  try {
    const out = await env.AI.run(TRANSLATE_MODEL, { text: raw.join("; "), source_lang: source.toLowerCase(), target_lang: "english" });
    const [name, ...rest] = String(out?.translated_text || "").split(/;\s*/);
    if (name) return { name, ingredients: rest.length ? rest : ingredients };
  } catch { /* fall through */ }
  return { name: meal.name, ingredients };
}

/** Ingredient words: English ids from the measured lines ("mincedBeef" → "minced beef") when there are any. */
export function ingredientWords(meal) {
  const lines = (meal.ingredientLines || []).map(l => String(l.id || "").replace(/([a-z])([A-Z])/g, "$1 $2").toLowerCase()).filter(Boolean);
  if (lines.length) return lines.slice(0, 8);
  return (meal.ingredients || []).filter(i => /^[\x20-\x7E]+$/.test(i)).slice(0, 8);
}

export function prompt({ name, look, ingredients }) {
  return `A realistic, appetising food photograph of "${String(name).trim()}"`
    + (look ? `: ${String(look).trim().replace(/\.$/, "")}` : "")
    + (ingredients?.length ? ` (made with ${ingredients.join(", ")})` : "")
    + ". One serving, plated as it is usually served, seen from slightly above, soft natural daylight, "
    + "plain light background, square framing with the dish in the centre. "
    + "No text, no labels, no logos, no hands, no people.";
}

/** An account key as the app stores it under users/: no characters Firebase forbids in a key. */
export function isUserKey(key) { return /^[^.#$\[\]\/\s]{1,128}$/.test(key); }

/** The owner's code (the worker secret OWNER_CODE, set in the Cloudflare dashboard) lifts the weekly limit;
 *  the minute limit and Cloudflare's daily allowance stay. Compared in constant time. */
export function isOwner(env, code) {
  const secret = String(env.OWNER_CODE || "");
  if (secret.length < 12 || code.length !== secret.length) return false;
  let diff = 0;
  for (let i = 0; i < secret.length; i++) diff |= secret.charCodeAt(i) ^ code.charCodeAt(i);
  return diff === 0;
}

export function weeklyLimit(env) { return Math.max(0, Number(env.WEEKLY_LIMIT ?? 10) || 0); }

/** The Monday (UTC) of the week that contains `date`, as yyyy-MM-dd. */
export function mondayOf(date) {
  const d = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
  d.setUTCDate(d.getUTCDate() - ((d.getUTCDay() + 6) % 7));
  return d.toISOString().slice(0, 10);
}

export function addDays(isoDate, n) {
  const d = new Date(`${isoDate}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
}

export function isAllowedOrigin(origin) { return ALLOWED_ORIGINS.some(r => r.test(origin)); }

function corsHeaders(origin) {
  return {
    "Access-Control-Allow-Origin": isAllowedOrigin(origin) ? origin : "null",
    "Access-Control-Allow-Methods": "POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type",
    "Access-Control-Max-Age": "86400",
    "Vary": "Origin"
  };
}

async function readJson(url) {
  const r = await fetch(url);
  if (!r.ok) return null;
  return r.json();
}

function json(body, status, headers) {
  return new Response(JSON.stringify(body), { status, headers: { ...headers, "Content-Type": "application/json", "Cache-Control": "no-store" } });
}
