using MealPlanner.Shared.Services;

namespace MealPlanner.Tests;

public class PlanMovesTests
{
    private static readonly DateOnly Monday = new(2026, 10, 19);
    private static readonly DateOnly Tuesday = Monday.AddDays(1);

    private static PlanDay Day(DateOnly date, params (MealSlot Slot, SlotEntry Entry)[] slots)
    {
        var day = new PlanDay { Date = date };
        foreach (var (slot, entry) in slots) day.Slots[slot] = entry;
        return day;
    }

    [Fact]
    public void Moving_to_an_empty_slot_on_another_day_empties_the_source_and_resets_the_answer()
    {
        var from = Day(Monday, (MealSlot.Dinner, new SlotEntry { MealId = "a", Servings = 1.5, PrepBatches = 2, Eaten = true }));
        var to = Day(Tuesday);

        Assert.True(PlanMoves.Move(from, MealSlot.Dinner, to, MealSlot.Meal1));

        Assert.Empty(from.Slots);
        var moved = to.Slots[MealSlot.Meal1];
        Assert.Equal("a", moved.MealId);
        Assert.Equal(1.5, moved.Servings);
        Assert.Equal(2, moved.PrepBatches);
        Assert.False(moved.Eaten);
    }

    [Fact]
    public void Moving_onto_a_taken_slot_swaps_the_two_meals()
    {
        var from = Day(Monday, (MealSlot.Dinner, new SlotEntry { MealId = "a" }));
        var to = Day(Tuesday, (MealSlot.Dinner, new SlotEntry { MealId = "b", Skipped = true }));

        Assert.True(PlanMoves.Move(from, MealSlot.Dinner, to, MealSlot.Dinner));

        Assert.Equal("b", from.Slots[MealSlot.Dinner].MealId);
        Assert.False(from.Slots[MealSlot.Dinner].Skipped);
        Assert.Equal("a", to.Slots[MealSlot.Dinner].MealId);
    }

    [Fact]
    public void Moving_within_the_same_day_keeps_eaten_and_swaps()
    {
        var day = Day(Monday, (MealSlot.Meal1, new SlotEntry { MealId = "a", Eaten = true }), (MealSlot.Dinner, new SlotEntry { MealId = "b" }));

        Assert.True(PlanMoves.Move(day, MealSlot.Meal1, day, MealSlot.Dinner));

        Assert.Equal("b", day.Slots[MealSlot.Meal1].MealId);
        Assert.Equal("a", day.Slots[MealSlot.Dinner].MealId);
        Assert.True(day.Slots[MealSlot.Dinner].Eaten);
    }

    [Fact]
    public void Nothing_moves_from_an_empty_slot_or_onto_itself()
    {
        var day = Day(Monday, (MealSlot.Meal1, new SlotEntry { MealId = "a" }));

        Assert.False(PlanMoves.Move(day, MealSlot.Dinner, Day(Tuesday), MealSlot.Dinner));
        Assert.False(PlanMoves.Move(day, MealSlot.Meal1, day, MealSlot.Meal1));
        Assert.False(PlanMoves.Copy(day, MealSlot.Meal1, day, MealSlot.Meal1));
        Assert.Equal("a", day.Slots[MealSlot.Meal1].MealId);
    }

    [Fact]
    public void Copying_brings_meal_and_servings_but_starts_fresh_and_leaves_the_source()
    {
        var from = Day(Monday, (MealSlot.Dinner, new SlotEntry { MealId = "a", Servings = 2, Eaten = true, PrepBatches = 3, Leftover = true }));
        var to = Day(Tuesday, (MealSlot.Dinner, new SlotEntry { MealId = "old" }));

        Assert.True(PlanMoves.Copy(from, MealSlot.Dinner, to, MealSlot.Dinner));

        var copy = to.Slots[MealSlot.Dinner];
        Assert.Equal("a", copy.MealId);
        Assert.Equal(2, copy.Servings);
        Assert.False(copy.Eaten);
        Assert.False(copy.Leftover);
        Assert.Equal(1, copy.PrepBatches);
        Assert.True(from.Slots[MealSlot.Dinner].Eaten);
    }

    [Fact]
    public void A_quick_add_is_copied_as_its_own_object()
    {
        var from = Day(Monday, (MealSlot.Snack, new SlotEntry { Quick = new QuickAddEntry { Name = "Nuts", Kcal = 180 } }));
        var to = Day(Tuesday);

        PlanMoves.Copy(from, MealSlot.Snack, to, MealSlot.Snack);
        to.Slots[MealSlot.Snack].Quick!.Kcal = 999;

        Assert.Equal(180, from.Slots[MealSlot.Snack].Quick!.Kcal);
    }
}

public class MealSearchAndRediscoverTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    private static MealCatalogItem M(string id, string name, double kcal, double protein, params string[] ingredients) => new()
    {
        Id = id, Name = name, Ingredients = ingredients.ToList(),
        Nutrition = new NutritionInfo { Kcal = kcal, Protein = protein }
    };

    private static readonly List<MealCatalogItem> Catalog =
    [
        M("soup", "Lentil soup", 380, 22, "lentils", "onion"),
        M("rice", "Chicken rice", 590, 48, "chicken", "rice"),
        M("wrap", "Tuna wrap", 410, 38, "tuna", "bread"),
        M("salad", "Chickpea salad", 300, 15, "chickpeas")
    ];

    [Fact]
    public void Search_matches_names_and_ingredients_with_name_starts_first()
    {
        Assert.Equal(["rice", "salad"], MealFiltering.Search(Catalog, "chick").Select(m => m.Id));
        Assert.Equal(["soup"], MealFiltering.Search(Catalog, "ONION").Select(m => m.Id));
        Assert.Empty(MealFiltering.Search(Catalog, "  "));
        Assert.Empty(MealFiltering.Search(Catalog, "pizza"));
    }

    [Fact]
    public void Search_leaves_archived_meals_out()
    {
        var archived = M("old", "Chicken pie", 500, 30);
        archived.IsArchived = true;

        Assert.DoesNotContain("old", MealFiltering.Search([.. Catalog, archived], "chicken").Select(m => m.Id));
    }

    private static Dictionary<DateOnly, PlanDay> History(params (int DaysAgo, string MealId, bool Eaten)[] entries)
    {
        var days = new Dictionary<DateOnly, PlanDay>();
        foreach (var (ago, id, eaten) in entries)
        {
            var date = Today.AddDays(-ago);
            if (!days.TryGetValue(date, out var day)) days[date] = day = new PlanDay { Date = date };
            var slot = day.Slots.ContainsKey(MealSlot.Meal1) ? MealSlot.Dinner : MealSlot.Meal1;
            day.Slots[slot] = new SlotEntry { MealId = id, Eaten = eaten };
        }
        return days;
    }

    [Fact]
    public void Rediscover_offers_a_meal_eaten_before_but_not_in_three_weeks()
    {
        var history = History((30, "soup", true), (40, "soup", true), (25, "wrap", true), (5, "rice", true), (35, "rice", true));

        var pick = Suggestions.Rediscover(MealSlot.Dinner, 900, new HashSet<string>(), Catalog, history, Today);

        // Soup was eaten twice long ago; rice was eaten 5 days ago, so it is not a rediscovery.
        Assert.Equal("soup", pick!.Meal.Id);
    }

    [Fact]
    public void Rediscover_ignores_planned_only_meals_and_respects_budget_and_exclusions()
    {
        var history = History((30, "soup", false), (30, "wrap", true));

        Assert.Equal("wrap", Suggestions.Rediscover(MealSlot.Dinner, 900, new HashSet<string>(), Catalog, history, Today)!.Meal.Id);
        Assert.Null(Suggestions.Rediscover(MealSlot.Dinner, 300, new HashSet<string>(), Catalog, history, Today));
        Assert.Null(Suggestions.Rediscover(MealSlot.Dinner, 900, new HashSet<string> { "wrap" }, Catalog, history, Today));
        Assert.Null(Suggestions.Rediscover(MealSlot.Dinner, 900, new HashSet<string>(), Catalog, history, Today, new HashSet<string> { "wrap" }));
        Assert.Null(Suggestions.Rediscover(MealSlot.Dinner, 900, new HashSet<string>(), Catalog, new Dictionary<DateOnly, PlanDay>(), Today));
    }
}
