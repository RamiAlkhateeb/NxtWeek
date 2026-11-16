namespace MealPlanner.Tests;

public class MealSortTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);

    private static MealCatalogItem M(string id, string name, double? kcal = null, double protein = 0, int? minutes = null) => new()
    {
        Id = id, Name = name, PreparationMinutes = minutes,
        Nutrition = kcal is null ? null : new NutritionInfo { Kcal = kcal.Value, Protein = protein }
    };

    private static readonly List<MealCatalogItem> Meals =
    [
        M("a", "Apple oats", 300, 12, 10),
        M("b", "Beef stew", 700, 45, 90),
        M("c", "Chicken rice", 590, 48, 25),
        M("d", "Dolma")   // no nutrition yet
    ];

    [Fact]
    public void Fewest_calories_and_most_protein_put_meals_without_nutrition_last()
    {
        Assert.Equal(["a", "c", "b", "d"], MealFiltering.Sort(Meals, MealSort.FewestKcal, new HashSet<string>()).Select(m => m.Id));
        Assert.Equal(["c", "b", "a", "d"], MealFiltering.Sort(Meals, MealSort.MostProtein, new HashSet<string>()).Select(m => m.Id));
    }

    [Fact]
    public void Recommended_puts_favourites_then_most_eaten_then_by_name()
    {
        var eaten = new Dictionary<string, int> { ["b"] = 3, ["a"] = 1 };

        Assert.Equal(["d", "b", "a", "c"], MealFiltering.Sort(Meals, MealSort.Recommended, new HashSet<string> { "d" }, eaten).Select(m => m.Id));
        Assert.Equal(["a", "b", "c", "d"], MealFiltering.Sort(Meals, MealSort.Recommended, new HashSet<string>()).Select(m => m.Id));
    }

    [Fact]
    public void Quick_means_20_minutes_or_less_and_unknown_time_is_not_quick()
    {
        var quick = Meals.Where(m => MealFiltering.Matches(m, MealFilters.Quick, new HashSet<string>())).Select(m => m.Id);

        Assert.Equal(["a"], quick);
    }

    [Fact]
    public void Eaten_counts_ignore_planned_skipped_and_future_meals()
    {
        var days = new Dictionary<DateOnly, PlanDay>
        {
            [Today.AddDays(-2)] = new() { Slots = { [MealSlot.Meal1] = new SlotEntry { MealId = "c", Eaten = true }, [MealSlot.Dinner] = new SlotEntry { MealId = "b", Skipped = true } } },
            [Today.AddDays(-1)] = new() { Slots = { [MealSlot.Meal1] = new SlotEntry { MealId = "c", Eaten = true }, [MealSlot.Dinner] = new SlotEntry { MealId = "a" } } },
            [Today] = new() { Slots = { [MealSlot.Meal1] = new SlotEntry { MealId = "a", Eaten = true } } },
            [Today.AddDays(1)] = new() { Slots = { [MealSlot.Meal1] = new SlotEntry { MealId = "b", Eaten = true } } }
        };

        var counts = MealFiltering.EatenCounts(days, Today);

        Assert.Equal(2, counts["c"]);
        Assert.Equal(1, counts["a"]);
        Assert.False(counts.ContainsKey("b"));
    }

    [Fact]
    public void Top_meals_are_the_most_eaten_still_in_the_catalog()
    {
        var counts = new Dictionary<string, int> { ["c"] = 5, ["a"] = 2, ["gone"] = 9, ["b"] = 2 };

        var top = MealFiltering.TopMeals(counts, Meals, max: 2);

        Assert.Equal([("c", 5), ("a", 2)], top.Select(t => (t.Meal.Id, t.Count)));
        Assert.Empty(MealFiltering.TopMeals(new Dictionary<string, int>(), Meals));
    }
}
