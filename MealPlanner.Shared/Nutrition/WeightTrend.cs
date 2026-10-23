using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

public enum WeeklyCheckState { Empty, First, Keep, OnTrack, Slower, Faster, Trend }

/// <param name="Entries">Weight entries so far.</param>
/// <param name="Change">7-day average now minus a week ago (negative = lost), when both exist.</param>
public sealed record WeeklyCheck(WeeklyCheckState State, int Entries, double? Change);

public readonly record struct ChartPoint(DateOnly Date, double Value);

/// <summary>
/// Pure weight maths for Progress: 7-day averages, the weekly check and the planned line.
/// A 7-day average needs at least 4 weigh-ins in the window, so one odd morning does not swing it.
/// </summary>
public static class WeightTrend
{
    public const int MinPointsForAverage = 4;
    public const int EntriesForCheck = 7;
    public const double OnTrackMinLoss = 0.15;
    public const double OnTrackMaxLoss = 0.55;
    public const double PlannedLossPerWeek = 0.35;
    public const int AdjustKcal = 150;

    /// <summary>Mean weight over the 7 days ending on <paramref name="day"/>, or null with fewer than 4 weigh-ins.</summary>
    public static double? SevenDayAverage(IEnumerable<BodyLog> logs, DateOnly day)
    {
        var window = logs.Where(l => l.Kg is not null && l.Date <= day && l.Date > day.AddDays(-7)).Select(l => l.Kg!.Value).ToList();
        return window.Count < MinPointsForAverage ? null : window.Average();
    }

    public static WeeklyCheck Check(IReadOnlyList<BodyLog> logs, DateOnly today, Goal goal)
    {
        var entries = logs.Count(l => l.Kg is not null);
        if (entries == 0) return new(WeeklyCheckState.Empty, 0, null);
        if (entries < EntriesForCheck) return new(WeeklyCheckState.First, entries, null);
        var now = SevenDayAverage(logs, today);
        var before = SevenDayAverage(logs, today.AddDays(-7));
        if (now is null || before is null) return new(WeeklyCheckState.Keep, entries, null);
        var change = now.Value - before.Value;
        if (goal != Goal.Lose) return new(WeeklyCheckState.Trend, entries, change);
        var loss = -change;
        var state = loss < OnTrackMinLoss ? WeeklyCheckState.Slower : loss > OnTrackMaxLoss ? WeeklyCheckState.Faster : WeeklyCheckState.OnTrack;
        return new(state, entries, change);
    }

    /// <summary>The 7-day average for every day of the range that has one.</summary>
    public static List<ChartPoint> AverageLine(IReadOnlyList<BodyLog> logs, DateOnly today, int rangeDays)
    {
        var points = new List<ChartPoint>();
        for (var d = today.AddDays(-(rangeDays - 1)); d <= today; d = d.AddDays(1))
            if (SevenDayAverage(logs, d) is { } avg) points.Add(new(d, avg));
        return points;
    }

    /// <summary>Planned loss of 0.35 kg a week from the first average point in range. Only for the Lose goal.</summary>
    public static List<ChartPoint> PlannedLine(IReadOnlyList<ChartPoint> averageLine, DateOnly today, Goal goal)
    {
        if (goal != Goal.Lose || averageLine.Count == 0) return [];
        var start = averageLine[0];
        double At(DateOnly d) => start.Value - PlannedLossPerWeek / 7 * (d.DayNumber - start.Date.DayNumber);
        return [start, new(today, At(today))];
    }
}

public enum SummaryDay { Within, Off, NoData, Future }

public sealed record WeekSummary(
    IReadOnlyList<SummaryDay> Days,
    int LoggedDays,
    double AverageKcal,
    double AverageProtein,
    double AverageTargetKcal,
    int ProteinTarget,
    int WithinTarget);

/// <summary>Eaten calories and protein over a week against each day's target.</summary>
public static class WeeklySummary
{
    public const double TargetBand = 0.10;

    /// <param name="upTo">Days on or after this date are "future" (this week so far: today).</param>
    public static WeekSummary For(DateOnly monday, DateOnly upTo, IReadOnlyDictionary<DateOnly, PlanDay> days,
        NutritionProfile profile, Func<string, NutritionInfo?> nutritionFor)
    {
        var states = new List<SummaryDay>();
        double kcal = 0, protein = 0, targetKcal = 0;
        int logged = 0, within = 0;
        for (var i = 0; i < 7; i++)
        {
            var date = monday.AddDays(i);
            if (date >= upTo) { states.Add(SummaryDay.Future); continue; }
            var day = days.GetValueOrDefault(date);
            var type = DayMath.TypeOf(day, date, profile);
            var target = TargetCalculator.For(profile, type);
            var totals = DayMath.Totals(day, type, target, nutritionFor, profile.MealTimes);
            // A day with nothing ticked as eaten is "not logged", not a 0 kcal day.
            if (totals.Eaten.Kcal <= 0) { states.Add(SummaryDay.NoData); continue; }
            logged++;
            kcal += totals.Eaten.Kcal;
            protein += totals.Eaten.Protein;
            targetKcal += target.Kcal;
            var ok = Math.Abs(totals.Eaten.Kcal - target.Kcal) / target.Kcal <= TargetBand;
            if (ok) within++;
            states.Add(ok ? SummaryDay.Within : SummaryDay.Off);
        }
        return new WeekSummary(states, logged,
            logged == 0 ? 0 : kcal / logged,
            logged == 0 ? 0 : protein / logged,
            logged == 0 ? 0 : targetKcal / logged,
            TargetCalculator.For(profile, DayType.Training).Protein,
            within);
    }
}
