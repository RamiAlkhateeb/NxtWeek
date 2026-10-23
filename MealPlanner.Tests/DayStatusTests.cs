namespace MealPlanner.Tests;

public class DayStatusTests
{
    private static PlanDay Day(params (MealSlot Slot, bool Eaten)[] entries)
    {
        var day = new PlanDay();
        foreach (var (slot, eaten) in entries) day.Slots[slot] = new SlotEntry { MealId = "x", Eaten = eaten };
        return day;
    }

    [Fact]
    public void Dot_reflects_how_much_of_the_day_is_eaten()
    {
        Assert.Equal(DayDot.None, DayStatus.DotFor(null, DayType.Training));
        Assert.Equal(DayDot.None, DayStatus.DotFor(new PlanDay(), DayType.Training));
        Assert.Equal(DayDot.Planned, DayStatus.DotFor(Day((MealSlot.Meal1, false)), DayType.Training));
        Assert.Equal(DayDot.PartlyEaten, DayStatus.DotFor(Day((MealSlot.Meal1, true), (MealSlot.Dinner, false)), DayType.Training));
        Assert.Equal(DayDot.AllEaten, DayStatus.DotFor(Day((MealSlot.Meal1, true), (MealSlot.Dinner, true)), DayType.Training));
    }

    [Fact]
    public void Dot_ignores_a_post_workout_entry_on_a_rest_day()
    {
        Assert.Equal(DayDot.AllEaten, DayStatus.DotFor(Day((MealSlot.Meal1, true), (MealSlot.PostWorkout, false)), DayType.Rest));
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
}
