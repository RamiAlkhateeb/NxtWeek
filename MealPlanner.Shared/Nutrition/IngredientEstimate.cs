using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

/// <summary>
/// Rough nutrition from measured ingredients: a small table of common foods, per 100 g,
/// per 100 ml or per piece. Stands in for the AI estimate until that is wired up; the
/// result is always marked as an estimate.
/// </summary>
public static class IngredientEstimate
{
    private sealed record Per(double Kcal, double Protein, double Carbs, double Fat, double Fibre);

    // Values per 100 g / 100 ml, or per piece for "pc".
    private static readonly Dictionary<string, Per> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chickenBreast"] = new(120, 23, 0, 2.6, 0),
        ["chickenThigh"] = new(180, 19, 0, 11, 0),
        ["salmon"] = new(208, 20, 0, 13, 0),
        ["tuna"] = new(116, 26, 0, 1, 0),
        ["rice"] = new(360, 7, 79, 0.6, 1.3),
        ["pasta"] = new(360, 13, 72, 1.5, 3),
        ["redLentils"] = new(350, 24, 60, 1.5, 11),
        ["brownLentils"] = new(350, 25, 60, 1, 10),
        ["oil"] = new(820, 0, 0, 91, 0),
        ["onion"] = new(44, 1.2, 10, 0.1, 2),
        ["broccoli"] = new(34, 2.8, 7, 0.4, 2.6),
        ["lemon"] = new(17, 0.6, 5, 0.2, 1.6),
        ["eggs"] = new(72, 6.3, 0.4, 4.8, 0),
        ["tomatoes"] = new(32, 1.6, 7, 0.3, 1.9),
        ["pepper"] = new(30, 1, 7, 0.3, 2.5),
        ["carrot"] = new(25, 0.6, 6, 0.1, 1.7),
        ["wrap"] = new(180, 6, 30, 4, 4),
        ["yogurt"] = new(60, 3.5, 4.7, 3.3, 0),
        ["lettuce"] = new(15, 1.4, 2.9, 0.2, 1.3),
        ["whey"] = new(390, 78, 8, 6, 0),
        ["banana"] = new(105, 1.3, 27, 0.4, 3.1),
        ["milk"] = new(47, 3.4, 4.8, 1.5, 0),
        ["cottage"] = new(98, 11, 3.4, 4.3, 0),
        ["berries"] = new(50, 0.8, 12, 0.3, 4),
        ["cucumber"] = new(15, 0.7, 3.6, 0.1, 0.5),
        ["pita"] = new(165, 5.5, 33, 0.7, 1.3)
    };

    // A typical portion when nothing is measured.
    private static readonly Dictionary<MealType, NutritionInfo> ByType = new()
    {
        [MealType.Meat] = new() { Kcal = 650, Protein = 38, Carbs = 50, Fat = 30, Fibre = 5 },
        [MealType.Chicken] = new() { Kcal = 550, Protein = 42, Carbs = 50, Fat = 18, Fibre = 5 },
        [MealType.Fish] = new() { Kcal = 520, Protein = 36, Carbs = 45, Fat = 20, Fibre = 5 },
        [MealType.Vegetarian] = new() { Kcal = 480, Protein = 18, Carbs = 65, Fat = 16, Fibre = 10 }
    };

    /// <summary>Per-serving estimate. Unknown ingredients are skipped; with none known, a typical portion for the meal type.</summary>
    public static NutritionInfo For(MealCatalogItem meal)
    {
        var total = new NutritionInfo { Source = NutritionSource.Estimate };
        var known = 0;
        foreach (var line in meal.IngredientLines)
        {
            if (!Table.TryGetValue(line.Id, out var per)) continue;
            var factor = line.Unit == IngredientUnit.Pieces ? line.QtyPerServing : line.QtyPerServing / 100;
            total.Kcal += per.Kcal * factor;
            total.Protein += per.Protein * factor;
            total.Carbs += per.Carbs * factor;
            total.Fat += per.Fat * factor;
            total.Fibre += per.Fibre * factor;
            known++;
        }

        var result = known > 0 ? total : ByType[meal.MealType].Scale(1);
        return new NutritionInfo
        {
            Kcal = Math.Round(result.Kcal / 10) * 10,
            Protein = Math.Round(result.Protein),
            Carbs = Math.Round(result.Carbs),
            Fat = Math.Round(result.Fat),
            Fibre = Math.Round(result.Fibre),
            Source = NutritionSource.Estimate
        };
    }
}

/// <summary>The "macros add up to" check in the nutrition editor: 4 kcal per g protein and carbs, 9 per g fat.</summary>
public static class MacroCheck
{
    public const double Tolerance = 0.10;

    public static double MacroKcal(double protein, double carbs, double fat) => 4 * protein + 4 * carbs + 9 * fat;

    /// <summary>True when the macros are more than 10% away from the calories entered.</summary>
    public static bool IsOff(double kcal, double protein, double carbs, double fat) =>
        kcal > 0 && Math.Abs(MacroKcal(protein, carbs, fat) - kcal) / kcal > Tolerance;
}
