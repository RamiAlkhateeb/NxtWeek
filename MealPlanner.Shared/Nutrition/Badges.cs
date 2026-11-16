using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

public enum BadgeKind { FirstFullWeek, Protein7, TenMeals, FirstBatch, SharedWeek }

/// <summary>One badge: how far along it is (<see cref="Progress"/> of <see cref="Goal"/>). Earned once progress reaches the goal.</summary>
public sealed record Badge(BadgeKind Kind, int Progress, int Goal)
{
    public bool Earned => Progress >= Goal;
}

/// <summary>
/// Pure badge rules. They read only what was eaten (never weight), so they can be worked out from the plan alone.
/// A missed day never takes a badge away: each one counts the best run or the total so far.
/// </summary>
public static class Badges
{
    public const double ProteinShare = 0.95;
    public const int ProteinDays = 7;
    public const int DifferentMeals = 10;

    /// <param name="days">Every day of history the caller has (any order); days after <paramref name="today"/> are ignored.</param>
    /// <param name="proteinTarget">The daily protein target in grams.</param>
    /// <param name="sharedWeek">The user has shared a week with friends.</param>
    public static List<Badge> For(IReadOnlyDictionary<DateOnly, PlanDay> days, DateOnly today, MealPrefs? prefs, double proteinTarget,
        Func<string, NutritionInfo?> nutritionFor, bool sharedWeek)
    {
        var past = days.Where(kv => kv.Key <= today).OrderBy(kv => kv.Key).ToList();
        return
        [
            new Badge(BadgeKind.FirstFullWeek, FullWeeks(past, prefs) > 0 ? 1 : 0, 1),
            new Badge(BadgeKind.Protein7, Math.Min(ProteinDays, BestProteinRun(past, prefs, proteinTarget, nutritionFor)), ProteinDays),
            new Badge(BadgeKind.TenMeals, Math.Min(DifferentMeals, past.SelectMany(kv => kv.Value.Slots.Values)
                .Where(e => e.Eaten && !e.Skipped && !string.IsNullOrEmpty(e.MealId)).Select(e => e.MealId!).Distinct().Count()), DifferentMeals),
            new Badge(BadgeKind.FirstBatch, past.Any(kv => kv.Value.Slots.Values.Any(e => e.Eaten && e.PrepBatches > 1)) ? 1 : 0, 1),
            new Badge(BadgeKind.SharedWeek, sharedWeek ? 1 : 0, 1)
        ];
    }

    // A Monday-to-Sunday week in which every day is logged (every meal eaten or skipped).
    private static int FullWeeks(List<KeyValuePair<DateOnly, PlanDay>> past, MealPrefs? prefs) =>
        past.Where(kv => DayStatus.DayLogged(kv.Value, prefs))
            .GroupBy(kv => kv.Key.AddDays(-(((int)kv.Key.DayOfWeek + 6) % 7)))
            .Count(g => g.Count() == 7);

    // The longest run of consecutive days whose eaten protein reached 95% of the target.
    private static int BestProteinRun(List<KeyValuePair<DateOnly, PlanDay>> past, MealPrefs? prefs, double target, Func<string, NutritionInfo?> nutritionFor)
    {
        if (target <= 0) return 0;
        var reached = past
            .Where(kv => DayMath.Totals(kv.Value, new DailyTargets(0, 0, 0, 0, 0), nutritionFor, prefs).Eaten.Protein >= target * ProteinShare)
            .Select(kv => kv.Key)
            .ToList();
        int best = 0, run = 0;
        DateOnly? previous = null;
        foreach (var date in reached)
        {
            run = previous is { } p && date.DayNumber - p.DayNumber == 1 ? run + 1 : 1;
            best = Math.Max(best, run);
            previous = date;
        }
        return best;
    }

    /// <summary>The Monday of the week <paramref name="date"/> is in.</summary>
    public static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
