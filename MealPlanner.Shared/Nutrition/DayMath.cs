using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

public readonly record struct MacroTotals(double Kcal, double Protein, double Carbs, double Fat, double Fibre)
{
    public static MacroTotals Zero => default;

    public MacroTotals Add(MacroTotals other) =>
        new(Kcal + other.Kcal, Protein + other.Protein, Carbs + other.Carbs, Fat + other.Fat, Fibre + other.Fibre);

    public static MacroTotals From(NutritionInfo n) => new(n.Kcal, n.Protein, n.Carbs, n.Fat, n.Fibre);
}

/// <summary>Nutrition of one slot entry. <see cref="Missing"/> means the meal has no nutrition yet.</summary>
public readonly record struct EntryNutrition(MacroTotals Totals, bool Missing, string? Source);

/// <summary>
/// One day's sums. Open = target − eaten − planned (what is still free to plan);
/// Left = target − eaten (what the "left today" labels show).
/// </summary>
public sealed record DayTotals(MacroTotals Eaten, MacroTotals Planned, DailyTargets Targets)
{
    public double OpenKcal => Targets.Kcal - Eaten.Kcal - Planned.Kcal;
    public double OpenProtein => Targets.Protein - Eaten.Protein - Planned.Protein;
    public double LeftKcal => Targets.Kcal - Eaten.Kcal;
    public double LeftProtein => Targets.Protein - Eaten.Protein;
}

/// <summary>Pure per-day maths: which slots a day has, and what it adds up to.</summary>
public static class DayMath
{
    private static readonly MealSlot[] TrainingSlots = [MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner, MealSlot.Snack];
    private static readonly MealSlot[] RestSlots = [MealSlot.Meal1, MealSlot.Dinner, MealSlot.Snack];

    /// <summary>A day's type: what was set on that day, else the profile's training days.</summary>
    public static DayType TypeOf(PlanDay? day, DateOnly date, NutritionProfile profile) =>
        day?.Type ?? (profile.IsTrainingDay(date) ? DayType.Training : DayType.Rest);

    /// <summary>Slots for a day type, ordered by the user's meal times. Rest days have no post-workout slot.</summary>
    public static IReadOnlyList<MealSlot> SlotsFor(DayType type, IReadOnlyDictionary<MealSlot, string>? mealTimes = null)
    {
        var times = mealTimes ?? NutritionProfile.DefaultMealTimes();
        var slots = type == DayType.Training ? TrainingSlots : RestSlots;
        return slots
            .Select((slot, index) => (slot, index, time: times.TryGetValue(slot, out var t) ? t : ""))
            .OrderBy(x => x.time, StringComparer.Ordinal)
            .ThenBy(x => x.index)
            .Select(x => x.slot)
            .ToList();
    }

    public static EntryNutrition Of(SlotEntry entry, Func<string, NutritionInfo?> nutritionFor)
    {
        var servings = entry.Servings;
        if (entry.Quick is { } q)
            return new EntryNutrition(new MacroTotals(q.Kcal * servings, q.Protein * servings, q.Carbs * servings, q.Fat * servings, 0), false, NutritionSource.Manual);

        var n = string.IsNullOrEmpty(entry.MealId) ? null : nutritionFor(entry.MealId);
        if (n is null) return new EntryNutrition(MacroTotals.Zero, true, null);
        return new EntryNutrition(MacroTotals.From(n.Scale(servings)), false, n.Source);
    }

    public static DayTotals Totals(PlanDay? day, DayType type, DailyTargets targets, Func<string, NutritionInfo?> nutritionFor,
        IReadOnlyDictionary<MealSlot, string>? mealTimes = null)
    {
        var eaten = MacroTotals.Zero;
        var planned = MacroTotals.Zero;
        if (day is not null)
        {
            foreach (var slot in SlotsFor(type, mealTimes))
            {
                if (!day.Slots.TryGetValue(slot, out var entry)) continue;
                var n = Of(entry, nutritionFor).Totals;
                if (entry.Eaten) eaten = eaten.Add(n); else planned = planned.Add(n);
            }
        }
        return new DayTotals(eaten, planned, targets);
    }

    public static IReadOnlyList<MealSlot> EmptySlots(PlanDay? day, DayType type, IReadOnlyDictionary<MealSlot, string>? mealTimes = null) =>
        SlotsFor(type, mealTimes).Where(s => day is null || !day.Slots.ContainsKey(s)).ToList();

    /// <summary>
    /// Days saved before slots existed held one meal at users/{key}/weeklyPlan/{date}. Until the
    /// day is saved in the new shape, that meal shows as the day's first meal (lunch).
    /// </summary>
    public static PlanDay FromLegacy(WeeklyPlanEntry legacy) => new()
    {
        Date = legacy.Date,
        Slots = string.IsNullOrWhiteSpace(legacy.MealId)
            ? new()
            : new() { [MealSlot.Meal1] = new SlotEntry { MealId = legacy.MealId } }
    };
}
