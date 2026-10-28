using System.Text;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

/// <summary>One ingredient as the AI returns it, per serving.</summary>
public sealed class SuggestedIngredient
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double Quantity { get; set; }
    public string Unit { get; set; } = "";
    public string Aisle { get; set; } = "";
}

/// <summary>Ingredients and nutrition per serving for one dish, as the AI returns them.</summary>
public sealed class SuggestedMealDetails
{
    public List<SuggestedIngredient> Ingredients { get; set; } = new();
    public int Servings { get; set; }
    public double Kcal { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public double Fibre { get; set; }
}

/// <summary>
/// Checks an AI reply and copies it onto a meal. Only what the meal is missing is filled, and the
/// nutrition is always marked as an estimate.
/// </summary>
public static class MealDetailsMapper
{
    public const double MinKcal = 50;
    public const double MaxKcal = 2500;
    public const double MacroTolerance = 0.25;
    private static readonly string[] Units = [IngredientUnit.Grams, IngredientUnit.Millilitres, IngredientUnit.Pieces];
    private static readonly string[] Aisles = ["produce", "meat", "dairy", "pantry", "bakery", "other"];

    /// <summary>Throws <see cref="InvalidOperationException"/> for a reply that cannot be right, so the next model is tried.</summary>
    public static SuggestedMealDetails Validate(SuggestedMealDetails d)
    {
        if (d.Kcal is < MinKcal or > MaxKcal)
            throw new InvalidOperationException($"Implausible calories: {d.Kcal}.");
        if (d.Protein < 0 || d.Carbs < 0 || d.Fat < 0 || d.Fibre < 0)
            throw new InvalidOperationException("Negative nutrition values.");
        if (Math.Abs(MacroCheck.MacroKcal(d.Protein, d.Carbs, d.Fat) - d.Kcal) / d.Kcal > MacroTolerance)
            throw new InvalidOperationException("Macros do not add up to the calories.");
        if (!Lines(d).Any())
            throw new InvalidOperationException("No usable ingredients.");
        return d;
    }

    public static List<IngredientLine> Lines(SuggestedMealDetails d) => d.Ingredients
        .Where(i => !string.IsNullOrWhiteSpace(i.Name) && i.Quantity > 0)
        .Select(i => new IngredientLine
        {
            Id = string.IsNullOrWhiteSpace(i.Id) ? Slug(i.Name) : Slug(i.Id),
            Name = i.Name.Trim(),
            QtyPerServing = Math.Round(i.Quantity, 2),
            Unit = Units.Contains(i.Unit?.Trim().ToLowerInvariant()) ? i.Unit!.Trim().ToLowerInvariant() : IngredientUnit.Grams,
            Aisle = Aisles.Contains(i.Aisle?.Trim().ToLowerInvariant()) ? i.Aisle!.Trim().ToLowerInvariant() : "other"
        })
        .DistinctBy(l => (l.Id, l.Unit))
        .ToList();

    /// <summary>Fills what the meal lacks. Returns true when anything changed.</summary>
    public static bool Apply(MealCatalogItem meal, SuggestedMealDetails d)
    {
        var changed = false;
        if (meal.IngredientLines.Count == 0)
        {
            meal.IngredientLines = Lines(d);
            changed |= meal.IngredientLines.Count > 0;
            if (meal.Ingredients.Count == 0) meal.Ingredients = meal.IngredientLines.Select(l => l.Name).ToList();
            if (d.Servings is > 0 and <= 20) meal.ServingsYield = d.Servings;
        }
        if (meal.Nutrition is null)
        {
            meal.Nutrition = new NutritionInfo
            {
                Kcal = Math.Round(d.Kcal / 10) * 10,
                Protein = Math.Round(d.Protein),
                Carbs = Math.Round(d.Carbs),
                Fat = Math.Round(d.Fat),
                Fibre = Math.Round(d.Fibre),
                Source = NutritionSource.Estimate
            };
            changed = true;
        }
        return changed;
    }

    /// <summary>True when the meal still lacks measured ingredients or nutrition.</summary>
    public static bool NeedsDetails(MealCatalogItem meal) => meal.Nutrition is null || meal.IngredientLines.Count == 0;

    // "Chicken breast" / "chicken-breast" → "chickenBreast", so lines from different meals merge on the shopping list.
    private static string Slug(string value)
    {
        var sb = new StringBuilder();
        var upper = false;
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(sb.Length == 0 ? char.ToLowerInvariant(ch) : upper ? char.ToUpperInvariant(ch) : ch); upper = false; }
            else upper = sb.Length > 0;
        }
        return sb.Length == 0 ? value.Trim() : sb.ToString();
    }
}
