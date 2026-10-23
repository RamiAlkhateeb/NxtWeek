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
    PostWorkout,
    Dinner,
    Snack
}

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
    public bool Leftover { get; set; }
    // Cook this many batches for the shopping list; does not change what is eaten.
    public int PrepBatches { get; set; } = 1;

    [JsonIgnore]
    public bool IsQuick => Quick is not null;
}

/// <summary>One planned day, stored at users/{key}/days/{yyyy-MM-dd}.</summary>
public class PlanDay
{
    public DateOnly Date { get; set; }
    // Null until the user flips the day; the profile's training days decide until then.
    public DayType? Type { get; set; }
    public Dictionary<MealSlot, SlotEntry> Slots { get; set; } = new();
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

/// <summary>Per-field target overrides for one day type. Null means "calculated".</summary>
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
    public TargetOverride Training { get; set; } = new();
    public TargetOverride Rest { get; set; } = new();

    public TargetOverride For(DayType type) => type == DayType.Rest ? Rest : Training;
}

/// <summary>Stored at users/{key}/nutritionProfile. Requires a signed-in account.</summary>
public class NutritionProfile
{
    public Sex Sex { get; set; } = Sex.Male;
    public int Age { get; set; } = 31;
    public double HeightCm { get; set; } = 175;
    public double WeightKg { get; set; } = 69;
    // 0 sedentary, 1 light, 2 moderate, 3 very active.
    public int Activity { get; set; } = 1;
    public Goal Goal { get; set; } = Goal.Lose;
    // Monday first, like the week view.
    public List<bool> TrainingDays { get; set; } = [true, false, true, false, true, false, false];
    // "HH:mm" per slot.
    public Dictionary<MealSlot, string> MealTimes { get; set; } = DefaultMealTimes();
    public TargetOverrides Overrides { get; set; } = new();
    public int WeeklyAdjustKcal { get; set; }

    public static Dictionary<MealSlot, string> DefaultMealTimes() => new()
    {
        [MealSlot.Meal1] = "13:00",
        [MealSlot.PostWorkout] = "17:30",
        [MealSlot.Dinner] = "19:00",
        [MealSlot.Snack] = "21:00"
    };

    public bool IsTrainingDay(DateOnly date)
    {
        var index = ((int)date.DayOfWeek + 6) % 7; // Monday = 0
        return index < TrainingDays.Count && TrainingDays[index];
    }
}

/// <summary>One body measurement day, stored at users/{key}/body/{yyyy-MM-dd}.</summary>
public class BodyLog
{
    public DateOnly Date { get; set; }
    public double? Kg { get; set; }
    public double? WaistCm { get; set; }
}
