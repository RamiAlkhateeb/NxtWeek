# Meal photo worker

A Cloudflare Worker that draws the shared AI meal photos with Workers AI (free daily allowance), so nobody needs a
Gemini key. It runs at `https://makdous-photos.rami13195.workers.dev`.

`POST /photo` with `{ "mealId": "en_n12", "user": "<account key>" }` returns
`{ "image": "data:image/jpeg;base64,…", "used": 3, "limit": 10 }`. The app shrinks it
to 480 px and stores it at `MealPhotos/{mealId}`.

- Only meals in `MealCatalog` that are not archived and have no shared photo yet are drawn; anything else is refused.
- Requests are only accepted from the app's own sites (production, Firebase previews, localhost), for an account that
  exists under `users/`.
- Each account may have 10 photos drawn per week, Monday to Sunday in UTC (`WEEKLY_LIMIT` in `wrangler.toml`; keep
  `MealPhotoService.WeeklyLimit` in the app the same). The count is kept at `PhotoQuota/{key}/{monday}`; only pictures
  actually drawn count. The 11th answers 429 `weekly_limit` with the date it resets.
- Arabic and German meals are drawn as English dishes, because the image model only understands English:
  - a seeded dish that also exists in English (`n12`, `de_n12` → `en_n12`) takes the English meal's photo when it has
    one (nothing is drawn or counted), otherwise it is drawn from the English meal and the reply names it (`twin`) so
    the app stores the photo for both;
  - the Arabic classics `m1`–`m49` use a curated English name and description (`src/dishes.js`);
  - any other meal asks `@cf/meta/llama-3.1-8b-instruct` for its usual English name and how it looks, falling back to
    `@cf/meta/m2m100-1.2b`.
- `"replace": true` redraws a meal that already has a photo ("New photo" in the app).
- `"ownerCode"` lifts the weekly limit for the app's owner: it must equal the `OWNER_CODE` secret (at least 12
  characters, compared in constant time). Without the secret set, no code is accepted. Set it in the Cloudflare
  dashboard (Workers & Pages → makdous-photos → Settings → Variables and Secrets, type Secret), then enter the same code
  in the app under Settings → Advanced → Meal photos → Owner code.
- The picture comes from `@cf/black-forest-labs/flux-1-schnell`.
- At most 30 pictures a minute per visitor. When the free daily allowance is used up, it answers 429
  `daily_limit` until the next day; on the Workers Free plan nothing is ever charged.

`POST /steps` with `{ "mealId": "…", "user": "<account key>", "replace": false }` returns
`{ "steps": [{ "text": "…", "minutes": 10 }, …] }`: 2–12 cooking steps in the meal's own language, written by
`@cf/meta/llama-3.3-70b-instruct-fp8-fast` (falling back to the 8B model) from its name and ingredients. The app saves
them at `MealCatalog/{id}/steps`. A meal that already has steps answers 409 `exists` unless `replace` is set. Same
origin, account and per-visitor checks as `/photo`; steps do not count towards the weekly photo limit.

## Deploy

`.github/workflows/photo-worker.yml` tests and deploys it on every push to `main` that touches this folder, and can be
run by hand. It needs two repository secrets:

- `CLOUDFLARE_API_TOKEN`: a Cloudflare API token made from the **Edit Cloudflare Workers** template, with
  **Workers AI: Read** added (dash.cloudflare.com → My Profile → API Tokens).
- `CLOUDFLARE_ACCOUNT_ID`: the account that owns `rami13195.workers.dev`.

## Test

```bash
node --test photo-worker/test/*.test.js
```
