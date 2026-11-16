using MealPlanner.Shared.Services;

namespace MealPlanner.Tests;

public class SyncMergeTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);

    private static PlanDay Day(DateOnly date, params (MealSlot Slot, SlotEntry Entry)[] slots)
    {
        var day = new PlanDay { Date = date };
        foreach (var (slot, entry) in slots) day.Slots[slot] = entry;
        return day;
    }

    [Fact]
    public void Mirror_adds_a_new_meal_with_one_serving_and_nothing_personal()
    {
        var before = Day(Today);
        var after = Day(Today, (MealSlot.Dinner, new SlotEntry { MealId = "kofta", Servings = 3, PrepBatches = 2 }));

        var partner = SyncMerge.Mirror(before, after, null, Today, Today)!;

        var entry = partner.Slots[MealSlot.Dinner];
        Assert.Equal("kofta", entry.MealId);
        Assert.Equal(1, entry.Servings);
        Assert.Equal(1, entry.PrepBatches);
        Assert.Equal(Today, partner.Date);
    }

    [Fact]
    public void Mirror_swaps_the_meal_but_keeps_the_partners_servings()
    {
        var before = Day(Today, (MealSlot.Dinner, new SlotEntry { MealId = "kofta" }));
        var after = Day(Today, (MealSlot.Dinner, new SlotEntry { MealId = "soup" }));
        var partner = Day(Today, (MealSlot.Dinner, new SlotEntry { MealId = "kofta", Servings = 2 }));

        var mirrored = SyncMerge.Mirror(before, after, partner, Today, Today)!;

        Assert.Equal("soup", mirrored.Slots[MealSlot.Dinner].MealId);
        Assert.Equal(2, mirrored.Slots[MealSlot.Dinner].Servings);
        Assert.Equal("kofta", partner.Slots[MealSlot.Dinner].MealId); // the input is not changed
    }

    [Fact]
    public void Mirror_never_touches_answered_entries_quick_adds_or_past_days()
    {
        var before = Day(Today, (MealSlot.Dinner, new SlotEntry { MealId = "kofta" }), (MealSlot.Snack, new SlotEntry { MealId = "shake" }));
        var after = Day(Today);
        var partner = Day(Today,
            (MealSlot.Dinner, new SlotEntry { MealId = "kofta", Eaten = true }),
            (MealSlot.Snack, new SlotEntry { Quick = new QuickAddEntry { Name = "Nuts" } }));

        Assert.Null(SyncMerge.Mirror(before, after, partner, Today, Today));
        Assert.Null(SyncMerge.Mirror(Day(Today.AddDays(-1)), Day(Today.AddDays(-1), (MealSlot.Meal1, new SlotEntry { MealId = "a" })), null, Today.AddDays(-1), Today));
    }

    [Fact]
    public void Answering_or_changing_servings_mirrors_nothing()
    {
        var before = Day(Today, (MealSlot.Dinner, new SlotEntry { MealId = "kofta" }));
        var after = Day(Today, (MealSlot.Dinner, new SlotEntry { MealId = "kofta", Eaten = true, Servings = 2 }));

        Assert.False(SyncMerge.MealsChanged(before, after));
        Assert.Null(SyncMerge.Mirror(before, after, null, Today, Today));
    }

    [Fact]
    public void Adopt_takes_over_the_requesters_meals_but_keeps_my_history_and_quick_adds()
    {
        var theirs = new Dictionary<DateOnly, PlanDay>
        {
            [Today] = Day(Today, (MealSlot.Meal1, new SlotEntry { MealId = "a" }), (MealSlot.Dinner, new SlotEntry { MealId = "b" })),
            [Today.AddDays(1)] = Day(Today.AddDays(1), (MealSlot.Meal1, new SlotEntry { MealId = "c", Eaten = true }))
        };
        var mine = new Dictionary<DateOnly, PlanDay>
        {
            [Today] = Day(Today, (MealSlot.Meal1, new SlotEntry { MealId = "x", Eaten = true }), (MealSlot.Snack, new SlotEntry { MealId = "y" })),
            [Today.AddDays(2)] = Day(Today.AddDays(2), (MealSlot.Snack, new SlotEntry { Quick = new QuickAddEntry { Name = "Nuts" } }))
        };

        var changed = SyncMerge.Adopt(theirs, mine, Today, Today.AddDays(2));

        Assert.Equal(2, changed.Count);
        var today = changed.Single(d => d.Date == Today);
        Assert.Equal("x", today.Slots[MealSlot.Meal1].MealId);      // eaten: mine stays
        Assert.Equal("b", today.Slots[MealSlot.Dinner].MealId);
        Assert.False(today.Slots.ContainsKey(MealSlot.Snack));        // their slot is empty: mine goes
        Assert.Equal("c", changed.Single(d => d.Date == Today.AddDays(1)).Slots[MealSlot.Meal1].MealId);
        Assert.False(changed.Single(d => d.Date == Today.AddDays(1)).Slots[MealSlot.Meal1].Eaten);
    }

    [Fact]
    public async Task Day_plan_changes_are_mirrored_to_the_sync_partner()
    {
        var store = new DayPlanServiceTests.MemoryStore();
        store.Links["u"] = new SyncLink { Partner = "p", ListHost = "u" };
        store.Others["p"] = new() { [Today] = Day(Today, (MealSlot.Dinner, new SlotEntry { MealId = "old", Servings = 2 })) };
        var plan = new DayPlanService(store, () => Today);

        await plan.SetEntryAsync("u", Today, MealSlot.Dinner, new SlotEntry { MealId = "kofta" });
        await plan.SetEntryAsync("u", Today.AddDays(1), MealSlot.Meal1, new SlotEntry { MealId = "rice" });
        await plan.MoveEntryAsync("u", Today.AddDays(1), MealSlot.Meal1, Today.AddDays(2), MealSlot.Snack);
        await plan.UpdateEntryAsync("u", Today, MealSlot.Dinner, e => e.Eaten = true);

        var p = store.Others["p"];
        Assert.Equal("kofta", p[Today].Slots[MealSlot.Dinner].MealId);
        Assert.Equal(2, p[Today].Slots[MealSlot.Dinner].Servings);
        Assert.False(p[Today].Slots[MealSlot.Dinner].Eaten);
        Assert.Empty(p[Today.AddDays(1)].Slots);
        Assert.Equal("rice", p[Today.AddDays(2)].Slots[MealSlot.Snack].MealId);
    }

    [Fact]
    public async Task Without_a_partner_nothing_is_mirrored()
    {
        var store = new DayPlanServiceTests.MemoryStore();
        store.Others["p"] = new();
        var plan = new DayPlanService(store, () => Today);

        await plan.SetEntryAsync("u", Today, MealSlot.Dinner, new SlotEntry { MealId = "kofta" });

        Assert.Empty(store.Others["p"]);
    }
}

public class ProteinChallengeTests
{
    private static readonly DateOnly Monday = new(2026, 11, 16);
    private static readonly Dictionary<string, NutritionInfo> Meals = new() { ["big"] = new() { Kcal = 700, Protein = 70 } };

    [Fact]
    public void Counts_days_up_to_today_where_eaten_protein_reached_the_target()
    {
        PlanDay Day(int i, int eaten) => new()
        {
            Date = Monday.AddDays(i),
            Slots = Enumerable.Range(0, 2).ToDictionary(n => (MealSlot)n, n => new SlotEntry { MealId = "big", Eaten = n < eaten })
        };
        var days = new Dictionary<DateOnly, PlanDay>
        {
            [Monday] = Day(0, 2),            // 140 g
            [Monday.AddDays(1)] = Day(1, 1), // 70 g: short
            [Monday.AddDays(2)] = Day(2, 2),
            [Monday.AddDays(3)] = Day(3, 2)  // after "today"
        };

        Assert.Equal(2, FriendShare.ProteinDays(days, Monday, Monday.AddDays(2), 135, id => Meals.GetValueOrDefault(id)));
        Assert.Equal(0, FriendShare.ProteinDays(days, Monday, Monday.AddDays(2), 0, id => Meals.GetValueOrDefault(id)));
    }
}
