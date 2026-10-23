using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

/// <summary>Daily targets for one day type, in whole kcal and grams.</summary>
public sealed record DailyTargets(int Kcal, int Protein, int Carbs, int Fat, int Fibre);

/// <summary>Every intermediate value, for the "How we calculate" card on the profile screen.</summary>
public sealed record TargetBreakdown(
    double Bmr,
    double Tdee,
    int GoalDeltaKcal,
    int WeeklyAdjustKcal,
    DailyTargets CalculatedTraining,
    DailyTargets CalculatedRest,
    double ProteinPerKg);

/// <summary>
/// Pure target maths (no I/O). Mifflin-St Jeor BMR × activity factor, plus the goal and the
/// weekly-check adjustment, rounded to 50 kcal. Protein is the larger of 28% of calories and
/// 1.9 g per kg; fat is 30% of calories; carbs take the rest. A rest day is 200 kcal less,
/// taken from carbs only.
/// </summary>
public static class TargetCalculator
{
    public const int RestDayKcalReduction = 200;
    public const int FibreTarget = 30;
    public static readonly double[] ActivityFactors = [1.2, 1.375, 1.55, 1.725];

    public static double Bmr(NutritionProfile p) =>
        10 * p.WeightKg + 6.25 * p.HeightCm - 5 * p.Age + (p.Sex == Sex.Male ? 5 : -161);

    public static double Tdee(NutritionProfile p) =>
        Bmr(p) * ActivityFactors[Math.Clamp(p.Activity, 0, ActivityFactors.Length - 1)];

    public static int GoalDelta(Goal goal) => goal switch
    {
        Goal.Lose => -300,
        Goal.Gain => 250,
        _ => 0
    };

    public static TargetBreakdown Calculate(NutritionProfile p)
    {
        var tdee = Tdee(p);
        var delta = GoalDelta(p.Goal);
        var kcal = Round50(tdee + delta + p.WeeklyAdjustKcal);
        var protein = Round5(Math.Max(kcal * 0.28 / 4, 1.9 * p.WeightKg));
        var fat = Round5(kcal * 0.30 / 9);
        var carbs = CarbsFor(kcal, protein, fat);
        var restKcal = kcal - RestDayKcalReduction;
        var restCarbs = CarbsFor(restKcal, protein, fat);

        return new TargetBreakdown(
            Bmr(p),
            tdee,
            delta,
            p.WeeklyAdjustKcal,
            new DailyTargets(kcal, protein, carbs, fat, FibreTarget),
            new DailyTargets(restKcal, protein, restCarbs, fat, FibreTarget),
            p.WeightKg > 0 ? protein / p.WeightKg : 0);
    }

    /// <summary>Targets for a day type, with the user's overrides for that type applied.</summary>
    public static DailyTargets For(NutritionProfile p, DayType type)
    {
        var calc = Calculate(p);
        var baseTargets = type == DayType.Rest ? calc.CalculatedRest : calc.CalculatedTraining;
        var o = p.Overrides.For(type);
        return new DailyTargets(
            o.Kcal ?? baseTargets.Kcal,
            o.Protein ?? baseTargets.Protein,
            o.Carbs ?? baseTargets.Carbs,
            o.Fat ?? baseTargets.Fat,
            baseTargets.Fibre);
    }

    private static int CarbsFor(int kcal, int protein, int fat) =>
        Math.Max(0, Round5((kcal - 4.0 * protein - 9.0 * fat) / 4));

    /// <summary>Nearest 5, halves up (matches the prototype's floor(x/5 + 0.5) * 5).</summary>
    public static int Round5(double x) => (int)(Math.Floor(x / 5 + 0.5) * 5);

    /// <summary>Nearest 50, halves up.</summary>
    public static int Round50(double x) => (int)(Math.Floor(x / 50 + 0.5) * 50);
}
