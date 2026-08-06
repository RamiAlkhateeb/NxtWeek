# NxtWeek

NxtWeek is an Arabic-first, RTL weekly meal planner for family home cooking. It is currently a Blazor WebAssembly PWA with a warm, mobile-first interface.

## Current experience

- Week view with Monday-based weeks, compact meal cards, suggestions, favorites, editing, moving, and deletion.
- Meals page with search, cuisine/type filters, favorites, context-aware meal selection, and browse-mode day selection.
- Monthly read-only calendar overlay from the Week page.
- Meal details and ingredients/side dishes.
- Onboarding for a display name, preferred cuisines, and starter meals.
- Three-tab bottom navigation: Week, Meals, Settings.
- Reusable UI components for buttons, cards, page headers, inputs, filter chips, empty states, and bottom sheets.

## Guest mode and storage

The normal entry point is Guest Mode. No email or password is required. The browser creates a persistent guest ID in `localStorage` and stores the guest profile, meal catalog, plans, and favorites in the local guest document:

```text
localStorage:nxtweek.guestId
localStorage:nxtweek.guestData.v1
localStorage:meals-cache
```

The application-facing interfaces are kept separate from storage implementations, including `IUserService`, `IMealCatalogService`, `IMealService`, `IHouseholdService`, `IDataImportService`, and `IDataExportService`.

## Household identity

Opening Settings mirrors the local guest profile to Firebase if it does not already exist. Firebase creates the household and its ID; that ID is then copied back to the local profile and shown in Settings.

Joining a household validates the ID in Firebase, adds the current user to `memberIds`, updates the Firebase user profile, and mirrors the ID locally.

Household ID membership is implemented. Automatic cross-device weekly-plan/history/favorite synchronization is not implemented yet; plans and catalog data remain local.

## Settings

Settings currently contains:

- Current user and Firebase household ID with copy action.
- Household ID connection.
- Weekly-plan JSON import and export.
- Clear local data with confirmation.
- Disabled “coming soon” controls for dark/light themes, language selection, feedback, privacy, and open source links.

The Settings UI talks to service interfaces rather than Firebase directly. Firebase Authentication is not implemented yet.

## JSON import/export

The import page accepts a JSON array and matches meal names against the existing catalog:

```json
[
  {
    "Date": "2026-07-20",
    "Day": "Monday",
    "Meal": "Chicken & Rice"
  }
]
```

`Date` should preferably use `yyyy-MM-dd`. `Day` is informational. Unknown meal names are reported and malformed JSON is handled with an error message. Export produces the same schema, pretty-printed, for the current week.

The current importer writes valid matched entries directly. Preview, duplicate-date reporting, merge/replace selection, and full history import are not implemented yet.

## Routes

| Route | Purpose |
|---|---|
| `/` | Current week |
| `/meals` | Meal catalog and selection |
| `/month` | Monthly overview route; month is also available from Week |
| `/meal/{mealId}` | Meal details |
| `/edit/{mealId}` | Legacy edit route still present |
| `/list` | Legacy list route still present |
| `/settings` | Settings |
| `/settings/import` | JSON weekly-plan import |
| `/onboarding/username` | Display-name onboarding |
| `/onboarding/cuisines` | Cuisine preferences |
| `/onboarding/meals` | Starter meal selection |
| `/signin` | Legacy email sign-in page; not used by the normal Guest Mode flow |

## Architecture

```text
MealPlanner.slnx
├── MealPlanner.Shared/   Models, service contracts, Razor pages, and reusable components
└── MealPlanner.Web/      Blazor WebAssembly PWA host and browser implementations
```

The app uses Firebase Realtime Database REST services for household identity and currently retains an existing Firebase service layer for future synchronization. The active user/catalog storage registrations are local guest implementations.

`database.rules.json` currently allows unrestricted reads and writes for testing. It is not suitable for production until Firebase Authentication and authenticated database rules are added.

## Requirements

- .NET 10 SDK
- Firebase Realtime Database for household identity features

The Firebase database URL is configured in `MealPlanner.Web/Program.cs`. The catalog seed file is `MealPlanner.Shared/wwwroot/seed/meals_seed.json`.

## Run locally

```bash
dotnet run --project MealPlanner.Web
```

Open the localhost URL. New users enter Guest Mode and complete the display-name and meal-preference onboarding.

## Deployment

The project is configured as a static Blazor WebAssembly PWA and includes Firebase Hosting configuration in `firebase.json`. No deployment workflow is currently included.

## Tech stack

- C# and Razor
- Blazor WebAssembly targeting `net10.0`
- Firebase Realtime Database REST API
- Browser `localStorage` through `IJSRuntime`
- Bootstrap RTL assets and custom CSS
- PWA manifest and service worker

