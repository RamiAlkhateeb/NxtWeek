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
    public static DayDot DotFor(PlanDay? day, DayType type, IReadOnlyDictionary<MealSlot, string>? mealTimes = null)
    {
        if (day is null) return DayDot.None;
        var entries = DayMath.SlotsFor(type, mealTimes).Where(day.Slots.ContainsKey).Select(s => day.Slots[s]).ToList();
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
    /// <summary>The first empty slot (by time) the meal suits; any empty slot if none suits; Meal 1 when the day is full.</summary>
    public static MealSlot FirstEmptyFor(PlanDay? day, DayType type, IReadOnlyDictionary<MealSlot, string>? mealTimes, MealCatalogItem? meal)
    {
        var empty = DayMath.EmptySlots(day, type, mealTimes);
        if (empty.Count == 0) return MealSlot.Meal1;
        if (meal is { SuitableSlots.Count: > 0 })
        {
            foreach (var slot in empty)
                if (meal.SuitableSlots.Contains(slot)) return slot;
        }
        return empty[0];
    }
}
