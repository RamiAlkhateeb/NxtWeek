using System.Globalization;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

public sealed record PlannedEntry(DateOnly Date, MealSlot Slot, SlotEntry Entry, MealCatalogItem Meal);

/// <param name="Key">Stable merge key: "rice|g" for measured lines, "txt:onion" for a free-text ingredient.</param>
/// <param name="Quantity">Grams, millilitres or pieces; 1 for an unmeasured ingredient.</param>
/// <param name="Unit">"g", "ml", "pc", or null when unmeasured.</param>
public sealed record MergedIngredient(string Key, string Name, double Quantity, string? Unit, string Aisle, int UsedIn, string FirstMeal);

public sealed record ShoppingSync(
    IReadOnlyList<MergedIngredient> Add,
    IReadOnlyList<(ShoppingItem Item, MergedIngredient Line)> Update,
    IReadOnlyList<ShoppingItem> Remove)
{
    public bool IsEmpty => Add.Count == 0 && Update.Count == 0 && Remove.Count == 0;
}

/// <summary>
/// Pure shopping maths: what still has to be cooked from today to Sunday, its ingredients merged
/// with amounts (per serving × servings × meal-prep batches), and how to bring a shopping list in line.
/// </summary>
public static class ShoppingAggregator
{
    /// <summary>Planned meals still to cook: today to Sunday, not eaten yet and not leftovers.</summary>
    public static List<PlannedEntry> PlannedEntries(IReadOnlyDictionary<DateOnly, PlanDay> days, IReadOnlyDictionary<string, MealCatalogItem> catalog, DateOnly today, DateOnly sunday, NutritionProfile profile)
    {
        var result = new List<PlannedEntry>();
        for (var date = today; date <= sunday; date = date.AddDays(1))
        {
            if (!days.TryGetValue(date, out var day)) continue;
            foreach (var slot in DayMath.SlotsFor(DayMath.TypeOf(day, date, profile), profile.MealTimes))
            {
                if (!day.Slots.TryGetValue(slot, out var entry) || entry.Eaten || entry.Leftover) continue;
                if (entry.MealId is null || !catalog.TryGetValue(entry.MealId, out var meal)) continue;
                result.Add(new PlannedEntry(date, slot, entry, meal));
            }
        }
        return result;
    }

    public static List<MergedIngredient> Merge(IEnumerable<PlannedEntry> entries)
    {
        var merged = new Dictionary<string, (string Name, double Qty, string? Unit, string Aisle, int UsedIn, string FirstMeal)>();
        var order = new List<string>();
        foreach (var e in entries)
        {
            var factor = e.Entry.Servings * Math.Max(1, e.Entry.PrepBatches);
            var lines = e.Meal.IngredientLines.Count > 0
                ? e.Meal.IngredientLines.Select(l => (Key: $"{l.Id}|{l.Unit}", l.Name, Qty: l.QtyPerServing * factor, Unit: (string?)l.Unit, l.Aisle))
                : e.Meal.Ingredients.Select(i => i.Trim()).Where(i => i.Length > 0)
                    .Select(i => (Key: "txt:" + i.ToLowerInvariant(), Name: i, Qty: 1.0, Unit: (string?)null, Aisle: ""));
            foreach (var (key, name, qty, unit, aisle) in lines.DistinctBy(l => l.Key))
            {
                if (merged.TryGetValue(key, out var m))
                    merged[key] = m with { Qty = unit is null ? 1 : m.Qty + qty, UsedIn = m.UsedIn + 1 };
                else { merged[key] = (name, qty, unit, aisle, 1, e.Meal.Name); order.Add(key); }
            }
        }
        return order.Select(k => { var m = merged[k]; return new MergedIngredient(k, m.Name, m.Qty, m.Unit, m.Aisle, m.UsedIn, m.FirstMeal); }).ToList();
    }

    /// <summary>The whole number stored on a shopping item: pieces round up, grams and millilitres to the nearest unit.</summary>
    public static int StoredQuantity(MergedIngredient line) =>
        line.Unit == IngredientUnit.Pieces ? (int)Math.Ceiling(line.Quantity - 1e-9) : Math.Max(1, (int)Math.Round(line.Quantity, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Brings the list in line with the plan. Only items this created (they carry an ingredient key) and that
    /// are not bought are updated or removed; bought items and items typed by hand are never touched, and a
    /// typed item with the same name stops a duplicate from being added.
    /// </summary>
    public static ShoppingSync Sync(IEnumerable<ShoppingItem> existing, IReadOnlyList<MergedIngredient> lines)
    {
        var items = existing.ToList();
        var auto = items.Where(i => !string.IsNullOrEmpty(i.IngredientKey)).ToList();
        var typedNames = items.Where(i => string.IsNullOrEmpty(i.IngredientKey) && !i.IsBought)
            .Select(i => i.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var add = new List<MergedIngredient>();
        var update = new List<(ShoppingItem, MergedIngredient)>();
        foreach (var line in lines)
        {
            var open = auto.FirstOrDefault(i => i.IngredientKey == line.Key && !i.IsBought);
            if (open is not null)
            {
                if (open.Quantity != StoredQuantity(line) || open.UsedIn != line.UsedIn) update.Add((open, line));
            }
            else if (!auto.Any(i => i.IngredientKey == line.Key) && !typedNames.Contains(line.Name.Trim()))
            {
                add.Add(line);
            }
        }
        var wanted = lines.Select(l => l.Key).ToHashSet();
        var remove = auto.Where(i => !i.IsBought && !wanted.Contains(i.IngredientKey!)).ToList();
        return new ShoppingSync(add, update, remove);
    }

    /// <summary>"1.2 kg", "750 g", "1.5 l", "3" pieces (the unit word is added by the caller). Null when unmeasured.</summary>
    public static (string Amount, string Unit)? Format(double quantity, string? unit, CultureInfo culture)
    {
        switch (unit)
        {
            case IngredientUnit.Grams when quantity >= 1000:
                return ((quantity / 1000).ToString("0.#", culture), "kg");
            case IngredientUnit.Millilitres when quantity >= 1000:
                return ((quantity / 1000).ToString("0.#", culture), "l");
            case IngredientUnit.Grams or IngredientUnit.Millilitres:
                return (Math.Round(quantity).ToString("0", culture), unit);
            case IngredientUnit.Pieces:
                return (Math.Ceiling(quantity - 1e-9).ToString("0", culture), unit);
            default:
                return null;
        }
    }
}
