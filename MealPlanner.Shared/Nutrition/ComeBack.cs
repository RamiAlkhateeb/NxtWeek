using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

public enum Celebration { None, DayComplete, ProteinReached }

/// <summary>Pure rules for the features that bring people back, without notifications.</summary>
public static class ComeBack
{
    public const int ReviewFromHour = 18;

    /// <summary>
    /// The week the Sunday review is about: from Sunday 18:00 it is the week that is ending, before that (Monday to
    /// Sunday afternoon) it is the week before. Returns that week's Monday.
    /// </summary>
    public static DateOnly ReviewWeek(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var monday = Badges.MondayOf(today);
        return today.DayOfWeek == DayOfWeek.Sunday && now.Hour >= ReviewFromHour ? monday : monday.AddDays(-7);
    }

    /// <summary>
    /// The review banner shows on Sunday evening and all of Monday, until that review was opened.
    /// It needs something to review: at least one logged day in that week.
    /// </summary>
    public static bool ShowReviewBanner(DateTime now, DateOnly? seenMonday, int loggedDaysInWeek)
    {
        var today = DateOnly.FromDateTime(now);
        var inWindow = (today.DayOfWeek == DayOfWeek.Sunday && now.Hour >= ReviewFromHour) || today.DayOfWeek == DayOfWeek.Monday;
        return inWindow && loggedDaysInWeek > 0 && seenMonday != ReviewWeek(now);
    }

    /// <summary>
    /// What to celebrate after a change to today: the day just became complete (every meal answered), or the
    /// eaten protein just reached the target. Completing the day wins when both happen at once.
    /// </summary>
    public static Celebration After(PlanDay? before, PlanDay? after, MealPrefs? prefs, double proteinTarget, Func<string, NutritionInfo?> nutritionFor)
    {
        if (!DayStatus.DayLogged(before, prefs) && DayStatus.DayLogged(after, prefs)) return Celebration.DayComplete;
        if (proteinTarget <= 0) return Celebration.None;
        var zero = new DailyTargets(0, 0, 0, 0, 0);
        var was = DayMath.Totals(before, zero, nutritionFor, prefs).Eaten.Protein;
        var now = DayMath.Totals(after, zero, nutritionFor, prefs).Eaten.Protein;
        return was < proteinTarget && now >= proteinTarget ? Celebration.ProteinReached : Celebration.None;
    }

    /// <summary>Minutes from <paramref name="now"/> until the "HH:mm" time today; null when it has passed or cannot be read.</summary>
    public static int? MinutesUntil(string time, TimeOnly now)
    {
        if (!TimeOnly.TryParse(time, out var at)) return null;
        var minutes = (int)Math.Ceiling((at - now).TotalMinutes);
        return at > now ? minutes : null;
    }
}
