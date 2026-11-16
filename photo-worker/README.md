# Meal photo worker

A Cloudflare Worker that draws the shared AI meal photos with Workers AI (free daily allowance), so nobody needs a
Gemini key. It runs at `https://makdous-photos.rami13195.workers.dev`.

`POST /photo` with `{ "mealId": "en_n12" }` returns `{ "image": "data:image/jpeg;base64,…" }`. The app shrinks it
to 480 px and stores it at `MealPhotos/{mealId}`.

- Only meals in `MealCatalog` that are not archived and have no shared photo yet are drawn; anything else is refused.
- Requests are only accepted from the app's own sites (production, Firebase previews, localhost).
- Non-English meals are described in English first: the seed's English twin (`n12` → `en_n12`) or a translation
  with `@cf/meta/m2m100-1.2b`. The picture comes from `@cf/black-forest-labs/flux-1-schnell`.
- At most 30 pictures a minute per visitor. When the free daily allowance is used up, it answers 429
  `daily_limit` until the next day; on the Workers Free plan nothing is ever charged.

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
