using System.Globalization;

namespace MealPlanner.Tests;

public class ShoppingAggregatorTests
{
    private static readonly DateOnly Friday = new(2026, 10, 23);
    private static readonly DateOnly Sunday = new(2026, 10, 25);

    private static readonly MealCatalogItem ChickenRice = new()
    {
        Id = "cr", Name = "Chicken and rice",
        IngredientLines =
        [
            new() { Id = "chickenBreast", Name = "Chicken breast", QtyPerServing = 150, Unit = "g", Aisle = "meat" },
            new() { Id = "rice", Name = "Rice", QtyPerServing = 70, Unit = "g", Aisle = "pantry" },
            new() { Id = "onion", Name = "Onion", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }
        ]
    };
    private static readonly MealCatalogItem Mujaddara = new()
    {
        Id = "mj", Name = "Mujaddara",
        IngredientLines =
        [
            new() { Id = "rice", Name = "Rice", QtyPerServing = 50, Unit = "g", Aisle = "pantry" },
            new() { Id = "onion", Name = "Onion", QtyPerServing = 1, Unit = "pc", Aisle = "produce" }
        ]
    };
    private static readonly MealCatalogItem Pizza = new() { Id = "pz", Name = "Pizza", Ingredients = ["Dough", "Cheese"] };
    private static readonly Dictionary<string, MealCatalogItem> Catalog = new() { ["cr"] = ChickenRice, ["mj"] = Mujaddara, ["pz"] = Pizza };

    private static PlanDay Day(DateOnly d, params (MealSlot S, SlotEntry E)[] slots)
    {
        var day = new PlanDay { Date = d, Type = DayType.Training };
        foreach (var (s, e) in slots) day.Slots[s] = e;
        return day;
    }

    [Fact]
    public void Planned_entries_skip_eaten_leftover_and_past_days()
    {
        var days = new Dictionary<DateOnly, PlanDay>
        {
            [Friday.AddDays(-1)] = Day(Friday.AddDays(-1), (MealSlot.Meal1, new SlotEntry { MealId = "cr" })),
            [Friday] = Day(Friday, (MealSlot.Meal1, new SlotEntry { MealId = "cr", Eaten = true }), (MealSlot.Dinner, new SlotEntry { MealId = "mj" })),
            [Sunday] = Day(Sunday, (MealSlot.Meal1, new SlotEntry { MealId = "cr", Leftover = true }), (MealSlot.Snack, new SlotEntry { Quick = new QuickAddEntry { Name = "Nuts" } }))
        };

        var planned = ShoppingAggregator.PlannedEntries(days, Catalog, Friday, Sunday, new NutritionProfile());

        Assert.Equal("mj", Assert.Single(planned).Meal.Id);
    }

    [Fact]
    public void Merge_multiplies_servings_and_prep_and_sums_across_meals()
    {
        var entries = new List<PlannedEntry>
        {
            new(Friday, MealSlot.Dinner, new SlotEntry { MealId = "cr", Servings = 1.5, PrepBatches = 2 }, ChickenRice),
            new(Sunday, MealSlot.Meal1, new SlotEntry { MealId = "mj" }, Mujaddara)
        };

        var merged = ShoppingAggregator.Merge(entries).ToDictionary(m => m.Key);

        Assert.Equal(450, merged["chickenBreast|g"].Quantity);       // 150 × 1.5 × 2
        Assert.Equal(210 + 50, merged["rice|g"].Quantity);
        Assert.Equal(2, merged["rice|g"].UsedIn);
        Assert.Equal(1.5 + 1, merged["onion|pc"].Quantity);
        Assert.Equal(3, ShoppingAggregator.StoredQuantity(merged["onion|pc"]));  // pieces round up
        Assert.Equal("pantry", merged["rice|g"].Aisle);
    }

    [Fact]
    public void Free_text_ingredients_merge_by_name_without_amounts()
    {
        var entries = new List<PlannedEntry>
        {
            new(Friday, MealSlot.Dinner, new SlotEntry { MealId = "pz", Servings = 2 }, Pizza),
            new(Sunday, MealSlot.Dinner, new SlotEntry { MealId = "pz" }, Pizza)
        };

        var dough = ShoppingAggregator.Merge(entries).Single(m => m.Name == "Dough");

        Assert.Null(dough.Unit);
        Assert.Equal(1, dough.Quantity);
        Assert.Equal(2, dough.UsedIn);
    }

    [Theory]
    [InlineData(1200, "g", "1.2", "kg")]
    [InlineData(750, "g", "750", "g")]
    [InlineData(1500, "ml", "1.5", "l")]
    [InlineData(2.5, "pc", "3", "pc")]
    public void Format_converts_large_amounts(double qty, string unit, string amount, string shownUnit)
    {
        Assert.Equal((amount, shownUnit), ShoppingAggregator.Format(qty, unit, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Format_uses_the_decimal_comma_in_german()
    {
        Assert.Equal(("1,2", "kg"), ShoppingAggregator.Format(1200, "g", CultureInfo.GetCultureInfo("de-DE")));
        Assert.Null(ShoppingAggregator.Format(1, null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Sync_adds_updates_and_removes_only_its_own_open_items()
    {
        var lines = new List<MergedIngredient>
        {
            new("rice|g", "Rice", 260, "g", "pantry", 2, "Chicken and rice"),
            new("onion|pc", "Onion", 2.5, "pc", "produce", 2, "Chicken and rice"),
            new("chickenBreast|g", "Chicken breast", 450, "g", "meat", 1, "Chicken and rice"),
            new("txt:milk", "Milk", 1, null, "", 1, "Shake")
        };
        var existing = new List<ShoppingItem>
        {
            new() { Id = "a", Name = "Rice", IngredientKey = "rice|g", Quantity = 140, UsedIn = 1 },                  // open: update
            new() { Id = "b", Name = "Chicken breast", IngredientKey = "chickenBreast|g", Quantity = 300, IsBought = true }, // bought: keep, no re-add
            new() { Id = "c", Name = "Lentils", IngredientKey = "brownLentils|g", Quantity = 70 },                   // no longer planned: remove
            new() { Id = "d", Name = "Old lentils", IngredientKey = "redLentils|g", Quantity = 80, IsBought = true }, // bought: keep
            new() { Id = "e", Name = "milk", Quantity = 1 }                                                          // typed: blocks a duplicate
        };

        var sync = ShoppingAggregator.Sync(existing, lines);

        Assert.Equal(["onion|pc"], sync.Add.Select(l => l.Key));
        Assert.Equal("a", Assert.Single(sync.Update).Item.Id);
        Assert.Equal("c", Assert.Single(sync.Remove).Id);
    }

    [Fact]
    public void Sync_with_nothing_changed_is_empty()
    {
        var line = new MergedIngredient("rice|g", "Rice", 260, "g", "pantry", 2, "x");
        var sync = ShoppingAggregator.Sync([new ShoppingItem { Name = "Rice", IngredientKey = "rice|g", Quantity = 260, UsedIn = 2 }], [line]);

        Assert.True(sync.IsEmpty);
    }
}
