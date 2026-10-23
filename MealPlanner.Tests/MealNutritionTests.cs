using MealPlanner.Shared.Services;

namespace MealPlanner.Tests;

public class MealNutritionTests
{
    [Fact]
    public void Estimate_from_measured_ingredients_is_marked_as_estimate()
    {
        var koshari = NutritionSeedMeals.All().First(m => m.Id == "en_n10");

        var n = IngredientEstimate.For(koshari);

        Assert.Equal(NutritionSource.Estimate, n.Source);
        Assert.InRange(n.Kcal, 480, 580); // prototype's AI estimate is 520
        Assert.InRange(n.Protein, 14, 22);
    }

    [Fact]
    public void Estimate_without_known_ingredients_falls_back_to_the_meal_type()
    {
        var meal = new MealCatalogItem { MealType = MealType.Chicken, Ingredients = ["Something"] };

        var n = IngredientEstimate.For(meal);

        Assert.Equal(NutritionSource.Estimate, n.Source);
        Assert.Equal(550, n.Kcal);
    }

    [Fact]
    public void Pieces_count_per_piece_and_grams_per_100()
    {
        var meal = new MealCatalogItem
        {
            IngredientLines =
            [
                new() { Id = "eggs", QtyPerServing = 2, Unit = IngredientUnit.Pieces },
                new() { Id = "rice", QtyPerServing = 50, Unit = IngredientUnit.Grams }
            ]
        };

        Assert.Equal(Math.Round((2 * 72 + 0.5 * 360) / 10.0) * 10, IngredientEstimate.For(meal).Kcal);
    }

    [Theory]
    [InlineData(590, 48, 62, 14, false)] // 4·48 + 4·62 + 9·14 = 566, within 10%
    [InlineData(400, 48, 62, 14, true)]
    [InlineData(0, 10, 10, 10, false)]   // no calories entered yet: no warning
    public void Macro_check_flags_more_than_10_percent_off(double kcal, double p, double c, double f, bool off)
    {
        Assert.Equal(off, MacroCheck.IsOff(kcal, p, c, f));
    }

    [Fact]
    public void Seed_meals_come_in_all_three_languages_with_stable_unique_ids()
    {
        var all = NutritionSeedMeals.All();

        Assert.Equal(30, all.Count);
        Assert.Equal(all.Count, all.Select(m => m.Id).Distinct().Count());
        foreach (var lang in new[] { "ar", "en", "de" })
            Assert.Equal(10, all.Count(m => m.Language == lang));
    }

    [Fact]
    public void Seed_meals_use_known_aisles_and_units_and_mark_their_source()
    {
        var aisles = new AisleService().All.Select(a => a.Key).ToHashSet();
        var units = new[] { IngredientUnit.Grams, IngredientUnit.Millilitres, IngredientUnit.Pieces };

        foreach (var meal in NutritionSeedMeals.All())
        {
            Assert.NotEmpty(meal.IngredientLines);
            Assert.All(meal.IngredientLines, l => { Assert.Contains(l.Aisle, aisles); Assert.Contains(l.Unit, units); Assert.False(string.IsNullOrWhiteSpace(l.Name)); });
            Assert.True(meal.Nutrition is null || meal.Nutrition.Source is NutritionSource.Estimate or NutritionSource.Manual);
            Assert.NotEmpty(meal.SuitableSlots);
        }
        // Koshari ships without nutrition so the "No nutrition yet" state is reachable.
        Assert.Null(NutritionSeedMeals.All().First(m => m.Id == "n10").Nutrition);
    }
}
