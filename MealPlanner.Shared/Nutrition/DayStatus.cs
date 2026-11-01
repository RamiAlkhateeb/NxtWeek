using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

public enum DayDot { None, Planned, PartlyEaten, AllEaten }

public enum ProteinZone { Low, Aim, High }

/// <summary>Small pure rules behind the day strip, the protein gauge and the servings stepper.</summary>
public static class DayStatus
{
    public const double ProteinAimLow = 30;
    public const double ProteinAimHigh = 40;
    public const double GaugeMax = 50;
    public const double ServingsStep = 0.5;
    public const double ServingsMin = 0.5;
    public const double ServingsMax = 6;

    /// <summary>The dot under a day chip, counting only the slots that day has.</summary>
    public static DayDot DotFor(PlanDay? day, DayType type, NutritionProfile? profile) =>
        DotFor(day, type, profile?.MealTimes, profile?.OffSlots);

    public static DayDot DotFor(PlanDay? day, DayType type, IReadOnlyDictionary<MealSlot, string>? mealTimes = null,
        IReadOnlyCollection<MealSlot>? off = null)
    {
        if (day is null) return DayDot.None;
        var entries = DayMath.SlotsFor(type, mealTimes, off).Where(day.Slots.ContainsKey).Select(s => day.Slots[s]).ToList();
        if (entries.Count == 0) return DayDot.None;
        if (entries.All(e => e.Eaten)) return DayDot.AllEaten;
        return entries.Any(e => e.Eaten) ? DayDot.PartlyEaten : DayDot.Planned;
    }

    /// <summary>Aim for 30–40 g protein per meal.</summary>
    public static ProteinZone ZoneFor(double proteinGrams) =>
        proteinGrams < ProteinAimLow ? ProteinZone.Low : proteinGrams > ProteinAimHigh ? ProteinZone.High : ProteinZone.Aim;

    /// <summary>Where the marker sits on the 0–50 g scale, in percent.</summary>
    public static double GaugePercent(double proteinGrams) => Math.Clamp(proteinGrams / GaugeMax * 100, 0, 100);

    public static double StepServings(double current, int direction) =>
        Math.Clamp(Math.Round((current + direction * ServingsStep) * 2) / 2, ServingsMin, ServingsMax);

    /// <summary>Percent of target, clamped to 0–100, for bars and rings.</summary>
    public static double Percent(double value, double target) => target > 0 ? Math.Clamp(value / target * 100, 0, 100) : 0;
}

/// <summary>Where a meal goes when the user did not pick a slot.</summary>
public static class SlotPicker
{
    // A dish that says nothing about where it fits is a main meal, not a post-workout shake or a snack.
    private static readonly MealSlot[] MainMeals = [MealSlot.Meal1, MealSlot.Dinner];

    /// <summary>
    /// The first empty slot (by time) the meal suits — main meals for a meal without suitable slots — else any
    /// empty slot; null when the day has none. Never a slot that is turned off.
    /// </summary>
    public static MealSlot? FreeSlotFor(PlanDay? day, DayType type, NutritionProfile? profile, MealCatalogItem? meal) =>
        FreeSlotFor(day, type, profile?.MealTimes, meal, profile?.OffSlots);

    public static MealSlot? FreeSlotFor(PlanDay? day, DayType type, IReadOnlyDictionary<MealSlot, string>? mealTimes, MealCatalogItem? meal,
        IReadOnlyCollection<MealSlot>? off = null)
    {
        var empty = DayMath.EmptySlots(day, type, mealTimes, off);
        if (empty.Count == 0) return null;
        IReadOnlyCollection<MealSlot> suits = meal is { SuitableSlots.Count: > 0 } ? meal.SuitableSlots : MainMeals;
        foreach (var slot in empty)
            if (suits.Contains(slot)) return slot;
        return empty[0];
    }

    /// <summary>Like <see cref="FreeSlotFor(PlanDay?, DayType, NutritionProfile?, MealCatalogItem?)"/>, but Meal 1 when the day is full.</summary>
    public static MealSlot FirstEmptyFor(PlanDay? day, DayType type, NutritionProfile? profile, MealCatalogItem? meal) =>
        FreeSlotFor(day, type, profile, meal) ?? MealSlot.Meal1;

    public static MealSlot FirstEmptyFor(PlanDay? day, DayType type, IReadOnlyDictionary<MealSlot, string>? mealTimes, MealCatalogItem? meal,
        IReadOnlyCollection<MealSlot>? off = null) =>
        FreeSlotFor(day, type, mealTimes, meal, off) ?? MealSlot.Meal1;
}
