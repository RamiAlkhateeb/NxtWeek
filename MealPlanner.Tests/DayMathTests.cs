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

    private static MealPrefs AllFour() => new() { On = [MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner, MealSlot.Snack] };

    [Fact]
    public void A_day_has_meal_1_dinner_and_evening_snack_by_default()
    {
        Assert.Equal([MealSlot.Meal1, MealSlot.Dinner, MealSlot.Snack], DayMath.SlotsFor(null));
        Assert.Equal([MealSlot.Meal1, MealSlot.Dinner, MealSlot.Snack], DayMath.SlotsFor(new MealPrefs()));
    }

    [Fact]
    public void The_afternoon_snack_is_optional_and_sits_by_its_time()
    {
        Assert.Equal([MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner, MealSlot.Snack], DayMath.SlotsFor(AllFour()));
    }

    [Fact]
    public void Slots_follow_the_users_meal_times()
    {
        var prefs = AllFour();
        prefs.Times[MealSlot.PostWorkout] = "20:00";

        Assert.Equal([MealSlot.Meal1, MealSlot.Dinner, MealSlot.PostWorkout, MealSlot.Snack], DayMath.SlotsFor(prefs));
    }

    [Fact]
    public void Ramadan_mode_sorts_by_the_ramadan_times_and_gives_the_usual_times_back()
    {
        var prefs = new MealPrefs { Ramadan = true };
        prefs.RamadanTimes[MealSlot.Meal1] = "04:30";   // suhoor
        prefs.RamadanTimes[MealSlot.Dinner] = "18:30";  // iftar

        Assert.Equal("04:30", prefs.TimeOf(MealSlot.Meal1));
        Assert.Equal([MealSlot.Meal1, MealSlot.Dinner, MealSlot.Snack], DayMath.SlotsFor(prefs));

        prefs.Ramadan = false;
        Assert.Equal("13:00", prefs.TimeOf(MealSlot.Meal1));
    }

    [Fact]
    public void Meals_turned_off_are_left_out_but_meal_1_stays()
    {
        var prefs = new MealPrefs { On = [] };

        Assert.Equal([MealSlot.Meal1], DayMath.SlotsFor(prefs));
        Assert.True(prefs.IsOn(MealSlot.Meal1));
        Assert.False(prefs.IsOn(MealSlot.Dinner));

        prefs.SetOn(MealSlot.Dinner, true);
        prefs.SetOn(MealSlot.Meal1, false);
        Assert.Equal([MealSlot.Meal1, MealSlot.Dinner], DayMath.SlotsFor(prefs));
    }

    [Fact]
    public void A_meal_that_is_off_does_not_count_and_is_not_empty()
    {
        var prefs = new MealPrefs { On = [MealSlot.Meal1, MealSlot.Snack] };
        var day = new PlanDay { Slots = { [MealSlot.Meal1] = new SlotEntry { MealId = "chicken" }, [MealSlot.Dinner] = new SlotEntry { MealId = "chicken" } } };

        var totals = DayMath.Totals(day, new DailyTargets(1950, 135, 205, 65, 30), Lookup, prefs);

        Assert.Equal(520, totals.Planned.Kcal);
        Assert.Equal([MealSlot.Snack], DayMath.EmptySlots(day, prefs));
        Assert.Equal(DayDot.Planned, DayStatus.DotFor(day, prefs));
    }

    [Fact]
    public void A_skipped_meal_counts_as_neither_eaten_nor_planned()
    {
        var day = new PlanDay
        {
            Slots =
            {
                [MealSlot.Meal1] = new SlotEntry { MealId = "chicken", Eaten = true },
                [MealSlot.Dinner] = new SlotEntry { MealId = "chicken", Skipped = true }
            }
        };

        var t = DayMath.Totals(day, new DailyTargets(1950, 135, 205, 65, 30), Lookup);

        Assert.Equal(520, t.Eaten.Kcal);
        Assert.Equal(0, t.Planned.Kcal);
        Assert.Equal(1430, t.OpenKcal);
        // A skipped slot is still taken: nothing is suggested for it.
        Assert.Equal([MealSlot.Snack], DayMath.EmptySlots(day));
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
        var targets = TargetCalculator.For(new NutritionProfile());

        var t = DayMath.Totals(day, targets, Lookup, AllFour());

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
    public void An_afternoon_snack_entry_is_ignored_while_that_meal_is_off()
    {
        var day = new PlanDay { Slots = { [MealSlot.PostWorkout] = new SlotEntry { MealId = "shake" } } };

        var t = DayMath.Totals(day, TargetCalculator.For(new NutritionProfile()), Lookup);

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

        Assert.Equal([MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Snack], DayMath.EmptySlots(day, AllFour()));
    }

    [Fact]
    public void Legacy_single_meal_becomes_the_first_meal()
    {
        var day = DayMath.FromLegacy(new WeeklyPlanEntry { Date = Monday, MealId = "chicken" });

        Assert.Equal(Monday, day.Date);
        Assert.Equal("chicken", Assert.Single(day.Slots).Value.MealId);
        Assert.Equal(MealSlot.Meal1, day.Slots.Keys.Single());
        Assert.Null(day.Type);
    }
}
