using System.Text.Json.Serialization;

namespace MealPlanner.Shared.Models;

public static class NutritionSource
{
    // An AI/ingredient estimate. Never shown without its "Estimate" badge.
    public const string Estimate = "est";
    // Typed in by the user ("Entered by you").
    public const string Manual = "man";
}

/// <summary>Nutrition for one serving of a meal.</summary>
public class NutritionInfo
{
    public double Kcal { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public double Fibre { get; set; }
    public string Source { get; set; } = NutritionSource.Manual;

    [JsonIgnore]
    public bool IsEstimate => Source == NutritionSource.Estimate;

    public NutritionInfo Scale(double factor) => new()
    {
        Kcal = Kcal * factor,
        Protein = Protein * factor,
        Carbs = Carbs * factor,
        Fat = Fat * factor,
        Fibre = Fibre * factor,
        Source = Source
    };
}

public static class IngredientUnit
{
    public const string Grams = "g";
    public const string Millilitres = "ml";
    public const string Pieces = "pc";
}

/// <summary>A measured ingredient, per eaten serving. Feeds the shopping list.</summary>
public class IngredientLine
{
    // Stable across languages ("chickenBreast"), so the shopping list can merge the same item.
    public string Id { get; set; } = string.Empty;
    // In the meal's own language, like the meal name.
    public string Name { get; set; } = string.Empty;
    public double QtyPerServing { get; set; }
    public string Unit { get; set; } = IngredientUnit.Grams;
    public string Aisle { get; set; } = "other";
}

/// <summary>Something eaten that is not a catalog meal (a handful of nuts, a bar).</summary>
public class QuickAddEntry
{
    public string Name { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public double Kcal { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<MealSlot>))]
public enum MealSlot
{
    Meal1,
    // The optional afternoon snack. It keeps its first name because that is what the stored days say.
    PostWorkout,
    Dinner,
    Snack
}

/// <summary>Only read from days saved before 3.1; Makdous no longer has training or rest days.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DayType>))]
public enum DayType
{
    Training,
    Rest
}

/// <summary>What sits in one slot of a day: a catalog meal or a quick add.</summary>
public class SlotEntry
{
    public string? MealId { get; set; }
    public QuickAddEntry? Quick { get; set; }
    public double Servings { get; set; } = 1;
    public bool Eaten { get; set; }
    // "Did you eat this?" answered No. A skipped meal counts nowhere.
    public bool Skipped { get; set; }
    public bool Leftover { get; set; }
    // Cook this many batches for the shopping list; does not change what is eaten.
    public int PrepBatches { get; set; } = 1;

    /// <summary>A copy that can be changed without touching this entry.</summary>
    public SlotEntry Clone() => (SlotEntry)MemberwiseClone();

    [JsonIgnore]
    public bool IsQuick => Quick is not null;
}

/// <summary>One planned day, stored at users/{key}/days/{yyyy-MM-dd}.</summary>
public class PlanDay
{
    public DateOnly Date { get; set; }
    // Legacy (before 3.1): kept so old days load and save unchanged; nothing reads it.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DayType? Type { get; set; }
    public Dictionary<MealSlot, SlotEntry> Slots { get; set; } = new();
    /// <summary>Glasses of water drunk that day (personal: never mirrored to a sync partner).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Water { get; set; }

    /// <summary>A deep copy: changing its entries leaves this day as it was.</summary>
    public PlanDay Clone() => new() { Date = Date, Type = Type, Water = Water, Slots = Slots.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()) };
}

[JsonConverter(typeof(JsonStringEnumConverter<Sex>))]
public enum Sex
{
    Male,
    Female
}

[JsonConverter(typeof(JsonStringEnumConverter<Goal>))]
public enum Goal
{
    Lose,
    Keep,
    Gain
}

/// <summary>Per-field target overrides. Null means "calculated".</summary>
public class TargetOverride
{
    public int? Kcal { get; set; }
    public int? Protein { get; set; }
    public int? Carbs { get; set; }
    public int? Fat { get; set; }

    [JsonIgnore]
    public bool IsEmpty => Kcal is null && Protein is null && Carbs is null && Fat is null;
}

public class TargetOverrides
{
    // The one set of overrides. Stored as "training" since before 3.1, when rest days had their own.
    public TargetOverride Training { get; set; } = new();

    [JsonIgnore]
    public TargetOverride Daily => Training;
}

/// <summary>
/// Body data and goal, stored at users/{key}/nutritionProfile. Health data: it needs a signed-in
/// account and never goes to localStorage. Meal times and other preferences live in <see cref="MealPrefs"/>.
/// </summary>
public class NutritionProfile
{
    public Sex Sex { get; set; } = Sex.Male;
    public int Age { get; set; } = 31;
    public double HeightCm { get; set; } = 175;
    public double WeightKg { get; set; } = 69;
    // Daily movement: 0 mostly sitting, 1 lightly active, 2 active, 3 very active.
    public int Activity { get; set; } = 1;
    public Goal Goal { get; set; } = Goal.Lose;
    public TargetOverrides Overrides { get; set; } = new();
    public int WeeklyAdjustKcal { get; set; }

    // Legacy (before 3.1): read once to build MealPrefs, never written again.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<MealSlot, string>? MealTimes { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<MealSlot>? OffSlots { get; set; }
}

/// <summary>
/// Which meals a day has and when, plus display preferences. Stored at users/{key}/mealPrefs.
/// Not health data, so guests have it too.
/// </summary>
public class MealPrefs
{
    // "HH:mm" per slot.
    public Dictionary<MealSlot, string> Times { get; set; } = DefaultTimes();
    // Meal 1 is always on, whatever this list says.
    public List<MealSlot> On { get; set; } = [MealSlot.Meal1, MealSlot.Dinner, MealSlot.Snack];
    public bool TrackMacros { get; set; }
    public bool Ramadan { get; set; }
    // Suhoor (Meal 1), iftar (Dinner) and the late snack; the usual times come back when Ramadan mode is off.
    public Dictionary<MealSlot, string> RamadanTimes { get; set; } = DefaultRamadanTimes();
    // The Monday of the last Sunday review the user opened, so its banner goes away.
    public DateOnly? ReviewSeen { get; set; }

    public static Dictionary<MealSlot, string> DefaultTimes() => new()
    {
        [MealSlot.Meal1] = "13:00",
        [MealSlot.PostWorkout] = "16:30",
        [MealSlot.Dinner] = "19:00",
        [MealSlot.Snack] = "21:00"
    };

    public static Dictionary<MealSlot, string> DefaultRamadanTimes() => new()
    {
        [MealSlot.Meal1] = "04:30",
        [MealSlot.Dinner] = "18:30",
        [MealSlot.Snack] = "22:00"
    };

    public bool IsOn(MealSlot slot) => slot == MealSlot.Meal1 || (On?.Contains(slot) ?? false);

    /// <summary>In Ramadan mode Meal 1, Dinner and the evening snack become suhoor, iftar and the late snack.</summary>
    public bool IsRamadanSlot(MealSlot slot) => Ramadan && slot is MealSlot.Meal1 or MealSlot.Dinner or MealSlot.Snack;

    /// <summary>The translation key of the slot's name: "nut_ramadan_*" for a Ramadan slot, else "nut_slot_*".</summary>
    public string NameKey(MealSlot slot) => (IsRamadanSlot(slot) ? "nut_ramadan_" : "nut_slot_") + slot;

    public void SetOn(MealSlot slot, bool on)
    {
        if (slot == MealSlot.Meal1) return;
        On ??= new();
        On.Remove(slot);
        if (on) On.Add(slot);
    }

    /// <summary>The slot's time today: its Ramadan time when Ramadan mode is on and it has one.</summary>
    public string TimeOf(MealSlot slot)
    {
        if (IsRamadanSlot(slot) && RamadanTimes is not null && RamadanTimes.TryGetValue(slot, out var r) && !string.IsNullOrEmpty(r)) return r;
        return Times is not null && Times.TryGetValue(slot, out var t) ? t : "";
    }

    /// <summary>Fills what Firebase dropped (empty lists and maps come back as null).</summary>
    public MealPrefs Normalize()
    {
        Times ??= DefaultTimes();
        foreach (var (slot, time) in DefaultTimes()) Times.TryAdd(slot, time);
        RamadanTimes ??= DefaultRamadanTimes();
        foreach (var (slot, time) in DefaultRamadanTimes()) RamadanTimes.TryAdd(slot, time);
        On ??= new();
        if (!On.Contains(MealSlot.Meal1)) On.Insert(0, MealSlot.Meal1);
        return this;
    }

    /// <summary>
    /// Preferences for someone who set meal times before 3.1: their times and switched-off meals carry over.
    /// The old post-workout meal becomes the optional afternoon snack, on only when a meal is still planned in it.
    /// </summary>
    public static MealPrefs FromLegacy(NutritionProfile? profile, bool hasUpcomingAfternoonMeal)
    {
        var prefs = new MealPrefs();
        if (profile?.MealTimes is { } times)
            foreach (var (slot, time) in times)
                if (!string.IsNullOrWhiteSpace(time)) prefs.Times[slot] = time;
        var off = profile?.OffSlots ?? [];
        prefs.On = [MealSlot.Meal1];
        if (hasUpcomingAfternoonMeal && !off.Contains(MealSlot.PostWorkout)) prefs.On.Add(MealSlot.PostWorkout);
        if (!off.Contains(MealSlot.Dinner)) prefs.On.Add(MealSlot.Dinner);
        if (!off.Contains(MealSlot.Snack)) prefs.On.Add(MealSlot.Snack);
        return prefs;
    }
}

/// <summary>
/// What the user chose to share of a week with friends from the Sunday review: how many days were logged and the
/// most eaten meal. Never calories or weight. Stored at users/{key}/sharedReview.
/// </summary>
public class SharedReview
{
    public DateOnly Monday { get; set; }
    public int DaysLogged { get; set; }
    public string? TopMealId { get; set; }
    public string? TopMealName { get; set; }
}

/// <summary>One body measurement day, stored at users/{key}/body/{yyyy-MM-dd}.</summary>
public class BodyLog
{
    public DateOnly Date { get; set; }
    public double? Kg { get; set; }
    public double? WaistCm { get; set; }
}
