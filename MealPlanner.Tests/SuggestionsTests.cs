using MealPlanner.Shared.Services;

namespace MealPlanner.Tests;

public class SuggestionsTests
{
    private static MealCatalogItem M(string id, double kcal, double protein, params MealSlot[] slots) =>
        new() { Id = id, Name = id, Nutrition = new() { Kcal = kcal, Protein = protein, Source = NutritionSource.Manual }, SuitableSlots = slots.ToList() };

    private static readonly MealCatalogItem[] Catalog =
    [
        M("chickenRice", 590, 48, MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner),
        M("salmon", 640, 42, MealSlot.Meal1, MealSlot.Dinner),
        M("tunaWrap", 410, 38, MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner),
        M("shake", 310, 32, MealSlot.PostWorkout, MealSlot.Snack),
        M("cottage", 220, 24, MealSlot.PostWorkout, MealSlot.Snack),
        M("lentil", 380, 22, MealSlot.Meal1, MealSlot.Dinner),
        new() { Id = "koshari", Name = "koshari", SuitableSlots = [MealSlot.Meal1, MealSlot.Dinner] } // no nutrition
    ];

    [Fact]
    public void Suggestions_are_the_top_three_by_protein_that_fit()
    {
        var s = Suggestions.ForSlot(MealSlot.Dinner, openKcal: 900, emptySlots: 1, usedToday: new HashSet<string>(), Catalog);

        Assert.Equal(["chickenRice", "salmon", "tunaWrap"], s.Select(x => x.Meal.Id));
    }

    [Fact]
    public void Suggestions_skip_meals_used_today_too_big_or_without_nutrition()
    {
        var s = Suggestions.ForSlot(MealSlot.Dinner, openKcal: 600, emptySlots: 1, usedToday: new HashSet<string> { "chickenRice" }, Catalog);

        Assert.Equal(["tunaWrap", "lentil"], s.Select(x => x.Meal.Id));
    }

    [Fact]
    public void With_several_empty_slots_a_meal_must_leave_room_for_the_rest()
    {
        // 1,000 kcal over 3 slots: max(150, 333) × 1.3 ≈ 433 kcal per slot.
        var s = Suggestions.ForSlot(MealSlot.Meal1, openKcal: 1000, emptySlots: 3, usedToday: new HashSet<string>(), Catalog);

        Assert.Equal(["tunaWrap", "lentil"], s.Select(x => x.Meal.Id));
    }

    [Fact]
    public void Suggestions_respect_suitable_slots()
    {
        var s = Suggestions.ForSlot(MealSlot.Snack, openKcal: 900, emptySlots: 1, usedToday: new HashSet<string>(), Catalog);

        Assert.Equal(["shake", "cottage"], s.Select(x => x.Meal.Id));
    }

    [Fact]
    public void Protein_plan_reaches_the_open_protein_within_the_calories()
    {
        var plan = Suggestions.PlanProtein([MealSlot.PostWorkout, MealSlot.Dinner, MealSlot.Snack], openKcal: 1200, openProtein: 100, new HashSet<string>(), Catalog)!;

        Assert.True(plan.Protein >= 100);
        Assert.True(plan.Kcal <= 1200);
        Assert.Equal(3, plan.Picks.Count);
        Assert.Equal(3, plan.Picks.Select(p => p.Meal.Id).Distinct().Count());
        Assert.All(plan.Picks, p => Assert.True(Suggestions.Suits(p.Meal, p.Slot)));
    }

    [Fact]
    public void Protein_plan_prefers_the_smallest_overshoot()
    {
        // One slot, 40 g open: tunaWrap (38) falls short, salmon (42) overshoots least.
        var plan = Suggestions.PlanProtein([MealSlot.Dinner], openKcal: 700, openProtein: 40, new HashSet<string>(), Catalog)!;

        Assert.Equal("salmon", Assert.Single(plan.Picks).Meal.Id);
    }

    [Fact]
    public void Protein_plan_gets_as_close_as_it_can_when_nothing_reaches()
    {
        var plan = Suggestions.PlanProtein([MealSlot.Snack], openKcal: 400, openProtein: 80, new HashSet<string>(), Catalog)!;

        Assert.Equal("shake", Assert.Single(plan.Picks).Meal.Id);
    }

    [Fact]
    public void Protein_plan_is_null_without_empty_slots_or_when_nothing_fits()
    {
        Assert.Null(Suggestions.PlanProtein([], 1000, 50, new HashSet<string>(), Catalog));
        Assert.Null(Suggestions.PlanProtein([MealSlot.Dinner], 100, 50, new HashSet<string>(), Catalog));
    }

    [Fact]
    public void Snack_is_the_most_protein_under_250_kcal()
    {
        Assert.Equal("cottage", Suggestions.Snack(900, new HashSet<string>(), Catalog)!.Meal.Id);
        Assert.Null(Suggestions.Snack(200, new HashSet<string>(), Catalog));
    }

    [Fact]
    public void Seed_meals_produce_a_plan_for_the_starting_user()
    {
        var seeds = NutritionSeedMeals.All().Where(m => m.Language == "en").ToList();
        var plan = Suggestions.PlanProtein([MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner, MealSlot.Snack], 1950, 135, new HashSet<string>(), seeds);

        Assert.NotNull(plan);
        Assert.True(plan!.Protein >= 135);
        Assert.True(plan.Kcal <= 1950);
    }
}

public class MealFilteringTests
{
    private static MealCatalogItem M(string id, double? kcal, double protein, int? minutes) =>
        new() { Id = id, PreparationMinutes = minutes, Nutrition = kcal is null ? null : new() { Kcal = kcal.Value, Protein = protein } };

    [Fact]
    public void Filters_combine()
    {
        var shake = M("shake", 310, 32, 3);
        var salmon = M("salmon", 640, 42, 25);
        var koshari = M("koshari", null, 0, 45);
        var favs = new HashSet<string> { "salmon" };

        Assert.True(MealFiltering.Matches(shake, MealFilters.HighProtein | MealFilters.Under600 | MealFilters.Quick, favs));
        Assert.False(MealFiltering.Matches(salmon, MealFilters.Under600, favs));
        Assert.True(MealFiltering.Matches(salmon, MealFilters.Favourites | MealFilters.HighProtein, favs));
        Assert.False(MealFiltering.Matches(koshari, MealFilters.HighProtein, favs));
        Assert.True(MealFiltering.Matches(koshari, MealFilters.None, favs));
    }

    [Fact]
    public void Pick_order_puts_meals_that_fit_first_then_protein()
    {
        var order = MealFiltering.PickOrder([M("big", 900, 60, 30), M("small", 300, 20, 10), M("mid", 500, 40, 20), M("none", null, 0, 10)], openKcal: 600);

        Assert.Equal(["mid", "small", "big", "none"], order.Select(m => m.Id));
    }
}
