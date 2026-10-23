namespace MealPlanner.Tests;

public class DayMathTests
{
    private static readonly DateOnly Monday = new(2026, 10, 19);

    private static readonly Dictionary<string, NutritionInfo> Meals = new()
    {
        ["chicken"] = new() { Kcal = 520, Protein = 45, Carbs = 55, Fat = 12, Fibre = 4, Source = NutritionSource.Manual },
        ["shake"] = new() { Kcal = 250, Protein = 35, Carbs = 15, Fat = 5, Fibre = 1, Source = NutritionSource.Estimate }
    };

    private static NutritionInfo? Lookup(string id) => Meals.GetValueOrDefault(id);

    [Fact]
    public void Training_day_has_four_slots_in_time_order()
    {
        Assert.Equal([MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner, MealSlot.Snack], DayMath.SlotsFor(DayType.Training));
    }

    [Fact]
    public void Rest_day_hides_post_workout()
    {
        Assert.Equal([MealSlot.Meal1, MealSlot.Dinner, MealSlot.Snack], DayMath.SlotsFor(DayType.Rest));
    }

    [Fact]
    public void Slots_follow_the_users_meal_times()
    {
        var times = NutritionProfile.DefaultMealTimes();
        times[MealSlot.PostWorkout] = "20:00";

        Assert.Equal([MealSlot.Meal1, MealSlot.Dinner, MealSlot.PostWorkout, MealSlot.Snack], DayMath.SlotsFor(DayType.Training, times));
    }

    [Fact]
    public void Day_type_comes_from_training_days_unless_set_on_the_day()
    {
        var profile = new NutritionProfile(); // Mon, Wed, Fri

        Assert.Equal(DayType.Training, DayMath.TypeOf(null, Monday, profile));
        Assert.Equal(DayType.Rest, DayMath.TypeOf(null, Monday.AddDays(1), profile));
        Assert.Equal(DayType.Rest, DayMath.TypeOf(new PlanDay { Type = DayType.Rest }, Monday, profile));
    }

    [Fact]
    public void Totals_split_eaten_and_planned_and_scale_by_servings()
    {
        var day = new PlanDay
        {
            Date = Monday,
            Slots =
            {
                [MealSlot.Meal1] = new SlotEntry { MealId = "chicken", Eaten = true },
                [MealSlot.PostWorkout] = new SlotEntry { MealId = "shake", Servings = 2 },
                [MealSlot.Snack] = new SlotEntry { Quick = new QuickAddEntry { Name = "Nuts", Kcal = 180, Protein = 6, Carbs = 6, Fat = 15 }, Eaten = true }
            }
        };
        var targets = TargetCalculator.For(new NutritionProfile(), DayType.Training);

        var t = DayMath.Totals(day, DayType.Training, targets, Lookup);

        Assert.Equal(700, t.Eaten.Kcal);
        Assert.Equal(51, t.Eaten.Protein);
        Assert.Equal(500, t.Planned.Kcal);
        Assert.Equal(70, t.Planned.Protein);
        Assert.Equal(1950 - 700 - 500, t.OpenKcal);
        Assert.Equal(135 - 51 - 70, t.OpenProtein);
        Assert.Equal(1250, t.LeftKcal);
        Assert.Equal(84, t.LeftProtein);
    }

    [Fact]
    public void Post_workout_entry_is_ignored_on_a_rest_day()
    {
        var day = new PlanDay { Slots = { [MealSlot.PostWorkout] = new SlotEntry { MealId = "shake" } } };

        var t = DayMath.Totals(day, DayType.Rest, TargetCalculator.For(new NutritionProfile(), DayType.Rest), Lookup);

        Assert.Equal(0, t.Planned.Kcal);
    }

    [Fact]
    public void Meal_without_nutrition_is_missing_and_adds_nothing()
    {
        var n = DayMath.Of(new SlotEntry { MealId = "unknown" }, Lookup);

        Assert.True(n.Missing);
        Assert.Equal(0, n.Totals.Kcal);
    }

    [Fact]
    public void Entry_keeps_the_meals_source()
    {
        Assert.Equal(NutritionSource.Estimate, DayMath.Of(new SlotEntry { MealId = "shake" }, Lookup).Source);
    }

    [Fact]
    public void Empty_slots_lists_what_is_not_planned()
    {
        var day = new PlanDay { Slots = { [MealSlot.Dinner] = new SlotEntry { MealId = "chicken" } } };

        Assert.Equal([MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Snack], DayMath.EmptySlots(day, DayType.Training));
    }

    [Fact]
    public void Legacy_single_meal_becomes_dinner()
    {
        var day = DayMath.FromLegacy(new WeeklyPlanEntry { Date = Monday, MealId = "chicken" });

        Assert.Equal(Monday, day.Date);
        Assert.Equal("chicken", Assert.Single(day.Slots).Value.MealId);
        Assert.Equal(MealSlot.Dinner, day.Slots.Keys.Single());
        Assert.Null(day.Type);
    }
}
