using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

/// <summary>What a friend may see of someone's day, and what copying their day brings over.</summary>
public static class FriendShare
{
    /// <summary>Calories and protein eaten that day; null when nothing is marked eaten. Nothing else is derived.</summary>
    public static (double Kcal, double Protein)? EatenOn(PlanDay? day, Func<string, NutritionInfo?> nutritionFor)
    {
        if (day is null) return null;
        double kcal = 0, protein = 0;
        var any = false;
        foreach (var entry in day.Slots.Values.Where(e => e.Eaten))
        {
            var n = DayMath.Of(entry, nutritionFor);
            kcal += n.Totals.Kcal;
            protein += n.Totals.Protein;
            any = true;
        }
        return any ? (kcal, protein) : null;
    }

    /// <summary>
    /// The friend's catalog meals for copying into my day: quick adds stay personal, and the copy starts
    /// fresh (not eaten, not a leftover, one serving, no meal prep).
    /// </summary>
    public static Dictionary<MealSlot, SlotEntry> CopyableSlots(PlanDay? day) =>
        day is null
            ? new()
            : day.Slots
                .Where(s => !string.IsNullOrWhiteSpace(s.Value.MealId))
                .ToDictionary(s => s.Key, s => new SlotEntry { MealId = s.Value.MealId });

    /// <summary>The catalog meal ids of a day in slot order.</summary>
    public static List<string> MealIds(PlanDay? day) =>
        day is null ? [] : day.Slots.OrderBy(s => s.Key).Select(s => s.Value.MealId).OfType<string>().ToList();
}
