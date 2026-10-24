# Makdous (مكدوس)

A weekly family meal planner, shipped as a **Blazor WebAssembly PWA**. Formerly
"NxtWeek". Arabic-first and RTL-capable, also available in English and German.
There is no mascot; do not add one.

## Layout

| Project | What lives there |
|---|---|
| `MealPlanner.Shared` | **All** pages, components, models and service interfaces. A Razor class library — this is where nearly every change goes. |
| `MealPlanner.Tests` | xUnit tests for the pure logic in `MealPlanner.Shared/Nutrition` (targets, day totals, slots, weight trend, weekly summary, suggestions, shopping amounts). |
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
with an unauthenticated `HttpClient`. `localStorage:meals-cache` is a read-through
display cache only — never treat it as authoritative.

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
| Shopping list | `users/{key}/shopping` |
| Day slots (Meal 1, post-workout, dinner, snack) | `users/{key}/days/{yyyy-MM-dd}` (old `weeklyPlan` entries read as Meal 1) |
| Nutrition profile and targets | `users/{key}/nutritionProfile` |
| Weight and waist (signed-in only, never localStorage) | `users/{key}/body/{yyyy-MM-dd}` |
| Share daily calories and protein with friends (off by default) | `users/{key}/shareNutrition` |

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

## Meal pictures

No remote photography. `IMealImageService` resolves a flat SVG illustration from
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
