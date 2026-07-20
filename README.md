# NxtWeek (Makdous) 

A playful, family-friendly weekly meal-planning app for Levantine home cooking —
built for Arabic speakers (RTL), with weekly and monthly meal views, smart
suggestions, favorites, and shared household planning for couples/families.

Named after *makdous* — the beloved stuffed baby-eggplant dish pickled in olive
oil — because good food planning should feel warm and homely, not corporate.

---

## ✨ Features

- **Weekly meal view** — a clean, calendar-style week screen (Monday–Sunday)
  showing each day's planned meal, with quick actions to edit, get a new
  suggestion, or view details. Empty days invite you to add a meal right there.
- **Monthly overview** — a read-only calendar grid to glance at an entire
  month's plan at once.
- **Meal pool / catalog browser** — search and filter the full shared meal
  catalog by cuisine and meal type, see which meals you've already picked into
  your rotation, and assign any meal to any day in the current week.
- **Smart suggestions** — random-from-catalog suggestions filtered by your
  preferred cuisines, weighted toward your favorite meals, avoiding recent
  repeats.
- **Favorites** — mark meals as favorites; they show up more often in
  suggestions and are flagged wherever they appear.
- **Meal details** — ingredients and suggested side dishes for every meal.
- **Simple onboarding** — pick a username (no password), choose your preferred
  cuisines (Syrian, Turkish, Lebanese), and select up to 8 starter meals — the
  app generates your first week automatically.
- **Household linking** — send a link request to another username (e.g. your
  spouse); once accepted, both accounts share one merged weekly plan.
- **Playful, informal UI** — warm coral color palette, bouncy "sticker" card
  style, emoji, and Arabic-first design throughout.

---

## 🏗️ Architecture

```
MealPlanner.sln
├── MealPlanner.Shared/     Razor Class Library — Models, Services, Pages/Components
├── MealPlanner.Web/        Blazor WebAssembly PWA (primary host, runs in-browser)
└── MealPlanner.Maui/       .NET MAUI Blazor Hybrid (planned — Android APK build)
```

- **UI is written once**, in `MealPlanner.Shared`, and hosted two ways: as a
  Blazor WASM PWA (installable via "Add to Home Screen" on iOS/Android browsers)
  and, eventually, as a native Android APK via MAUI Blazor Hybrid — no separate
  UI code needed per platform.
- **No custom backend.** The app talks directly to **Firebase Realtime
  Database** over its plain REST API (`HttpClient` + `.json` endpoints) — no
  Firebase SDK dependency, which keeps the same service code working
  identically in both WASM and MAUI hosts.
- **Local caching**: the Web host caches the current week in browser
  `localStorage` via `IJSRuntime` for instant reloads and light offline
  resilience. (The MAUI host will use SQLite for the same purpose.)
- **State management**: no external MVVM framework — plain Blazor component
  state plus a small set of shared services registered via dependency
  injection.

---

## 🗂️ Data model (Firebase Realtime Database)

```
mealCatalog/{mealId}
  name, cuisine (Syrian | Turkish | Lebanese)
  mealType (Meat | Chicken | Fish | Vegetarian | Vegan)
  ingredients: [...]
  sideDishes: [...]

users/{username}
  householdId
  preferredCuisines: [...]
  selectedMealIds: [...]        // up to 8, picked during onboarding
  pendingLinkRequests: [...]    // incoming household-link requests

households/{householdId}
  memberUsernames: [...]
  weeklyPlan:
    "yyyy-MM-dd": { mealId, isFavorite }
  favoriteMealIds: [...]
```

- The meal catalog is **global and shared** across all users — everyone draws
  from the same pool, filtered by their own cuisine preferences.
- Every user gets a **solo household** automatically at signup, so weekly-plan
  logic never has to special-case "not yet linked" — linking just merges two
  households into one.
- Weeks always start on **Monday**.

---

## 🚀 Getting started

### Prerequisites
- [.NET SDK](https://dotnet.microsoft.com/download) (targeting `net8.0` or later)
- A free [Firebase](https://console.firebase.google.com) project with Realtime
  Database enabled (test-mode rules are fine for a private/family app)

### Setup
```bash
git clone <repo-url>
cd MealPlanner

# Set your Firebase Realtime Database URL in MealPlanner.Web/Program.cs:
#   FirebaseOptions.DatabaseUrl = "https://YOUR-PROJECT-default-rtdb.firebaseio.com"

dotnet run --project MealPlanner.Web
```

Open the printed `localhost` URL in your browser. On first load, the app seeds
the global meal catalog from `MealPlanner.Shared/wwwroot/seed/meals_seed.json`
if Firebase is empty.

### Running on your phone
- **iPhone**: open the deployed URL in Safari → Share → **Add to Home Screen**.
- **Android**: once `MealPlanner.Maui` is built, install the generated `.apk`
  directly (sideloading — no Play Store account needed for personal use).

---

## 📦 Deployment

The Web app is a static Blazor WASM build, deployable for free to **GitHub
Pages** (or Azure Static Web Apps). Build the `wwwroot` output and publish it
to a `gh-pages` branch — no server required.

---

## 🔮 Roadmap

- [ ] Ship `MealPlanner.Maui` Android APK for local/family install
- [ ] Smarter (Gemini-powered) meal suggestions as an alternative to random
- [ ] GitHub Pages deployment workflow
- [ ] App logo / branding (in progress)

---

## 🧰 Tech stack

| Layer | Choice |
|---|---|
| UI framework | Blazor (WebAssembly + planned MAUI Hybrid) |
| Language | C# / Razor |
| Database | Firebase Realtime Database (REST, no SDK) |
| Local cache | Browser `localStorage` via `IJSRuntime` |
| Hosting | GitHub Pages (free, static) |
| Fonts | Cairo (Google Fonts) |

Everything in this stack is free to run at this app's scale — no paid tiers,
no backend server, no App Store fees for personal/family use.
