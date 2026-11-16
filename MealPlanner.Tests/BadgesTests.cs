namespace MealPlanner.Tests;

public class BadgesTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly Dictionary<string, NutritionInfo> Meals = new()
    {
        ["big"] = new() { Kcal = 700, Protein = 70 },
        ["small"] = new() { Kcal = 300, Protein = 20 }
    };
    private static readonly MealPrefs TwoMeals = new() { On = [MealSlot.Meal1, MealSlot.Dinner] };

    private static PlanDay Day(DateOnly date, string meal1, string dinner, bool eaten = true, int prep = 1) => new()
    {
        Date = date,
        Slots =
        {
            [MealSlot.Meal1] = new SlotEntry { MealId = meal1, Eaten = eaten, PrepBatches = prep },
            [MealSlot.Dinner] = new SlotEntry { MealId = dinner, Eaten = eaten }
        }
    };

    private static List<Badge> For(Dictionary<DateOnly, PlanDay> days, DateOnly today, bool shared = false) =>
        Badges.For(days, today, TwoMeals, 135, id => Meals.GetValueOrDefault(id), shared);

    private static Badge Get(List<Badge> badges, BadgeKind kind) => badges.Single(b => b.Kind == kind);

    [Fact]
    public void A_week_with_every_day_logged_earns_the_first_full_week()
    {
        var days = Enumerable.Range(0, 7).ToDictionary(i => Monday.AddDays(i), i => Day(Monday.AddDays(i), "big", "small"));

        Assert.True(Get(For(days, Monday.AddDays(6)), BadgeKind.FirstFullWeek).Earned);

        days[Monday.AddDays(3)].Slots[MealSlot.Dinner] = new SlotEntry { MealId = "small", Skipped = true };
        Assert.True(Get(For(days, Monday.AddDays(6)), BadgeKind.FirstFullWeek).Earned);

        days.Remove(Monday.AddDays(5));
        Assert.False(Get(For(days, Monday.AddDays(6)), BadgeKind.FirstFullWeek).Earned);
    }

    [Fact]
    public void Protein_counts_the_best_run_of_days_at_95_percent_and_a_missed_day_breaks_only_the_run()
    {
        // 90 g a day is under 95% of 135 (128.25); 140 g reaches it.
        var days = new Dictionary<DateOnly, PlanDay>();
        for (var i = 0; i < 4; i++) days[Monday.AddDays(i)] = Day(Monday.AddDays(i), "big", "big");    // 140 g
        days[Monday.AddDays(4)] = Day(Monday.AddDays(4), "big", "small");                               // 90 g
        for (var i = 5; i < 8; i++) days[Monday.AddDays(i)] = Day(Monday.AddDays(i), "big", "big");

        var badge = Get(For(days, Monday.AddDays(10)), BadgeKind.Protein7);

        Assert.Equal(4, badge.Progress);
        Assert.False(badge.Earned);

        for (var i = 8; i < 12; i++) days[Monday.AddDays(i)] = Day(Monday.AddDays(i), "big", "big");
        Assert.True(Get(For(days, Monday.AddDays(12)), BadgeKind.Protein7).Earned);
    }

    [Fact]
    public void Planned_meals_and_future_days_do_not_count()
    {
        var days = new Dictionary<DateOnly, PlanDay>
        {
            [Monday] = Day(Monday, "big", "big", eaten: false, prep: 3),
            [Monday.AddDays(9)] = Day(Monday.AddDays(9), "big", "small", prep: 2)
        };

        var badges = For(days, Monday.AddDays(1));

        Assert.All(badges.Where(b => b.Kind != BadgeKind.SharedWeek), b => Assert.Equal(0, b.Progress));
    }

    [Fact]
    public void Different_meals_batch_cooking_and_sharing()
    {
        var days = new Dictionary<DateOnly, PlanDay>();
        for (var i = 0; i < 6; i++) days[Monday.AddDays(i)] = Day(Monday.AddDays(i), $"m{i}a", $"m{i}b", prep: i == 2 ? 2 : 1);

        var badges = For(days, Monday.AddDays(6), shared: true);

        Assert.Equal(10, Get(badges, BadgeKind.TenMeals).Progress);
        Assert.True(Get(badges, BadgeKind.TenMeals).Earned);
        Assert.True(Get(badges, BadgeKind.FirstBatch).Earned);
        Assert.True(Get(badges, BadgeKind.SharedWeek).Earned);
        Assert.False(Get(For(new(), Monday), BadgeKind.SharedWeek).Earned);
    }
}
