using MealPlanner.Shared.Services;

namespace MealPlanner.Tests;

public class ComeBackTests
{
    // 2026-11-16 is a Monday.
    private static readonly DateOnly Monday = new(2026, 11, 16);
    private static readonly DateOnly Sunday = Monday.AddDays(6);
    private static readonly DateOnly NextMonday = Monday.AddDays(7);
    private static readonly MealPrefs TwoMeals = new() { On = [MealSlot.Meal1, MealSlot.Dinner] };
    private static readonly Dictionary<string, NutritionInfo> Meals = new()
    {
        ["big"] = new() { Kcal = 700, Protein = 70 },
        ["small"] = new() { Kcal = 300, Protein = 20 }
    };

    private static NutritionInfo? For(string id) => Meals.GetValueOrDefault(id);

    private static PlanDay Day(DateOnly date, params (MealSlot Slot, SlotEntry Entry)[] slots)
    {
        var day = new PlanDay { Date = date };
        foreach (var (slot, entry) in slots) day.Slots[slot] = entry;
        return day;
    }

    [Fact]
    public void Review_is_about_the_ending_week_from_sunday_evening_and_the_week_before_until_then()
    {
        Assert.Equal(Monday, ComeBack.ReviewWeek(Sunday.ToDateTime(new TimeOnly(18, 0))));
        Assert.Equal(Monday.AddDays(-7), ComeBack.ReviewWeek(Sunday.ToDateTime(new TimeOnly(17, 59))));
        Assert.Equal(Monday, ComeBack.ReviewWeek(NextMonday.ToDateTime(new TimeOnly(9, 0))));
        Assert.Equal(Monday.AddDays(-7), ComeBack.ReviewWeek(Monday.AddDays(3).ToDateTime(new TimeOnly(20, 0))));
    }

    [Fact]
    public void Review_banner_shows_on_sunday_evening_and_monday_until_seen()
    {
        var sundayEvening = Sunday.ToDateTime(new TimeOnly(19, 0));
        var monday = NextMonday.ToDateTime(new TimeOnly(8, 0));

        Assert.True(ComeBack.ShowReviewBanner(sundayEvening, null, 3));
        Assert.True(ComeBack.ShowReviewBanner(monday, Monday.AddDays(-7), 3));
        Assert.False(ComeBack.ShowReviewBanner(monday, Monday, 3));
        Assert.False(ComeBack.ShowReviewBanner(monday, null, 0));
        Assert.False(ComeBack.ShowReviewBanner(Sunday.ToDateTime(new TimeOnly(12, 0)), null, 3));
        Assert.False(ComeBack.ShowReviewBanner(Monday.AddDays(2).ToDateTime(new TimeOnly(12, 0)), null, 3));
    }

    [Fact]
    public void Completing_the_day_is_celebrated_once()
    {
        var before = Day(Monday, (MealSlot.Meal1, new SlotEntry { MealId = "small", Eaten = true }), (MealSlot.Dinner, new SlotEntry { MealId = "small" }));
        var after = Day(Monday, (MealSlot.Meal1, new SlotEntry { MealId = "small", Eaten = true }), (MealSlot.Dinner, new SlotEntry { MealId = "small", Skipped = true }));

        Assert.Equal(Celebration.DayComplete, ComeBack.After(before, after, TwoMeals, 200, For));
        Assert.Equal(Celebration.None, ComeBack.After(after, after, TwoMeals, 200, For));
    }

    [Fact]
    public void Reaching_protein_is_celebrated_when_it_crosses_the_target()
    {
        var prefs = new MealPrefs { On = [MealSlot.Meal1, MealSlot.Dinner, MealSlot.Snack] };
        var before = Day(Monday, (MealSlot.Meal1, new SlotEntry { MealId = "big", Eaten = true }), (MealSlot.Dinner, new SlotEntry { MealId = "big" }));
        var after = Day(Monday, (MealSlot.Meal1, new SlotEntry { MealId = "big", Eaten = true }), (MealSlot.Dinner, new SlotEntry { MealId = "big", Eaten = true }));

        Assert.Equal(Celebration.ProteinReached, ComeBack.After(before, after, prefs, 130, For));
        Assert.Equal(Celebration.None, ComeBack.After(before, after, prefs, 200, For));
        Assert.Equal(Celebration.None, ComeBack.After(after, after, prefs, 130, For));
    }

    [Fact]
    public void Minutes_until_a_time_today()
    {
        Assert.Equal(90, ComeBack.MinutesUntil("18:30", new TimeOnly(17, 0)));
        Assert.Null(ComeBack.MinutesUntil("18:30", new TimeOnly(18, 31)));
        Assert.Null(ComeBack.MinutesUntil("nope", new TimeOnly(10, 0)));
    }

    [Fact]
    public void Ramadan_renames_and_retimes_three_slots_only_when_on()
    {
        var prefs = new MealPrefs();
        Assert.Equal("nut_slot_Dinner", prefs.NameKey(MealSlot.Dinner));
        Assert.Equal("19:00", prefs.TimeOf(MealSlot.Dinner));

        prefs.Ramadan = true;
        Assert.Equal("nut_ramadan_Meal1", prefs.NameKey(MealSlot.Meal1));
        Assert.Equal("nut_ramadan_Dinner", prefs.NameKey(MealSlot.Dinner));
        Assert.Equal("nut_slot_PostWorkout", prefs.NameKey(MealSlot.PostWorkout));
        Assert.Equal("04:30", prefs.TimeOf(MealSlot.Meal1));
        Assert.Equal("16:30", prefs.TimeOf(MealSlot.PostWorkout));
    }

    [Fact]
    public void Copying_a_week_fills_only_empty_slots_with_fresh_entries()
    {
        var days = new Dictionary<DateOnly, PlanDay>
        {
            [Monday] = Day(Monday, (MealSlot.Meal1, new SlotEntry { MealId = "big", Servings = 2, Eaten = true, PrepBatches = 3 }), (MealSlot.Dinner, new SlotEntry { MealId = "small", Skipped = true })),
            [Monday.AddDays(1)] = Day(Monday.AddDays(1), (MealSlot.Meal1, new SlotEntry { MealId = "small" })),
            [NextMonday] = Day(NextMonday, (MealSlot.Dinner, new SlotEntry { MealId = "mine" }))
        };

        var changed = PlanMoves.CopyWeek(days, Monday, NextMonday);

        Assert.Equal(2, changed.Count);
        var mon = changed.Single(d => d.Date == NextMonday);
        Assert.Equal("big", mon.Slots[MealSlot.Meal1].MealId);
        Assert.Equal(2, mon.Slots[MealSlot.Meal1].Servings);
        Assert.False(mon.Slots[MealSlot.Meal1].Eaten);
        Assert.Equal(1, mon.Slots[MealSlot.Meal1].PrepBatches);
        Assert.Equal("mine", mon.Slots[MealSlot.Dinner].MealId);
        Assert.Equal("small", changed.Single(d => d.Date == NextMonday.AddDays(1)).Slots[MealSlot.Meal1].MealId);
        // The source is untouched.
        Assert.True(days[Monday].Slots[MealSlot.Meal1].Eaten);
        Assert.Single(days[NextMonday].Slots);
    }

    [Fact]
    public void Copying_into_a_week_under_way_skips_the_days_already_gone()
    {
        var days = new Dictionary<DateOnly, PlanDay>
        {
            [Monday] = Day(Monday, (MealSlot.Meal1, new SlotEntry { MealId = "big" })),
            [Monday.AddDays(3)] = Day(Monday.AddDays(3), (MealSlot.Meal1, new SlotEntry { MealId = "small" }))
        };

        var changed = PlanMoves.CopyWeek(days, Monday, NextMonday, notBefore: NextMonday.AddDays(2));

        Assert.Equal(NextMonday.AddDays(3), Assert.Single(changed).Date);
    }

    [Fact]
    public async Task Copy_week_saves_only_the_days_that_changed()
    {
        var store = new DayPlanServiceTests.MemoryStore();
        store.Days[Monday] = Day(Monday, (MealSlot.Meal1, new SlotEntry { MealId = "big" }));
        var plan = new DayPlanService(store);

        var written = await plan.CopyWeekAsync("u", Monday, NextMonday);

        Assert.Single(written);
        Assert.Equal(1, store.Saves);
        Assert.Equal("big", store.Days[NextMonday].Slots[MealSlot.Meal1].MealId);
    }
}
