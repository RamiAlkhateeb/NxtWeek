using MealPlanner.Shared.Services;

namespace MealPlanner.Tests;

public class DayPlanServiceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 19);

    /// <summary>Behaves like FirebaseNutritionStore: legacy entries read as Meal 1 until the day is saved.</summary>
    internal sealed class MemoryStore : INutritionStore
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
        public MealPrefs Prefs { get; set; } = new();
        public Task<MealPrefs> GetMealPrefsAsync(string uid) => Task.FromResult(Prefs);
        public Task SaveMealPrefsAsync(string uid, MealPrefs prefs) { Prefs = prefs; return Task.CompletedTask; }
        public Task<List<BodyLog>> GetBodyLogsAsync(string uid) => Task.FromResult(new List<BodyLog>());
        public Task SaveBodyLogAsync(string uid, BodyLog log) => Task.CompletedTask;
        public Task<Dictionary<DateOnly, string>> GetProgressPhotosAsync(string uid) => Task.FromResult(new Dictionary<DateOnly, string>());
        public Task SaveProgressPhotoAsync(string uid, DateOnly monday, string dataUrl) => Task.CompletedTask;
        public SharedReview? Shared;
        public Task<SharedReview?> GetSharedReviewAsync(string uid) => Task.FromResult(Shared);
        public Task ShareReviewAsync(string uid, SharedReview review) { Shared = review; return Task.CompletedTask; }
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

    private static MealPrefs AllFour() => new() { On = [MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner, MealSlot.Snack] };

    [Fact]
    public async Task A_day_saved_before_3_1_keeps_its_day_type_when_edited()
    {
        var store = new MemoryStore();
        store.Days[Monday] = new PlanDay { Date = Monday, Type = DayType.Rest };
        var plan = new DayPlanService(store);

        await plan.SetEntryAsync("u", Monday, MealSlot.Meal1, new SlotEntry { MealId = "a" });

        Assert.Equal(DayType.Rest, store.Days[Monday].Type);
    }

    [Fact]
    public async Task Assigning_without_a_slot_uses_the_users_meals()
    {
        var store = new MemoryStore { Prefs = new MealPrefs { On = [MealSlot.Meal1, MealSlot.Dinner] } };
        var assign = new MealAssignService(new DayPlanService(store), store);
        var meal = new MealCatalogItem { Id = "a" };

        Assert.Equal(MealSlot.Meal1, await assign.AssignAsync("u", Monday, meal));
        Assert.Equal(MealSlot.Dinner, await assign.AssignAsync("u", Monday, new MealCatalogItem { Id = "b" }));
        Assert.Null(await assign.FindFreeSlotAsync("u", Monday, new MealCatalogItem { Id = "c" }));
    }

    [Fact]
    public async Task Moving_a_meal_to_another_day_saves_both_days()
    {
        var store = new MemoryStore();
        var plan = new DayPlanService(store);
        await plan.SetEntryAsync("u", Monday, MealSlot.Dinner, new SlotEntry { MealId = "a", Servings = 2 });
        await plan.SetEntryAsync("u", Monday.AddDays(1), MealSlot.Dinner, new SlotEntry { MealId = "b" });

        var changed = await plan.MoveEntryAsync("u", Monday, MealSlot.Dinner, Monday.AddDays(1), MealSlot.Dinner);

        Assert.Equal(2, changed.Count);
        Assert.Equal("b", store.Days[Monday].Slots[MealSlot.Dinner].MealId);
        Assert.Equal("a", store.Days[Monday.AddDays(1)].Slots[MealSlot.Dinner].MealId);
        Assert.Equal(2, store.Days[Monday.AddDays(1)].Slots[MealSlot.Dinner].Servings);
    }

    [Fact]
    public async Task Moving_within_a_day_and_copying_save_one_day()
    {
        var store = new MemoryStore();
        var plan = new DayPlanService(store);
        await plan.SetEntryAsync("u", Monday, MealSlot.Meal1, new SlotEntry { MealId = "a" });

        Assert.Single(await plan.MoveEntryAsync("u", Monday, MealSlot.Meal1, Monday, MealSlot.Dinner));
        Assert.Equal("a", store.Days[Monday].Slots[MealSlot.Dinner].MealId);
        Assert.False(store.Days[Monday].Slots.ContainsKey(MealSlot.Meal1));

        var copy = await plan.CopyEntryAsync("u", Monday, MealSlot.Dinner, Monday.AddDays(3), MealSlot.Meal1);
        Assert.Equal("a", copy!.Slots[MealSlot.Meal1].MealId);
        Assert.Equal("a", store.Days[Monday].Slots[MealSlot.Dinner].MealId);

        Assert.Empty(await plan.MoveEntryAsync("u", Monday, MealSlot.Snack, Monday.AddDays(1), MealSlot.Snack));
        Assert.Null(await plan.CopyEntryAsync("u", Monday, MealSlot.Snack, Monday.AddDays(1), MealSlot.Snack));
    }

    [Fact]
    public void First_empty_slot_skips_filled_ones_and_respects_suitable_slots()
    {
        var day = new PlanDay { Slots = { [MealSlot.Meal1] = new SlotEntry { MealId = "a" } } };
        var shake = new MealCatalogItem { SuitableSlots = [MealSlot.PostWorkout, MealSlot.Snack] };

        // A dish that says nothing about where it fits is a main meal: Dinner, not a snack.
        Assert.Equal(MealSlot.Dinner, SlotPicker.FirstEmptyFor(day, AllFour(), null));
        Assert.Equal(MealSlot.PostWorkout, SlotPicker.FirstEmptyFor(day, AllFour(), shake));
        Assert.Equal(MealSlot.Dinner, SlotPicker.FirstEmptyFor(day, null, null));
        Assert.Equal(MealSlot.Snack, SlotPicker.FirstEmptyFor(day, null, shake));
    }

    [Fact]
    public void A_meal_is_never_placed_in_a_slot_that_is_off()
    {
        var prefs = new MealPrefs { On = [MealSlot.Meal1, MealSlot.Dinner] };
        var shake = new MealCatalogItem { SuitableSlots = [MealSlot.PostWorkout, MealSlot.Snack] };

        Assert.Equal(MealSlot.Meal1, SlotPicker.FirstEmptyFor(new PlanDay(), prefs, shake));
        var day = new PlanDay { Slots = { [MealSlot.Meal1] = new SlotEntry { MealId = "a" } } };
        Assert.Equal(MealSlot.Dinner, SlotPicker.FirstEmptyFor(day, prefs, shake));
    }

    [Fact]
    public void Only_open_non_main_meals_left_still_take_a_dish()
    {
        var day = new PlanDay { Slots = { [MealSlot.Meal1] = new SlotEntry { MealId = "a" }, [MealSlot.Dinner] = new SlotEntry { MealId = "b" } } };

        Assert.Equal(MealSlot.PostWorkout, SlotPicker.FreeSlotFor(day, AllFour(), null));
    }

    [Fact]
    public void A_full_day_has_no_free_slot()
    {
        var day = new PlanDay();
        foreach (var s in Enum.GetValues<MealSlot>()) day.Slots[s] = new SlotEntry { MealId = "a" };

        Assert.Null(SlotPicker.FreeSlotFor(day, AllFour(), null));
        var oneMeal = new MealPrefs { On = [MealSlot.Meal1] };
        Assert.Null(SlotPicker.FreeSlotFor(new PlanDay { Slots = { [MealSlot.Meal1] = new SlotEntry { MealId = "a" } } }, oneMeal, null));
    }

    [Fact]
    public void A_full_day_falls_back_to_meal_1()
    {
        var day = new PlanDay();
        foreach (var s in Enum.GetValues<MealSlot>()) day.Slots[s] = new SlotEntry { MealId = "a" };

        Assert.Equal(MealSlot.Meal1, SlotPicker.FirstEmptyFor(day, AllFour(), null));
    }
}
