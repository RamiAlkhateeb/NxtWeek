# Sahtein (صحتين)

A weekly family meal planner, shipped as a **Blazor WebAssembly PWA**. Formerly
"NxtWeek" / "مكدوس". Arabic-first and RTL-capable, also available in English and
German. The mascot is **Abu Saleh**, a bald, moustached home cook.

## Layout

| Project | What lives there |
|---|---|
| `MealPlanner.Shared` | **All** pages, components, models and service interfaces. A Razor class library — this is where nearly every change goes. |
| `MealPlanner.Web` | The WASM host: `Program.cs` (DI), `App.razor`, `MainLayout`, `wwwroot` (CSS, icons, illustrations, service worker), and the browser-specific service implementations. |

```bash
dotnet build MealPlanner.slnx
dotnet run --project MealPlanner.Web      # http://localhost:5186
```

There is no npm, no bundler and no test project. Bootstrap is vendored under
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

> Running the app locally talks to the **production** database. Seeding in
> particular writes new catalog rows to it. Keep that in mind before `dotnet run`.

## Meal pictures

No remote photography. `IMealImageService` resolves a flat SVG illustration from
`wwwroot/img/meals/` for every meal — user-supplied `PhotoUrl` first, then an
archetype `Tag`, then a name keyword (matched in all three languages), then the
`MealType`. Seeded meals carry a `Tags` entry so they resolve by tag.

## Mascot

`<AbuSaleh Pose="wave|cook|shrug|cheer|basket" Size="96" />` — one component,
inline SVG, coloured from the theme custom properties so a palette change carries
it along. `wwwroot/logo.svg` is the master for the PWA icons.

## Conventions

- Reuse the `Ui*` components (`UiButton`, `UiCard`, `UiBottomSheet`,
  `UiEmptyState`, `UiPageHeader`, `UiTextInput`, `UiFilterChip`) rather than
  hand-rolling markup.
- Pages that read `Loc` subscribe to `Loc.OnLanguageChanged` and unsubscribe in
  `Dispose`.
- Bump `wwwroot/version.json` for a release; the service worker keys its cache
  off the generated asset manifest.
