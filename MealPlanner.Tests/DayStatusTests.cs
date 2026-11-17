namespace MealPlanner.Tests;

public class DayStatusTests
{
    private static PlanDay Day(params (MealSlot Slot, bool Eaten)[] entries)
    {
        var day = new PlanDay();
        foreach (var (slot, eaten) in entries) day.Slots[slot] = new SlotEntry { MealId = "x", Eaten = eaten };
        return day;
    }

    private static readonly DateOnly Monday = new(2026, 10, 19);

    [Fact]
    public void Dot_reflects_how_much_of_the_day_is_eaten()
    {
        Assert.Equal(DayDot.None, DayStatus.DotFor(null));
        Assert.Equal(DayDot.None, DayStatus.DotFor(new PlanDay()));
        Assert.Equal(DayDot.Planned, DayStatus.DotFor(Day((MealSlot.Meal1, false))));
        Assert.Equal(DayDot.PartlyEaten, DayStatus.DotFor(Day((MealSlot.Meal1, true), (MealSlot.Dinner, false))));
        Assert.Equal(DayDot.AllEaten, DayStatus.DotFor(Day((MealSlot.Meal1, true), (MealSlot.Dinner, true))));
    }

    [Fact]
    public void Dot_ignores_an_entry_in_a_meal_that_is_off()
    {
        Assert.Equal(DayDot.AllEaten, DayStatus.DotFor(Day((MealSlot.Meal1, true), (MealSlot.PostWorkout, false))));
    }

    [Fact]
    public void A_day_is_logged_when_every_meal_is_eaten_or_skipped()
    {
        var full = Day((MealSlot.Meal1, true), (MealSlot.Dinner, true), (MealSlot.Snack, true));
        Assert.True(DayStatus.DayLogged(full));

        var skippedSnack = Day((MealSlot.Meal1, true), (MealSlot.Dinner, true));
        skippedSnack.Slots[MealSlot.Snack] = new SlotEntry { MealId = "x", Skipped = true };
        Assert.True(DayStatus.DayLogged(skippedSnack));

        Assert.False(DayStatus.DayLogged(null));
        Assert.False(DayStatus.DayLogged(new PlanDay()));
        Assert.False(DayStatus.DayLogged(Day((MealSlot.Meal1, true), (MealSlot.Dinner, true))));                       // snack empty
        Assert.False(DayStatus.DayLogged(Day((MealSlot.Meal1, true), (MealSlot.Dinner, false), (MealSlot.Snack, true)))); // dinner unanswered
    }

    [Fact]
    public void Someone_who_eats_one_meal_a_day_logs_the_day_with_that_meal()
    {
        var oneMeal = new MealPrefs { On = [MealSlot.Meal1] };

        Assert.True(DayStatus.DayLogged(Day((MealSlot.Meal1, true)), oneMeal));
    }

    [Fact]
    public void Stamps_count_logged_days_and_a_missed_day_does_not_reset_them()
    {
        PlanDay Full() => Day((MealSlot.Meal1, true), (MealSlot.Dinner, true), (MealSlot.Snack, true));
        var days = new Dictionary<DateOnly, PlanDay>
        {
            [Monday] = Full(),
            [Monday.AddDays(1)] = Day((MealSlot.Meal1, true)),   // missed
            [Monday.AddDays(2)] = Full(),
            [Monday.AddDays(4)] = Full(),
            [Monday.AddDays(7)] = Full()                          // next week
        };

        Assert.Equal(3, DayStatus.Stamps(Monday, days));
    }

    [Theory]
    [InlineData("13:00", "15:40", true)]
    [InlineData("15:40", "15:40", true)]
    [InlineData("19:00", "15:40", false)]
    [InlineData("", "15:40", false)]
    public void Check_in_is_asked_once_the_meals_time_has_passed(string slotTime, string now, bool expected) =>
        Assert.Equal(expected, DayStatus.NeedsCheckIn(new SlotEntry { MealId = "x" }, slotTime, TimeOnly.Parse(now)));

    [Fact]
    public void Check_in_is_not_asked_twice_or_on_other_days_or_for_empty_slots()
    {
        var now = new TimeOnly(22, 0);

        Assert.False(DayStatus.NeedsCheckIn(new SlotEntry { MealId = "x", Eaten = true }, "13:00", now));
        Assert.False(DayStatus.NeedsCheckIn(new SlotEntry { MealId = "x", Skipped = true }, "13:00", now));
        Assert.False(DayStatus.NeedsCheckIn(new SlotEntry { MealId = "x" }, "13:00", now, isToday: false));
        Assert.False(DayStatus.NeedsCheckIn(null, "13:00", now));
    }

    [Theory]
    [InlineData(29.9, ProteinZone.Low)]
    [InlineData(30, ProteinZone.Aim)]
    [InlineData(40, ProteinZone.Aim)]
    [InlineData(41, ProteinZone.High)]
    public void Protein_zone(double grams, ProteinZone zone) => Assert.Equal(zone, DayStatus.ZoneFor(grams));

    [Theory]
    [InlineData(25, 50)]
    [InlineData(80, 100)]
    [InlineData(-5, 0)]
    public void Gauge_position_on_a_0_to_50_scale(double grams, double percent) => Assert.Equal(percent, DayStatus.GaugePercent(grams));

    [Theory]
    [InlineData(1, 1, 1.5)]
    [InlineData(1, -1, 0.5)]
    [InlineData(0.5, -1, 0.5)]
    [InlineData(6, 1, 6)]
    public void Servings_step_by_half_within_limits(double current, int direction, double expected) =>
        Assert.Equal(expected, DayStatus.StepServings(current, direction));

    [Fact]
    public void Ring_arcs_never_pass_a_full_ring()
    {
        Assert.Equal((0.5, 0.25, false), DayStatus.RingArcs(1000, 500, 2000));
        Assert.Equal((0.75, 0.25, false), DayStatus.RingArcs(1500, 900, 2000));
        Assert.Equal((1.0, 0.0, true), DayStatus.RingArcs(2400, 300, 2000));
        Assert.Equal((0.0, 0.0, false), DayStatus.RingArcs(100, 100, 0));
    }

    [Fact]
    public void Next_meal_is_the_earliest_unanswered_planned_meal_still_ahead()
    {
        var prefs = new MealPrefs();   // Meal 1, Dinner and Evening snack on
        var day = new PlanDay
        {
            Slots =
            {
                [MealSlot.Meal1] = new SlotEntry { MealId = "a" },
                [MealSlot.Dinner] = new SlotEntry { MealId = "b" },
                [MealSlot.Snack] = new SlotEntry { MealId = "c" }
            }
        };
        var before = TimeOnly.Parse(prefs.TimeOf(MealSlot.Meal1)).AddMinutes(-5);
        var afterDinner = TimeOnly.Parse(prefs.TimeOf(MealSlot.Dinner)).AddMinutes(5);
        Assert.Equal(MealSlot.Meal1, DayStatus.NextMeal(day, prefs, before));
        Assert.Equal(MealSlot.Snack, DayStatus.NextMeal(day, prefs, afterDinner));
        day.Slots[MealSlot.Meal1].Eaten = true;
        Assert.Equal(MealSlot.Dinner, DayStatus.NextMeal(day, prefs, before));
        day.Slots.Clear();
        Assert.Null(DayStatus.NextMeal(day, prefs, before));
        Assert.Null(DayStatus.NextMeal(null, prefs, before));
    }

    [Fact]
    public void Eaten_share_is_capped_at_a_full_ring()
    {
        Assert.Equal(0.5, DayStatus.EatenShare(1000, 2000));
        Assert.Equal(1, DayStatus.EatenShare(3000, 2000));
    }
}
