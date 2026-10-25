using MealPlanner.Shared.Services;

namespace MealPlanner.Tests;

public class SeedMealNutritionTests
{
    [Fact]
    public void Every_built_in_meal_has_an_estimate_and_nothing_else_does()
    {
        var ids = FirebaseMealService.BuiltInMeals().Select(m => m.Id).ToHashSet();

        Assert.Equal(91, ids.Count);
        Assert.All(ids, id => Assert.True(SeedMealNutrition.ById.ContainsKey(id), id));
        Assert.All(SeedMealNutrition.ById.Keys, id => Assert.Contains(id, ids));
    }

    [Fact]
    public void Estimates_are_plausible_and_add_up()
    {
        foreach (var (id, v) in SeedMealNutrition.ById)
        {
            Assert.InRange(v.Kcal, 150, 1200);
            var fromMacros = 4 * v.Protein + 4 * v.Carbs + 9 * v.Fat;
            Assert.True(Math.Abs(fromMacros - v.Kcal) / v.Kcal <= 0.15, $"{id}: macros {fromMacros} vs {v.Kcal} kcal");
        }
    }

    [Fact]
    public void Estimates_carry_the_estimate_source()
    {
        Assert.Equal(NutritionSource.Estimate, SeedMealNutrition.For("m9")!.Source);
        Assert.Null(SeedMealNutrition.For("user-meal"));
    }
}
