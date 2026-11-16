// Makdous meal photos: POST /photo { "mealId": "..." } → { "image": "data:image/jpeg;base64,..." }.
//
// Only meals that exist in the shared catalog and have no shared photo yet are drawn, so the endpoint
// cannot be used to draw anything else, nor to redraw what everyone already has. The app shrinks the
// picture and stores it at MealPhotos/{mealId} itself, as before.

const IMAGE_MODEL = "@cf/black-forest-labs/flux-1-schnell";
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

    let mealId = "";
    try { mealId = String((await request.json()).mealId || ""); } catch { /* handled below */ }
    if (!/^[A-Za-z0-9_-]{1,64}$/.test(mealId)) return json({ error: "bad_request" }, 400, cors);

    if (env.PER_VISITOR) {
      const visitor = request.headers.get("CF-Connecting-IP") || "unknown";
      const { success } = await env.PER_VISITOR.limit({ key: visitor });
      if (!success) return json({ error: "busy" }, 429, cors);
    }

    const db = env.FIREBASE_URL.replace(/\/$/, "");
    const meal = await readJson(`${db}/MealCatalog/${mealId}.json`);
    if (!meal || meal.isArchived) return json({ error: "unknown_meal" }, 404, cors);
    if (await readJson(`${db}/MealPhotos/${mealId}.json?shallow=true`)) return json({ error: "exists" }, 409, cors);

    try {
      const dish = await englishDish(env, db, mealId, meal);
      const result = await env.AI.run(IMAGE_MODEL, { prompt: prompt(dish), steps: 4 });
      if (!result?.image) return json({ error: "no_image" }, 502, cors);
      return json({ image: `data:image/jpeg;base64,${result.image}`, prompt: prompt(dish) }, 200, cors);
    } catch (e) {
      // On the free plan Workers AI stops for the day once the allowance is used (error 4006).
      const text = String(e?.message || e);
      if (/4006|allocation|neurons|quota/i.test(text)) return json({ error: "daily_limit" }, 429, cors);
      return json({ error: "model_failed", detail: text.slice(0, 200) }, 502, cors);
    }
  }
};

/** The dish in English, which the image model understands best: the meal's own English twin when the
 *  seed has one (n12 ↔ en_n12), its name when it is English, otherwise a translation of name and ingredients. */
export async function englishDish(env, db, mealId, meal) {
  const lang = meal.language || "ar";
  const ingredients = ingredientWords(meal);
  if (lang === "en") return { name: meal.name, ingredients };
  const twin = mealId.match(/^(?:de_)?(n\d+)$/);
  if (twin) {
    const en = await readJson(`${db}/MealCatalog/en_${twin[1]}.json`);
    if (en?.name) return { name: en.name, ingredients: ingredientWords(en) };
  }
  try {
    const source = lang === "de" ? "german" : "arabic";
    const text = [meal.name, ...(meal.ingredients || []).slice(0, 8)].join("; ");
    const out = await env.AI.run(TRANSLATE_MODEL, { text, source_lang: source, target_lang: "english" });
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

export function prompt({ name, ingredients }) {
  return `A realistic, appetising food photograph of "${String(name).trim()}"`
    + (ingredients?.length ? ` (made with ${ingredients.join(", ")})` : "")
    + ". One serving, plated as it is usually served, seen from slightly above, soft natural daylight, "
    + "plain light background, square framing with the dish in the centre. "
    + "No text, no labels, no logos, no hands, no people.";
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
