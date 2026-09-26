---
name: add-translation
description: Add or change a user-facing string in the Makdous app, or add a new UI language. Use whenever you are about to put text on screen in a .razor component, when a string shows up untranslated, or when someone asks for another language.
---

# Adding a translated string

All UI text lives in one dictionary: `Translations` in
`MealPlanner.Shared/Services/LocalizationService.cs`.

## Add a string

1. Add a row with an entry for **every** supported language — `ar`, `en`, `de`.
   A missing language falls back (`requested → en → ar → the key itself`), which
   means a half-filled row ships visibly wrong text, not an error.

   ```csharp
   ["week_remove_from_day"] = new() { ["ar"] = "حذف من اليوم", ["en"] = "Remove from day", ["de"] = "Vom Tag entfernen" },
   ```

2. Use it in the component:

   ```razor
   <button>@Loc["week_remove_from_day"]</button>
   ```

3. Keep the key in the page's existing prefix group (`week_`, `meals_`,
   `shopping_`, `settings_`, `recover_`, `install_`, `carousel_`, `meal_`).

## Never do this

```razor
@(Loc.IsArabic ? "حذف" : "Delete")   @* breaks German — it silently shows Arabic *@
```

`Loc.IsArabic` is for RTL layout decisions only. For text, always use a key.

## Strings with values

German word order differs from English, so interpolate positionally and let the
translator move the placeholder:

```csharp
["meals_added_toast"] = new() { ["ar"] = "تمت إضافة {0} إلى {1}", ["en"] = "Added {0} to {1}", ["de"] = "{0} für {1} eingeplant" },
```

```razor
@Loc.T("meals_added_toast", meal.Name, dayLabel)
```

Comment the row with what each placeholder is.

## Dates

Never format a date in a page. Use `Loc.FormatDayDate(date)`,
`Loc.FormatMonthYear(date)`, `Loc.GetDayName(day)` or `Loc.GetShortDayName(day)`.

## Add a whole language

1. Append to `Languages` in `LocalizationService`: code, native name, flag,
   direction. The Settings switcher and `SupportedLanguageCodes` both read from
   this array, so nothing else needs touching for the switcher.
2. Add the day, short-day, month and short-month arrays, wire them into the
   `Days`/`ShortDays`/`Months`/`ShortMonths` switches and `FormatDayDate`, and
   add a `CultureInfo` to `CultureFor`.
3. Add the new code to **every** row in `Translations`.
4. Seed meals for it — see the `add-meal` skill. Without them the catalog falls
   back to showing all languages, which works but is not what you want.
5. For an RTL language, also check `Direction`, `IsRtl` and that no CSS uses
   `text-align: right` (the stylesheet uses logical `start`).

## Verify

```bash
grep -rn "IsArabic ?" --include=*.razor MealPlanner.Shared MealPlanner.Web
```

Should return nothing. Then run the app, switch to each language in Settings and
walk Week → Meals → meal details → Shopping → Friends → Settings → Install.
