---
name: add-meal
description: Add meals to the Sahtein catalog seed, or change how a meal's picture is chosen. Use when asked to add dishes for a language, expand the meal library, or fix a meal showing the wrong illustration.
---

# Adding catalog meals

The seed list is the big `catalogMeals` collection inside `SeedAsync` in
`MealPlanner.Shared/Services/FirebaseMealService.cs`.

## Add a meal

```csharp
new() { Id = "de_m28", Name = "Semmelknödel mit Pilzrahm", MealType = MealType.Vegetarian,
        Language = "de", Tags = new() { "stew" },
        Ingredients = new() { "Semmelknödel", "Champignons", "Sahne", "Petersilie" },
        SideDishes = new() { "Grüner Salat" } },
```

- **Id** follows the language: `m*` for Arabic (historical, no prefix),
  `en_m*`, `de_m*`. Ids must be unique and stable — they are the Firebase keys.
- **Language** must be one of the codes in `LocalizationService.Languages`.
  An omitted language defaults to `"ar"`.
- **Name, Ingredients, SideDishes** are written *in that meal's language*. They
  are data, not UI strings, so they do not go through `ILocalizationService`.
- **Tags** should carry an archetype so the dish gets the right picture.

## Pictures

`MealImageService` picks an SVG from `MealPlanner.Web/wwwroot/img/meals/`:

1. an explicit `PhotoUrl`,
2. a `Tag` matching a file name,
3. a keyword in the meal name (table in `MealImageService`, all languages),
4. the `MealType`.

Available archetypes: `meat`, `chicken`, `fish`, `vegetarian`, `vegan`, `pasta`,
`soup`, `rice`, `bread`, `salad`, `burger`, `pizza`, `stew`, `eggs`, `potato`.

Wrong illustration? Add a `Tag` to the meal (best) or a distinctive stem to the
keyword table. To add a new archetype, drop a 200×200 SVG in that folder using
the existing palette, and add its name to `Archetypes` in the service.

## The seeding gate

`SeedAsync` runs on every Week/Meals page load and calls `SeedCatalogAsync`,
which fetches the whole catalog and `PUT`s only the meals whose id **and**
normalised name are both absent. So:

- adding a meal to the list is enough — it appears on the next load,
- **renaming** a seeded meal in the list creates a *second* row rather than
  editing the first; change it in Firebase, or delete the old id,
- `IsCatalogSeededAsync` is true only when every supported language has at
  least one meal.

## Careful: this writes to production

The app talks to the live Firebase Realtime Database (`MealCatalog/{id}`) with
no local emulator. Simply running the app after editing the seed publishes the
new meals to every user. Say so before running it.
