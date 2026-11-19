# Makdous (مكدوس)

A weekly family meal planner, shipped as a **Blazor WebAssembly PWA**. Formerly
"NxtWeek". Arabic-first and RTL-capable, also available in English and German.
There is no mascot; do not add one.

## Layout

| Project | What lives there |
|---|---|
| `MealPlanner.Shared` | **All** pages, components, models and service interfaces. A Razor class library — this is where nearly every change goes. |
| `MealPlanner.Tests` | xUnit tests for the pure logic in `MealPlanner.Shared/Nutrition` (targets, day totals, slots, stamps and check-in, weight trend, weekly summary, suggestions, shopping amounts). |
| `MealPlanner.Web` | The WASM host: `Program.cs` (DI), `App.razor`, `MainLayout`, `wwwroot` (CSS, icons, illustrations, service worker), and the browser-specific service implementations. |

```bash
dotnet build MealPlanner.slnx
dotnet run --project MealPlanner.Web      # http://localhost:5186
dotnet test MealPlanner.Tests
```

There is no npm and no bundler. Nutrition maths (targets, totals, and later suggestions and
shopping quantities) lives in `MealPlanner.Shared/Nutrition` as pure static code with tests. Bootstrap is vendored under
`wwwroot/lib` but the app is styled almost entirely by
`MealPlanner.Web/wwwroot/css/app.css`.

## Translation — the one rule that matters

Every user-facing string goes through `ILocalizationService`, with an entry for
**all three** languages (`ar`, `en`, `de`) in the `Translations` dictionary in
[`LocalizationService.cs`](MealPlanner.Shared/Services/LocalizationService.cs):

```razor
<h1>@Loc["week_title"]</h1>
<p>@Loc.T("meals_added_toast", meal.Name, dayLabel)</p>
```

**Never write an inline language ternary** (`Loc.IsArabic ? "..." : "..."`).
The app was full of them and every one silently fell back to Arabic the moment a
third language existed. `Loc.IsArabic` remains only for genuine RTL behaviour —
prefer `Loc.IsRtl` or `Loc.Direction` for that.

Sentences with values use positional `{0}` placeholders and `Loc.T(key, args)`,
never string concatenation: German word order is not English word order.

Dates go through the service too — `Loc.FormatDayDate`, `Loc.FormatMonthYear`,
`Loc.GetDayName`, `Loc.GetShortDayName`. Do not add another month-name array to
a page.

CSS uses logical alignment (`text-align: start`), never `right`.

## Data

Firebase Realtime Database is the **source of truth**, reached over its REST API
with an unauthenticated `HttpClient`. Every service reaches it through `FirebaseRest` (`Key` escapes a path key; `Get`,
`Put`, `Patch`, `Delete` throw on failure, `Try…` return false or null; multi-path `Patch` on a parent or `""` for changes
that must happen together; `GetWithETagAsync` / `TryPutIfMatchAsync` for a write that must not overwrite a change made
meanwhile). Do not build database URLs or call `HttpClient` for the database anywhere else. The offline cache (below) is a convenience only — never treat
it as authoritative.

Identity keys in `localStorage` (do **not** rename these; they are live user data):

```text
nxtweek.guestId        # "guest_<guid>" or the chosen username
nxtweek.activeUserKey
nxtweek.language       # "ar" | "en" | "de"
nxtweek.theme
```

| Data | Path |
|---|---|
| Meal catalog (shared by all users) | `MealCatalog/{id}` |
| Weekly plan | `users/{key}/weeklyPlan/{yyyy-MM-dd}` |
| Favorites | `users/{key}/favoriteMealIds` |
| Shopping list (stores with items; a store's `logoDomain` and `emoji` come from the AI) | `users/{key}/shopping` |
| Which store an item goes to, by ingredient key or name | `users/{key}/shoppingRoutes/{key}` |
| Plan items the user removed from the list, with the amount the plan needed then | `users/{key}/shoppingDismissed/{ingredientKey}` |
| Day slots (Meal 1, afternoon snack, dinner, evening snack) and glasses of water (`water`, personal, never mirrored) | `users/{key}/days/{yyyy-MM-dd}` (old `weeklyPlan` entries read as Meal 1) |
| Meal times, which meals are on, track carbs and fat, Ramadan mode (not health data, guests too) | `users/{key}/mealPrefs` |
| Body data, goal and target overrides (health data, signed-in only) | `users/{key}/nutritionProfile` |
| Weight and waist (signed-in only, never localStorage) | `users/{key}/body/{yyyy-MM-dd}` |
| Weekly progress photo, JPEG data URL (signed-in only, never localStorage, never shared) | `users/{key}/progressPhotos/{monday}` |
| Share daily calories and protein with friends (off by default) | `users/{key}/shareNutrition` |
| AI meal photos, shared by all users (480 px JPEG data URL) | `MealPhotos/{mealId}` |
| Recipe steps (text and optional minutes), shared with the meal | `MealCatalog/{id}/steps` |
| Photos drawn per account per week (Monday, UTC), written by the photo worker | `PhotoQuota/{key}/{yyyy-MM-dd}` |
| Sync with one friend: partner and whose list is shared (both sides) | `users/{key}/sync` |
| Waiting sync request (with the person asked) / the one I sent | `users/{key}/syncRequest`, `users/{key}/syncOutgoing` |
| Protein-challenge count, only while sharing nutrition | `users/{key}/shareStats` |
| Week shared from the Sunday review: Monday, days logged, most eaten meal (never calories or weight) | `users/{key}/sharedReview` |

Makdous is about meals only: there are no training or rest days, and one daily target. The afternoon snack is
stored under its first name, `PostWorkout`. Fields from before 3.1 (`days/*/type`, and `mealTimes`, `offSlots`,
`trainingDays`, `overrides/rest` in `nutritionProfile`) are still read but never written; `INutritionStore.GetMealPrefsAsync`
builds `mealPrefs` from them once. A slot entry is planned, eaten or skipped; skipped counts nowhere.

Three bottom tabs: Week, Meals, Settings. Friends, Shopping and Progress open from icons in the Week header.
A new user starts at `/setup` (goal, about you, meal times, targets; `/welcome` is the same page). Meal times
save for everyone; goal and body data are saved only for a signed-in user, so a guest who declines to sign in
continues with the default targets. Settings is grouped as You, App and Account.

The Week page shows calories, protein and water as three smartwatch-style rings (`TargetRing` in `TargetsPanel`, arcs
from `DayStatus.RingArcs`: eaten solid, planned faint, full and in the accent colour when over) with what is left. Water
counts glasses against a goal of 8 (`DayStatus.WaterGoal`, up to `WaterMax`); tapping the water ring adds a glass and holding it
removes one (`TargetRing.OnTap` / `OnLongPress`, `nxtweek.longPress`, `IDayPlanService.SetWaterAsync`), each with Undo; past the goal it shows "Goal reached", never "over" (`OverIsGood`). Without body data
(offline) only the water ring shows. Under the rings, today shows the next meal as a big card with one-tap
"Mark as eaten" (`DayStatus.NextMeal`). Each day in the strip has a mini ring with the
share of the calorie target eaten (`DayStatus.EatenShare`), green with a tick when the day is logged. carbs and fat appear under Details only when
`mealPrefs.trackMacros` is on. An empty slot is one "Add a meal" target that opens `AddMealSheet` (search,
suggestions with a reason, Rediscover, Browse all meals, Quick add). The day strip swipes a whole week at a time from 8 weeks back to next week (one `GetDaysAsync` for the range;
older weeks are in Month view). There is no week-range row: the date title opens Month view, and on any other day a
"Today" chip next to it jumps back. The slot sheet puts Mark as eaten / Change meal first, nutrition as one line that opens,
and has no leftover switch any more (the `Leftover` field is still read). Every bottom sheet closes with a swipe down
(`nxtweek.js`). Slot cards have no tick button: once today's
planned meal is past its time the card asks "Did you eat this?" (`DayStatus.NeedsCheckIn`); No marks it skipped.
A meal is moved or copied to another slot or day through `IDayPlanService.MoveEntryAsync` / `CopyEntryAsync`
(rules in `PlanMoves`: a move onto a taken slot swaps, a copy replaces). The assistant (`/ai-plan`) only has the
two on-device asks; there is no free-text Gemini chat.

Meals: search, and the meal types (All, Meat, Chicken, Fish & seafood, Vegetarian) as chips under it, one tap each; the
filter sheet only holds the language. The list is in one order (`MealFiltering.Recommended`: favourites, then most eaten,
then name); there is no sort or nutrition filter. "Your top meals" shows the five most eaten meals of the last 12 weeks (`MealFiltering.TopMeals`).
The details call also fills a missing cooking time. A meal's page has an Edit button (name and type); in its sheet a meal the
user added (`CreatedBy`) can be deleted and a shared built-in meal hidden, both after a confirmation and with Undo. Both
only set `IsArchived`, so days that already use the meal keep showing it. The Week slot sheet links to the meal's page
("Edit meal"). After a rename the sheet offers to recalculate: `MealDetailsMapper.ForgetForNewName`
clears the ingredient lists, cooking time and an estimated nutrition on a fresh copy (typed nutrition stays), then
`IMealDetailsService.FillMissingAsync` fills them for the new name; a failure changes nothing.

Progress is one page: weight and waist with the weekly check, the weekly summary, badges (`Badges`, pure: first full
week, protein 7 days in a row, 10 different meals, first batch cook, shared a week; computed from eaten meals only, so a
missed day never takes one away) and the private weekly photo.

Come-back features, all without notifications, with their rules in `ComeBack` and `PlanMoves.CopyWeek` (pure, tested):
the stamp line on Week counts logged days (`DayStatus.Stamps`; a missed day never resets anything); the Sunday review
(`/review?week=`) is announced on Week from Sunday 18:00 through Monday until opened (`mealPrefs.reviewSeen`), and its
"Plan next week" copies meals and servings into the empty slots of the following week from today on; "Copy this week" shows
on an empty next week; `CelebrationOverlay` marks day complete and protein reached (no animation under reduced motion).
Ramadan mode (`mealPrefs.ramadan`) renames Meal 1, Dinner and the evening snack to suhoor, iftar and late snack with
their own `ramadanTimes`: show slot names with `Loc[prefs.NameKey(slot)]` and times with `prefs.TimeOf(slot)`, never
`"nut_slot_" + slot` directly.

Shopping (`/shopping`) is shopping by servings: the meals still to cook from today to Sunday (not eaten, skipped or
leftover) as small chips in two scrolling rows (a chip opens servings and meal prep), and one list in shop order with quick add
and bought items struck through at the end of their own store, that follows them. With more than one store the list is grouped by store; moving an item
to a store remembers it for next time (`users/{owner}/shoppingRoutes/{StoreRoute.Key}`), and plan items without a
remembered store go to the first store made (`ShoppingStores`, pure and tested). Moving an item and deleting a store
(its items go to the default store) are single multi-path writes, so no reader ever sees an item in two stores. Items are
read and written through `IShoppingListService`, stores through `IShoppingStoreService`, and whose list it is through
`IShoppingListOwner`. Each store shows a picture (`StoreThumb`): Gemini with the user's key (else the worker's `/store`) names the chain's web
domain and an emoji (`IStoreLogoService`, cleaned by `StoreLogo.Clean`), the logo is the site's icon from Google's
favicon service, else the emoji, else the first letter; asked once per store (`logoChecked`), again after a rename.
Any item can be deleted. A plan item that is deleted is remembered in `shoppingDismissed` and stays away until the plan
needs more of it (`ShoppingAggregator.Sync` with `dismissed`, `DismissFor`); "Clear list" empties every store and dismisses
the plan items the same way, in one write. Rows swipe (`nxtweek.swipeRows`): towards the reading end ticks an item,
towards the start removes it.
The page runs one change at a time and applies a live update that arrives meanwhile right after it. The page brings the list in
line with the plan on every visit and after each change (`ShoppingAggregator.Sync`), touching only items it made itself
(`IngredientKey`) that are not bought. Bought items stay in their store, struck through; "Clear bought" removes only typed items
and ones the plan no longer needs. Old accounts with several stores see all their items together.

Friends: each friend's week shows their meals (tap one to add it to your favourites) and, only when they share, calories
and protein. The protein challenge shows when you and a friend both share; each side publishes just its protein-day count
(`FriendShare.ProteinDays`). Sync (`ISyncService`) links one friend at a time: the one asked accepts after a confirmation, and
their meals from today on become the requester's (`SyncMerge.Adopt`). From then on `DayPlanService` mirrors every change to a
catalog meal on a day from today on into the partner's day (`SyncMerge.Mirror`). Servings, eaten, skipped, meal prep,
leftovers and quick adds stay personal, and an answered entry is never changed. Both use the requester's shopping list
(`IShoppingListService.GetListOwnerAsync`), and its amounts count both people's servings. Stopping gives the other side a copy
of the list. Live updates use `LiveWatch` (Firebase REST streaming on one narrow path, plus a reload when the tab is shown
again): Week watches this week's days, Shopping watches the shared list and both plans.

The built-in meals are seeded by `ICatalogSeeder`; "Surprise my week" picks with `SurprisePicks` (pure, tested).

Write day slots only through `IDayPlanService` (or `IMealAssignService`, which picks a slot):
it saves the whole day, so a legacy `weeklyPlan` day becomes the new shape on its first edit.
Direct `weeklyPlan` writes are invisible once a day exists under `days`; nothing writes `weeklyPlan` any more.

Another user's data is read narrowly: `GetProfileAsync` does a `?shallow=true` read (Firebase answers every
child with `true`, so it only tells which fields exist) and then reads each profile field on its own, so `days`, `body`, `nutritionProfile` and `shopping` never travel with a profile, and people are
discovered from the `usernames` node, never by reading `users`. Friends see your meals; calories and
protein only when `users/{key}/shareNutrition` is on; weight never. `CreateProfileAsync` never writes an
empty username, display name or account link. E-mail sign-in opens `users/{sanitized e-mail}` when it
exists; otherwise the guest's key is kept.

> Running the app locally talks to the **production** database. Seeding in
> particular writes new catalog rows to it. Keep that in mind before `dotnet run`.

## Meal details

When a meal is added (Meals "New meal"), `IMealDetailsService` asks for ingredient lines (amount per serving, unit,
aisle, English camelCase `id` so shopping merges) and nutrition per serving: Gemini with the user's own key, otherwise,
or when Gemini fails, the photo worker's free `/details` (`IWorkerAi`; the choice is `AiFallback.RunAsync`, pure and
tested). The key is optional (Settings says so under it). `MealDetailsMapper` (pure, tested) rejects implausible replies
from either and only fills what the meal lacks, always as an estimate. A failure never loses the meal. "Estimate from
ingredients" uses the same call (`AiNutritionEstimator`) and falls back to the small `IngredientEstimate` table.

All Gemini calls go through Nxt.UI's `GeminiClient`: it tries the model chosen in Settings (saved as soon as it is
picked), then up to three chat models the key can reach, and reports a switch in `LastFallback`. Failures are
`GeminiApiException` with Google's own reason; show them with `NxtText.AiError(ex)`, never `ex.Message`.

## Undo and feedback

Changes that are easy to regret offer Undo for six seconds in the bar above the tabs (`UndoService` + `UndoBar` in the
layout, a Makdous component because Nxt.UI's toast has no action): removing a meal, moving or copying it, deleting or hiding a meal from the library, "No" on the
check-in, adding or removing a glass of water, removing a shopping item and Clear list. The offer replaces the usual toast. Marking a meal eaten, ticking an
item and swiping one away give a short vibration (`nxtweek.buzz`) where the phone supports it.

## Recipe steps and cooking mode

A meal's page shows its steps (`MealCatalogItem.Steps`) with "Start cooking" (`/meal/{id}/cook`: one big step at a time,
the screen kept on with `nxtweek.keepAwake`, and a timer for steps with minutes that beeps and vibrates). Steps are
typed one per line (`RecipeSteps.Parse`, pure and tested: numbering dropped, "10 min" / "١٠ دقائق" / "10 Minuten"
becomes the timer) or fetched for free from the photo worker's `/steps` (`IRecipeService`). The worker's `/details` and
`/store` are text-only and save nothing; all three share Cloudflare's daily allowance, not the weekly photo limit.

## Sharing the shopping list

The share button on Shopping sends the open items as plain text (`ShoppingText.Build`: grouped by store, amounts
included) through the phone's share sheet (`nxtweek.shareText`), or copies it where there is none.

## Offline

Every database request goes through `OfflineHandler` (registered in `Program.cs` as the `HttpClient`'s handler). Reads
are remembered in `localStorage` (`nxtweek.offline.get:{path}{query}`) and answered from there when the network fails,
also from a remembered parent or wider key range; writes that fail for lack of network wait in
`nxtweek.offline.outbox` and are sent in order before the next write, when the browser is back online, every 20 s and
on the next start. Pending writes are laid over cached reads (`OfflineMerge`, pure and tested), so the app shows its own
offline edits. `OfflineBanner` says when the app is offline and how much is waiting. Health data and photos
(`body`, `nutritionProfile`, `progressPhotos`, `mealPhotos`, `MealPhotos`, `PhotoQuota`) are never kept or queued
(`OfflineMerge.MayKeep`); the Week page hides the targets card when the body data cannot be read. The cache is a
convenience, never the source of truth.

## Meal pictures

Show a meal's picture with `<MealPhoto Meal="meal" />`, never a bare `<img>`. The order is the user's own private
photo (`IMealImageService`, `users/{key}/mealPhotos`), then the shared AI photo, then the illustration.

AI photos (`IMealPhotoService`) are shared by everyone at `MealPhotos/{mealId}`: one 480 px square JPEG data URL,
kept out of `MealCatalog` so the catalog download stays small. A shallow read says which meals have one; each photo is
fetched only when shown. They are drawn for free, without any user key, by the photo worker in `photo-worker/`
(Cloudflare Workers AI, Flux; deployed by `.github/workflows/photo-worker.yml`, tests with
`node --test photo-worker/test/*.test.js`): in the background when a meal is added, from "Make a photo" on the meal's
page, and for every meal without one from Settings → Advanced → Meal photos (`MealPhotosBulk`). An Arabic or German seeded dish shows
its English twin's photo (`MealTwins.EnglishTwinId`: `n12`/`de_n12` → `en_n12`), and the worker draws from the English
meal; the Arabic classics have curated English descriptions in `photo-worker/src/dishes.js` (add one when you seed an
Arabic meal). "New photo" sends `replace`. The worker only draws
catalog meals without a photo (unless replacing), for an existing account (`users/{key}`), from the app's own sites, with 10 photos per
account per week (`WEEKLY_LIMIT`, mirrored in `MealPhotoService.WeeklyLimit`; counted at `PhotoQuota`; the owner's code,
the worker's `OWNER_CODE` secret entered under Meal photos and kept in `nxtweek.photoOwnerCode`, lifts it), 30 a minute per
visitor and Cloudflare's daily allowance; its errors become
`MealPhotoException` and are shown with `MealPhotoException.MessageKey`. The illustration stays when no photo can be made.

`IMealImageService` resolves a flat SVG illustration from
`wwwroot/img/meals/` for every meal — user-supplied `PhotoUrl` first, then an
archetype `Tag`, then a name keyword (matched in all three languages), then the
`MealType`. Seeded meals carry a `Tags` entry so they resolve by tag.

## Look and feel

The 2.0 redesign (see `docs/REDESIGN.md`) lives in `MealPlanner.Web/wwwroot/css/mk.css` (`mk-*` classes, loaded after `app.css`); new UI should use those
classes. `app.css` only keeps what the remaining legacy pages (recover, install) still use.

Originally styled to match the sibling `../Syrian-Radio` *player* (its light theme, not its
dark Ninar page): light grey page (`#f7f7f9`), white cards with ~28px radius,
near-black text (`#1c1c1e`), pink-red accent (`#ff2d55`), black round/pill primary
buttons, grey pill chips. Colours live in the `:root` tokens at the top of
`app.css`; new UI should use the tokens rather than hard-coded colours.

## Conventions

- Reuse the shared `Nxt*` components from the `Nxt.UI` submodule (`lib/Nxt.UI`,
  shared with NxtTask and Syrian Radio): `NxtButton`, `NxtCard`, `NxtBottomSheet`,
  `NxtEmptyState`, `NxtPageHeader`, `NxtTextInput`, `NxtChip`, `NxtSegmented`,
  `NxtIconButton`, plus `ToastService`, `AiChat` and `NxtSettingsPage`, rather than
  hand-rolling markup. Colours are `--nxt-*` tokens (light and dark) in
  `_content/Nxt.UI/nxt.css`; the `--color-*` names in `app.css` alias them.
  Change shared UI in the Nxt.UI repo, then bump the submodule in all three apps.
- Pages that read `Loc` subscribe to `Loc.OnLanguageChanged` and unsubscribe in
  `Dispose`.
- Bump `wwwroot/version.json` for a release; the service worker keys its cache
  off the generated asset manifest.
