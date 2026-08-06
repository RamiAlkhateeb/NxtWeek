# NxtWeek

NxtWeek is a family meal-planning app for Levantine home cooking. It is currently a Blazor WebAssembly PWA with an Arabic-first, RTL interface.

The app lets users plan meals by week, browse the shared meal catalog, view a monthly overview, save favorites, and receive random meal suggestions.

## Current features

- Weekly meal planning, with Monday as the first day of the week.
- Monthly read-only overview of the household plan.
- Meal catalog browsing with cuisine and meal-type filters.
- Meal details, including ingredients and side dishes.
- Random suggestions based on preferred cuisines and favorites; favorites are weighted more heavily and recent meals are avoided where possible.
- Favorites stored for the household and shown on meal cards and details pages.
- Onboarding for an email-based local sign-in, preferred cuisines, and up to eight starter meals.
- Household linking by sending and accepting requests for another email address.
- JSON history import at `/settings/import`.
- Browser local-storage caching of the current week.
- Installable PWA assets and service worker for the WebAssembly app.

## Routes

| Route | Purpose |
|---|---|
| `/` | Current week |
| `/month` | Monthly overview |
| `/meals` | Meal catalog |
| `/list` | Saved/list view |
| `/meal/{mealId}` | Meal details |
| `/edit/{mealId}` | Edit a planned meal |
| `/settings` | Settings and household actions |
| `/signin` | Local email sign-in |
| `/onboarding/username` | Initial account setup |
| `/onboarding/cuisines` | Cuisine preferences |
| `/onboarding/meals` | Starter meal selection |
| `/settings/import` | Import meal history |

## Architecture

```text
MealPlanner.slnx
├── MealPlanner.Shared/   Razor Class Library: models, services, pages, and components
└── MealPlanner.Web/      Blazor WebAssembly PWA host
```

The UI and most application services are in `MealPlanner.Shared`. `MealPlanner.Web` provides the browser host and registers the services.

The app currently has no MAUI or native mobile project. There is also no custom backend: the WebAssembly client calls Firebase Realtime Database directly through REST endpoints using `HttpClient`.

Authentication is currently a lightweight local email sign-in implemented with browser `localStorage`; it is not Firebase Authentication and does not use passwords or tokens.

## Firebase data model

The Firebase Realtime Database uses these main paths:

```text
mealCatalog/{mealId}
  name, cuisine, mealType, ingredients, sideDishes

users/{sanitized-email}
  uid, email, displayName, preferredCuisines, selectedMealIds,
  favoriteMealIds, householdId, pendingLinkRequestUids

households/{householdId}
  memberIds, weeklyPlan, favoriteMealIds
```

Weekly plan entries are stored under date keys in `yyyy-MM-dd` format and contain a meal ID and favorite flag. A solo household is created for a user when needed; accepted household-link requests move the accepting user into the requester's household.

The initial catalog is loaded from `MealPlanner.Shared/wwwroot/seed/meals_seed.json` when the catalog is empty.

## Requirements

- .NET 10 SDK
- A Firebase project with Realtime Database enabled

The Firebase database URL is currently configured in `MealPlanner.Web/Program.cs` in the registered `FirebaseOptions` instance. Database access rules are defined in `database.rules.json`.

## Run locally

```bash
dotnet run --project MealPlanner.Web
```

Open the localhost URL printed by the .NET tooling. On first use, sign in with an email address and complete the onboarding screens.

## Deployment

The project is configured as a static Blazor WebAssembly PWA and includes Firebase Hosting configuration in `firebase.json`. A deployment workflow is not currently included in the repository.

## Tech stack

- C# and Razor
- Blazor WebAssembly targeting `net10.0`
- Firebase Realtime Database REST API
- Browser `localStorage` through `IJSRuntime`
- Bootstrap RTL assets and custom CSS
- PWA manifest and service worker

