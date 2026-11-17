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
    if (url.pathname !== "/photo") return json({ error: "not_found" }, 404, cors);
    if (request.method !== "POST") return json({ error: "method" }, 405, cors);
    if (!isAllowedOrigin(origin)) return json({ error: "origin" }, 403, cors);

    let mealId = "", user = "", replace = false;
    try {
      const body = await request.json();
      mealId = String(body.mealId || "");
      user = String(body.user || "");
      replace = body.replace === true;
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
    const monday = mondayOf(new Date());
    const quotaUrl = `${db}/PhotoQuota/${userPath}/${monday}.json`;
    const used = Number(await readJson(quotaUrl)) || 0;
    if (used >= limit) return json({ error: "weekly_limit", used, limit, resetsOn: addDays(monday, 7) }, 429, cors);

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
      return json({ image: `data:image/jpeg;base64,${result.image}`, twin: twin ? twinId : undefined, used: used + 1, limit }, 200, cors);
    } catch (e) {
      // On the free plan Workers AI stops for the day once the allowance is used (error 4006).
      const text = String(e?.message || e);
      if (/4006|allocation|neurons|quota/i.test(text)) return json({ error: "daily_limit" }, 429, cors);
      return json({ error: "model_failed", detail: text.slice(0, 200) }, 502, cors);
    }
  }
};

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
