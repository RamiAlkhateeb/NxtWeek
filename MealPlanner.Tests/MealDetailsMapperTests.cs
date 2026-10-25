namespace MealPlanner.Tests;

public class MealDetailsMapperTests
{
    private static SuggestedMealDetails Biryani() => new()
    {
        Servings = 4,
        Kcal = 640, Protein = 40, Carbs = 70, Fat = 20, Fibre = 4,   // 4·40 + 4·70 + 9·20 = 620
        Ingredients =
        [
            new() { Id = "chickenThigh", Name = "Chicken thigh", Quantity = 160, Unit = "g", Aisle = "meat" },
            new() { Id = "basmati rice", Name = "Basmati rice", Quantity = 80, Unit = "G", Aisle = "Pantry" },
            new() { Id = "onion", Name = "Onion", Quantity = 0.5, Unit = "piece", Aisle = "veg" },
            new() { Id = "", Name = "", Quantity = 10, Unit = "g", Aisle = "other" },          // no name: dropped
            new() { Id = "salt", Name = "Salt", Quantity = 0, Unit = "g", Aisle = "pantry" }   // no amount: dropped
        ]
    };

    [Fact]
    public void A_good_reply_fills_ingredients_and_an_estimate()
    {
        var meal = new MealCatalogItem { Name = "Chicken biryani" };

        Assert.True(MealDetailsMapper.Apply(meal, MealDetailsMapper.Validate(Biryani())));

        Assert.Equal(["chickenThigh", "basmatiRice", "onion"], meal.IngredientLines.Select(l => l.Id));
        Assert.Equal(["Chicken thigh", "Basmati rice", "Onion"], meal.Ingredients);
        Assert.Equal("g", meal.IngredientLines[1].Unit);
        Assert.Equal("pantry", meal.IngredientLines[1].Aisle);
        Assert.Equal("g", meal.IngredientLines[2].Unit);        // unknown unit falls back to grams
        Assert.Equal("other", meal.IngredientLines[2].Aisle);   // unknown aisle falls back to other
        Assert.Equal(4, meal.ServingsYield);
        Assert.Equal(640, meal.Nutrition!.Kcal);
        Assert.Equal(NutritionSource.Estimate, meal.Nutrition.Source);
    }

    [Fact]
    public void Existing_values_are_never_overwritten()
    {
        var mine = new NutritionInfo { Kcal = 500, Protein = 30, Source = NutritionSource.Manual };
        var meal = new MealCatalogItem
        {
            Nutrition = mine,
            Ingredients = ["My rice"],
            IngredientLines = [new() { Id = "rice", Name = "My rice", QtyPerServing = 90, Unit = "g", Aisle = "pantry" }]
        };

        Assert.False(MealDetailsMapper.Apply(meal, Biryani()));
        Assert.Same(mine, meal.Nutrition);
        Assert.Equal("My rice", Assert.Single(meal.IngredientLines).Name);
    }

    [Fact]
    public void Free_text_ingredients_are_kept_when_lines_are_added()
    {
        var meal = new MealCatalogItem { Ingredients = ["دجاج", "أرز"] };

        MealDetailsMapper.Apply(meal, Biryani());

        Assert.Equal(["دجاج", "أرز"], meal.Ingredients);
        Assert.Equal(3, meal.IngredientLines.Count);
    }

    [Theory]
    [InlineData(20, 1, 1, 1)]        // too few calories
    [InlineData(3000, 100, 400, 90)] // too many
    [InlineData(600, 10, 10, 5)]     // macros far from the calories
    public void Implausible_replies_are_rejected(double kcal, double p, double c, double f)
    {
        var d = Biryani();
        d.Kcal = kcal; d.Protein = p; d.Carbs = c; d.Fat = f;

        Assert.Throws<InvalidOperationException>(() => MealDetailsMapper.Validate(d));
    }

    [Fact]
    public void A_reply_without_usable_ingredients_is_rejected()
    {
        var d = Biryani();
        d.Ingredients = [new() { Name = "", Quantity = 1 }];

        Assert.Throws<InvalidOperationException>(() => MealDetailsMapper.Validate(d));
    }

    [Fact]
    public void Needs_details_until_both_are_there()
    {
        Assert.True(MealDetailsMapper.NeedsDetails(new MealCatalogItem()));
        Assert.True(MealDetailsMapper.NeedsDetails(new MealCatalogItem { Nutrition = new() }));
        Assert.False(MealDetailsMapper.NeedsDetails(new MealCatalogItem { Nutrition = new(), IngredientLines = [new()] }));
    }
}
