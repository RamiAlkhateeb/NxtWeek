using MealPlanner.Shared.Services;

namespace MealPlanner.Tests;

public class DayPlanServiceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 19);

    /// <summary>Behaves like FirebaseNutritionStore: legacy entries read as Meal 1 until the day is saved.</summary>
    private sealed class MemoryStore : INutritionStore
    {
        public Dictionary<DateOnly, PlanDay> Days { get; } = new();
        public Dictionary<DateOnly, WeeklyPlanEntry> Legacy { get; } = new();
        public int Saves { get; private set; }

        public Task<Dictionary<DateOnly, PlanDay>> GetDaysAsync(string uid, DateOnly start, DateOnly end)
        {
            var result = new Dictionary<DateOnly, PlanDay>();
            foreach (var (date, legacy) in Legacy) if (date >= start && date <= end) result[date] = DayMath.FromLegacy(legacy);
            foreach (var (date, day) in Days) if (date >= start && date <= end) result[date] = Clone(day);
            return Task.FromResult(result);
        }

        public Task SaveDayAsync(string uid, PlanDay day) { Days[day.Date] = Clone(day); Saves++; return Task.CompletedTask; }

        private static PlanDay Clone(PlanDay d) => System.Text.Json.JsonSerializer.Deserialize<PlanDay>(System.Text.Json.JsonSerializer.Serialize(d))!;

        public Task<NutritionProfile?> GetProfileAsync(string uid) => Task.FromResult<NutritionProfile?>(null);
        public Task SaveProfileAsync(string uid, NutritionProfile profile) => Task.CompletedTask;
        public Task<List<BodyLog>> GetBodyLogsAsync(string uid) => Task.FromResult(new List<BodyLog>());
        public Task SaveBodyLogAsync(string uid, BodyLog log) => Task.CompletedTask;
    }

    [Fact]
    public async Task Editing_a_legacy_day_keeps_its_meal_as_meal_1()
    {
        var store = new MemoryStore();
        store.Legacy[Monday] = new WeeklyPlanEntry { Date = Monday, MealId = "koshari" };
        var plan = new DayPlanService(store);

        await plan.SetEntryAsync("u", Monday, MealSlot.Snack, new SlotEntry { MealId = "shake" });

        var saved = store.Days[Monday];
        Assert.Equal("koshari", saved.Slots[MealSlot.Meal1].MealId);
        Assert.Equal("shake", saved.Slots[MealSlot.Snack].MealId);
    }

    [Fact]
    public async Task Removing_one_slot_keeps_the_others()
    {
        var store = new MemoryStore();
        var plan = new DayPlanService(store);
        await plan.SetEntryAsync("u", Monday, MealSlot.Meal1, new SlotEntry { MealId = "a" });
        await plan.SetEntryAsync("u", Monday, MealSlot.Dinner, new SlotEntry { MealId = "b" });

        await plan.RemoveEntryAsync("u", Monday, MealSlot.Meal1);

        Assert.Equal("b", Assert.Single(store.Days[Monday].Slots).Value.MealId);
    }

    [Fact]
    public async Task Removing_the_legacy_meal_hides_it_for_good()
    {
        var store = new MemoryStore();
        store.Legacy[Monday] = new WeeklyPlanEntry { Date = Monday, MealId = "koshari" };
        var plan = new DayPlanService(store);

        await plan.RemoveEntryAsync("u", Monday, MealSlot.Meal1);

        Assert.Empty((await plan.GetDaysAsync("u", Monday, Monday))[Monday].Slots);
    }

    [Fact]
    public async Task Update_changes_an_entry_in_place()
    {
        var store = new MemoryStore();
        var plan = new DayPlanService(store);
        await plan.SetEntryAsync("u", Monday, MealSlot.Meal1, new SlotEntry { MealId = "a" });

        await plan.UpdateEntryAsync("u", Monday, MealSlot.Meal1, e => { e.Eaten = true; e.Servings = 1.5; });

        var entry = store.Days[Monday].Slots[MealSlot.Meal1];
        Assert.True(entry.Eaten);
        Assert.Equal(1.5, entry.Servings);
    }

    [Fact]
    public async Task Day_type_is_stored_on_the_day()
    {
        var store = new MemoryStore();
        var plan = new DayPlanService(store);

        await plan.SetDayTypeAsync("u", Monday, DayType.Rest);

        Assert.Equal(DayType.Rest, store.Days[Monday].Type);
    }

    [Fact]
    public void First_empty_slot_skips_filled_ones_and_respects_suitable_slots()
    {
        var day = new PlanDay { Slots = { [MealSlot.Meal1] = new SlotEntry { MealId = "a" } } };
        var shake = new MealCatalogItem { SuitableSlots = [MealSlot.PostWorkout, MealSlot.Snack] };

        Assert.Equal(MealSlot.PostWorkout, SlotPicker.FirstEmptyFor(day, DayType.Training, null, null));
        Assert.Equal(MealSlot.Dinner, SlotPicker.FirstEmptyFor(day, DayType.Rest, null, null));
        Assert.Equal(MealSlot.Snack, SlotPicker.FirstEmptyFor(day, DayType.Rest, null, shake));
    }

    [Fact]
    public void A_full_day_falls_back_to_meal_1()
    {
        var day = new PlanDay();
        foreach (var s in Enum.GetValues<MealSlot>()) day.Slots[s] = new SlotEntry { MealId = "a" };

        Assert.Equal(MealSlot.Meal1, SlotPicker.FirstEmptyFor(day, DayType.Training, null, null));
    }
}
