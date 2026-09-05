# NxtWeek

NxtWeek is an Arabic-first, RTL weekly meal planner for family home cooking. It is currently a Blazor WebAssembly PWA with a warm, mobile-first interface.

## Current experience

- Week view with Monday-based weeks, compact meal cards, suggestions, favorites, editing, moving, and deletion.
- Meals page with search, meal-type filters, favorites, context-aware meal selection, and browse-mode day selection.
- Monthly read-only calendar overlay from the Week page.
- Meal details and ingredients/side dishes.
- Guest Mode starts directly without a name, username, cuisine, or email prompt. A first-time visitor gets an auto-generated current week of random meals.
- Three-tab bottom navigation: Week, Meals, Settings.
- Reusable UI components for buttons, cards, page headers, inputs, filter chips, empty states, and bottom sheets.

## Guest mode and storage

The normal entry point is Guest Mode. No email or password is required. The browser creates a persistent guest ID and keeps identity keys in `localStorage`:

```text
localStorage:nxtweek.guestId        # identity key: "guest_<guid>" or chosen username
localStorage:nxtweek.activeUserKey
localStorage:meals-cache            # read-through display cache only (never source of truth)
```

Actual persistence is a Firebase Realtime Database (the RTDB REST API through an unauthenticated `HttpClient`). Per-user data is keyed by the identity key under `users/{key}/...`:

| Data | Path |
|---|---|
| Weekly plan | `users/{key}/weeklyPlan/{yyyy-MM-dd}` |
| Favorites / selected meals | `users/{key}/favoriteMealIds`, `selectedMealIds` |
| Profile / username | `users/{key}` + `usernames/{username}` reservation |
| Shopping list | `users/{key}/shopping` |

The application-facing interfaces are kept separate from storage implementations (`IUserService`, `IMealCatalogService`, `IMealService`, `IShoppingListService`, ...).

## Data safety

A user node under `users/{key}` is a single RTDB document holding profile fields **plus** sibling subtrees (`weeklyPlan`, `shopping`, ...). Profile writes therefore use `PATCH` (shallow merge) and targeted path writes — never whole-document `PUT` — and choosing/renaming a username **moves the whole user subtree** to the new key, so no plan or shopping data is ever dropped. Each write is checked for success. Guest and first-visit weeks are auto-filled from random catalog meals exactly once (tracked by `UserProfile.FirstWeekAutoFilled`), never overwriting a deliberately cleared week.

## Username identity

A first-time visitor lands directly in Guest Mode with an auto-generated current week; no username is required. The profile icon in the corner shows a red nudge until the visitor chooses a username (from Settings or the Friends page). Choosing a username replaces the guest ID (the data key is renamed to the username, carrying all stored data with it), which also lets friends find them. Friends discovery lists other registered usernames.

## Settings

Settings currently contains:

- Current user profile and username (choose / rename a username).
- Recover a plan by username after cleared browser data / on a new device.
- Add-to-home-screen install guide for iPhone/iPad and Android.
- Shopping list and weekly-plan management from their own tabs.
- Disabled “coming soon” controls for cross-device sync.

The Settings UI talks to service interfaces rather than Firebase directly. Firebase Authentication / email-link cross-device sync is available but not wired to real credentials yet (`firebase-auth-config.js` must be filled in).

## CSV import/export

The import page accepts CSV and matches meal names against the existing catalog:

```csv
Date,Day,Meal
2026-07-20,Monday,Chicken & Rice
```

`Date` should preferably use `yyyy-MM-dd`. `Day` is informational. Unknown meals, invalid dates, and duplicate dates are reported. Export produces the same schema for the current week.

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
| `/settings` | Settings / profile |
| `/settings/install` | Add-to-home-screen guide (Android + iPhone) |
| `/settings/recover` | Recover a plan by username after cleared browser data / new device |
| `/settings/import` | CSV weekly-plan import |
| `/friends` | Friends, friend requests, and discovering other app users |
| `/signin` | Legacy email sign-in page; not used by the normal Guest Mode flow |

## Architecture

```text
MealPlanner.slnx
├── MealPlanner.Shared/   Models, service contracts, Razor pages, and reusable components
└── MealPlanner.Web/      Blazor WebAssembly PWA host and browser implementations
```

The app uses Firebase Realtime Database REST services for user data (weekly plan, profile, favorites, shopping) and the global `MealCatalog`. User data is written with merge-safe `PATCH` operations and username renames move the whole user node, so no plan data is lost across updates. The C# meal list is idempotent seed data: missing meals are added to Firebase without overwriting, duplicating, or deleting existing catalog entries.

`database.rules.json` currently allows unrestricted reads and writes for testing. It is not suitable for production until Firebase Authentication and authenticated database rules are added.

## Requirements

- .NET 10 SDK
- Firebase Realtime Database for household identity features

## Cross-device sync (email link)

Users first choose a unique username in **Settings**, then enter their email to receive a passwordless sign-in link. Opening that link on another browser or phone loads the same username-keyed plan and syncs plan changes live.

Before testing, add the Web App configuration to `MealPlanner.Web/wwwroot/firebase-auth-config.js`, then in Firebase Console enable **Authentication → Sign-in method → Email/Password** and **Email link (passwordless sign-in)**. Add localhost and the deployed app domains in **Authentication → Settings → Authorized domains**.

The database rules are intentionally still public for testing. Do not use this configuration for a production deployment: public rules allow anyone with the database URL to alter usernames, plans, or friendships.

The Firebase database URL is configured in `MealPlanner.Web/Program.cs`. The catalog seed file is `MealPlanner.Shared/wwwroot/seed/meals_seed.json`.

## Run locally

```bash
dotnet run --project MealPlanner.Web
```

Open the localhost URL. New users enter Guest Mode and can plan meals immediately.

## Deployment

The project is configured as a static Blazor WebAssembly PWA and includes Firebase Hosting configuration in `firebase.json`. No deployment workflow is currently included.

## Tech stack

- C# and Razor
- Blazor WebAssembly targeting `net10.0`
- Firebase Realtime Database REST API
- Browser `localStorage` through `IJSRuntime`
- Bootstrap RTL assets and custom CSS
- PWA manifest and service worker
