namespace MealPlanner.Tests;

public class TargetCalculatorTests
{
    // The main user: man, 31, 175 cm, 69 kg, lifts 3×/week (light activity), slow fat loss.
    private static NutritionProfile StartingUser() => new();

    [Fact]
    public void Starting_user_gets_the_agreed_training_day_targets()
    {
        var t = TargetCalculator.For(StartingUser(), DayType.Training);

        Assert.Equal(new DailyTargets(1950, 135, 205, 65, 30), t);
    }

    [Fact]
    public void Rest_day_is_200_kcal_less_taken_from_carbs()
    {
        var t = TargetCalculator.For(StartingUser(), DayType.Rest);

        Assert.Equal(1750, t.Kcal);
        Assert.Equal(155, t.Carbs);
        Assert.Equal(135, t.Protein);
        Assert.Equal(65, t.Fat);
    }

    [Fact]
    public void Breakdown_exposes_bmr_and_tdee()
    {
        var b = TargetCalculator.Calculate(StartingUser());

        Assert.Equal(1633.75, b.Bmr, 2);
        Assert.Equal(2246.40625, b.Tdee, 4);
        Assert.Equal(-300, b.GoalDeltaKcal);
        Assert.Equal(135 / 69.0, b.ProteinPerKg, 6);
    }

    [Fact]
    public void Female_bmr_uses_minus_161()
    {
        var p = StartingUser();
        p.Sex = Sex.Female;

        Assert.Equal(1633.75 - 166, TargetCalculator.Bmr(p), 2);
    }

    [Theory]
    [InlineData(Goal.Keep, 2250)]
    [InlineData(Goal.Gain, 2500)]
    public void Goal_shifts_calories(Goal goal, int expected)
    {
        var p = StartingUser();
        p.Goal = goal;

        Assert.Equal(expected, TargetCalculator.For(p, DayType.Training).Kcal);
    }

    [Fact]
    public void Weekly_adjustment_moves_calories()
    {
        var p = StartingUser();
        p.WeeklyAdjustKcal = -150;

        Assert.Equal(1800, TargetCalculator.For(p, DayType.Training).Kcal);
    }

    [Fact]
    public void Protein_floor_is_1_9_g_per_kg_when_heavier()
    {
        var p = StartingUser();
        p.WeightKg = 100; // 1.9 × 100 = 190 g beats 28% of calories

        Assert.Equal(190, TargetCalculator.For(p, DayType.Training).Protein);
    }

    [Fact]
    public void Overrides_replace_only_the_fields_set_for_that_day_type()
    {
        var p = StartingUser();
        p.Overrides.Rest.Protein = 150;

        var rest = TargetCalculator.For(p, DayType.Rest);
        var train = TargetCalculator.For(p, DayType.Training);

        Assert.Equal(150, rest.Protein);
        Assert.Equal(1750, rest.Kcal);
        Assert.Equal(135, train.Protein);
    }

    [Fact]
    public void Carbs_never_go_negative()
    {
        var p = StartingUser();
        p.WeightKg = 200;
        p.WeeklyAdjustKcal = -3000;

        Assert.Equal(0, TargetCalculator.For(p, DayType.Rest).Carbs);
    }

    [Theory]
    [InlineData(136.5, 135)]
    [InlineData(137.5, 140)]
    [InlineData(206.25, 205)]
    [InlineData(152.5, 155)]
    public void Round5_rounds_halves_up(double x, int expected) => Assert.Equal(expected, TargetCalculator.Round5(x));

    [Theory]
    [InlineData(1946.4, 1950)]
    [InlineData(1925, 1950)]
    [InlineData(1924.9, 1900)]
    public void Round50_rounds_halves_up(double x, int expected) => Assert.Equal(expected, TargetCalculator.Round50(x));
}
