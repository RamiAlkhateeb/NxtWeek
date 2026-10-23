namespace MealPlanner.Tests;

public class WeightTrendTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    /// <summary>The prototype's sample series: 28 daily weigh-ins losing <paramref name="perDay"/> kg a day, with weekly noise.</summary>
    private static List<BodyLog> Series(double perDay, int n = 28)
    {
        var logs = new List<BodyLog>();
        for (var i = 0; i < n; i++)
        {
            var k = n - 1 - i;
            var noise = .28 * Math.Sin(2 * Math.PI * i / 7 + .6) + .14 * Math.Sin(4 * Math.PI * i / 7 + 1.9);
            logs.Add(new BodyLog { Date = Today.AddDays(-k), Kg = Math.Round((69 + perDay * k + noise) * 10) / 10 });
        }
        return logs;
    }

    [Fact]
    public void Average_needs_four_weigh_ins_in_the_window()
    {
        var logs = new List<BodyLog>
        {
            new() { Date = Today, Kg = 69 }, new() { Date = Today.AddDays(-1), Kg = 70 }, new() { Date = Today.AddDays(-2), Kg = 71 }
        };
        Assert.Null(WeightTrend.SevenDayAverage(logs, Today));

        logs.Add(new() { Date = Today.AddDays(-6), Kg = 70 });
        Assert.Equal(70, WeightTrend.SevenDayAverage(logs, Today));

        logs.Add(new() { Date = Today.AddDays(-7), Kg = 99 }); // outside the 7-day window
        Assert.Equal(70, WeightTrend.SevenDayAverage(logs, Today));
    }

    [Fact]
    public void Waist_only_days_do_not_count_as_weigh_ins()
    {
        var logs = Enumerable.Range(0, 5).Select(i => new BodyLog { Date = Today.AddDays(-i), WaistCm = 85 }).ToList();
        Assert.Null(WeightTrend.SevenDayAverage(logs, Today));
        Assert.Equal(WeeklyCheckState.Empty, WeightTrend.Check(logs, Today, Goal.Lose).State);
    }

    [Theory]
    [InlineData(0.043, WeeklyCheckState.OnTrack)]
    [InlineData(0.01, WeeklyCheckState.Slower)]
    [InlineData(0.1, WeeklyCheckState.Faster)]
    public void Weekly_check_for_the_prototype_presets(double perDay, WeeklyCheckState expected)
    {
        Assert.Equal(expected, WeightTrend.Check(Series(perDay), Today, Goal.Lose).State);
    }

    [Fact]
    public void Few_entries_count_towards_seven()
    {
        var check = WeightTrend.Check(Series(0.043, 4), Today, Goal.Lose);
        Assert.Equal(WeeklyCheckState.First, check.State);
        Assert.Equal(4, check.Entries);
    }

    [Fact]
    public void Seven_entries_without_a_week_ago_average_wait_for_14_days()
    {
        Assert.Equal(WeeklyCheckState.Keep, WeightTrend.Check(Series(0.043, 9), Today, Goal.Lose).State);
    }

    [Fact]
    public void No_entries_is_empty()
    {
        Assert.Equal(WeeklyCheckState.Empty, WeightTrend.Check([], Today, Goal.Lose).State);
    }

    [Theory]
    [InlineData(Goal.Keep)]
    [InlineData(Goal.Gain)]
    public void Maintain_and_gain_only_see_the_trend(Goal goal)
    {
        var check = WeightTrend.Check(Series(0.1), Today, goal);
        Assert.Equal(WeeklyCheckState.Trend, check.State);
        Assert.True(check.Change < 0);
        Assert.Empty(WeightTrend.PlannedLine(WeightTrend.AverageLine(Series(0.1), Today, 28), Today, goal));
    }

    [Fact]
    public void Planned_line_drops_0_35_kg_a_week_from_the_first_average()
    {
        var avg = WeightTrend.AverageLine(Series(0.043), Today, 28);
        var plan = WeightTrend.PlannedLine(avg, Today, Goal.Lose);

        Assert.Equal(avg[0], plan[0]);
        var weeks = (Today.DayNumber - avg[0].Date.DayNumber) / 7.0;
        Assert.Equal(avg[0].Value - 0.35 * weeks, plan[1].Value, 6);
    }
}

public class WeeklySummaryTests
{
    private static readonly DateOnly Monday = new(2026, 10, 12);
    private static readonly Dictionary<string, NutritionInfo> Meals = new()
    {
        ["m"] = new() { Kcal = 1000, Protein = 60, Source = NutritionSource.Manual }
    };

    private static PlanDay Eaten(DateOnly date, double servings) => new()
    {
        Date = date,
        Type = DayType.Training,
        Slots = { [MealSlot.Meal1] = new SlotEntry { MealId = "m", Servings = servings, Eaten = true } }
    };

    [Fact]
    public void Skips_days_with_nothing_eaten_and_counts_the_10_percent_band()
    {
        var days = new Dictionary<DateOnly, PlanDay>
        {
            [Monday] = Eaten(Monday, 1.95),               // 1,950 vs 1,950: within
            [Monday.AddDays(1)] = Eaten(Monday.AddDays(1), 1.5), // 1,500: off
            [Monday.AddDays(2)] = new PlanDay { Date = Monday.AddDays(2), Slots = { [MealSlot.Meal1] = new SlotEntry { MealId = "m" } } } // planned, not eaten
        };

        var s = WeeklySummary.For(Monday, Monday.AddDays(7), days, new NutritionProfile(), id => Meals.GetValueOrDefault(id));

        Assert.Equal([SummaryDay.Within, SummaryDay.Off, SummaryDay.NoData, SummaryDay.NoData, SummaryDay.NoData, SummaryDay.NoData, SummaryDay.NoData], s.Days);
        Assert.Equal(2, s.LoggedDays);
        Assert.Equal(1, s.WithinTarget);
        Assert.Equal(1725, s.AverageKcal);
        Assert.Equal(1950, s.AverageTargetKcal);
        Assert.Equal(135, s.ProteinTarget);
    }

    [Fact]
    public void This_week_so_far_marks_today_and_later_as_future()
    {
        var s = WeeklySummary.For(Monday, Monday.AddDays(3), new Dictionary<DateOnly, PlanDay>(), new NutritionProfile(), _ => null);

        Assert.Equal(4, s.Days.Count(d => d == SummaryDay.Future));
        Assert.Equal(0, s.LoggedDays);
        Assert.Equal(0, s.AverageKcal);
    }
}
