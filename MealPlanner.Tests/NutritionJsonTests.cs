using System.Text.Json;

namespace MealPlanner.Tests;

// Firebase is reached with System.Net.Http.Json, i.e. the web (camelCase) defaults.
public class NutritionJsonTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Plan_day_round_trips_with_readable_slot_names()
    {
        var day = new PlanDay
        {
            Date = new DateOnly(2026, 10, 19),
            Slots =
            {
                [MealSlot.PostWorkout] = new SlotEntry { MealId = "shake", Servings = 1.5, PrepBatches = 2 },
                [MealSlot.Dinner] = new SlotEntry { MealId = "rice", Skipped = true }
            }
        };

        var json = JsonSerializer.Serialize(day, Web);
        var back = JsonSerializer.Deserialize<PlanDay>(json, Web)!;

        Assert.Contains("\"PostWorkout\"", json);
        Assert.DoesNotContain("\"type\"", json);
        Assert.Equal(1.5, back.Slots[MealSlot.PostWorkout].Servings);
        Assert.Equal(2, back.Slots[MealSlot.PostWorkout].PrepBatches);
        Assert.True(back.Slots[MealSlot.Dinner].Skipped);
    }

    [Fact]
    public void A_day_saved_before_3_1_still_loads_and_keeps_its_day_type()
    {
        var back = JsonSerializer.Deserialize<PlanDay>("{\"type\":\"Rest\",\"slots\":{\"Meal1\":{\"mealId\":\"a\",\"eaten\":true}}}", Web)!;

        Assert.Equal(DayType.Rest, back.Type);
        Assert.True(back.Slots[MealSlot.Meal1].Eaten);
        Assert.False(back.Slots[MealSlot.Meal1].Skipped);
        Assert.Contains("\"Rest\"", JsonSerializer.Serialize(back, Web));
    }

    [Fact]
    public void Profile_round_trips_with_health_fields_only()
    {
        var p = new NutritionProfile { Goal = Goal.Keep, WeeklyAdjustKcal = -150 };
        p.Overrides.Daily.Kcal = 2000;

        var json = JsonSerializer.Serialize(p, Web);
        var back = JsonSerializer.Deserialize<NutritionProfile>(json, Web)!;

        Assert.Equal(Goal.Keep, back.Goal);
        Assert.Equal(-150, back.WeeklyAdjustKcal);
        Assert.Equal(2000, back.Overrides.Daily.Kcal);
        Assert.DoesNotContain("mealTimes", json);
        Assert.DoesNotContain("offSlots", json);
        Assert.DoesNotContain("trainingDays", json);
        Assert.DoesNotContain("\"daily\"", json);
    }

    [Fact]
    public void A_profile_saved_before_3_1_loads_with_its_training_overrides_as_the_daily_ones()
    {
        var old = "{\"goal\":\"Lose\",\"trainingDays\":[true,false,true,false,true,false,false]," +
                  "\"mealTimes\":{\"Meal1\":\"12:00\",\"PostWorkout\":\"17:30\"},\"offSlots\":[\"Snack\"]," +
                  "\"overrides\":{\"training\":{\"protein\":150},\"rest\":{\"kcal\":1700}}}";

        var back = JsonSerializer.Deserialize<NutritionProfile>(old, Web)!;

        Assert.Equal(150, back.Overrides.Daily.Protein);
        Assert.Null(back.Overrides.Daily.Kcal);
        Assert.Equal("12:00", back.MealTimes![MealSlot.Meal1]);
        Assert.Equal([MealSlot.Snack], back.OffSlots);
        Assert.Equal(150, TargetCalculator.For(back).Protein);
    }

    [Fact]
    public void Meal_prefs_round_trip_and_default_to_three_meals_with_macros_off()
    {
        var prefs = new MealPrefs { TrackMacros = true, Ramadan = true };
        prefs.SetOn(MealSlot.PostWorkout, true);
        prefs.Times[MealSlot.Dinner] = "20:15";

        var json = JsonSerializer.Serialize(prefs, Web);
        var back = JsonSerializer.Deserialize<MealPrefs>(json, Web)!.Normalize();

        Assert.Contains("\"PostWorkout\"", json);
        Assert.True(back.TrackMacros);
        Assert.True(back.Ramadan);
        Assert.True(back.IsOn(MealSlot.PostWorkout));
        Assert.Equal("20:15", back.Times[MealSlot.Dinner]);

        var fresh = new MealPrefs();
        Assert.False(fresh.TrackMacros);
        Assert.False(fresh.Ramadan);
        Assert.Equal([MealSlot.Meal1, MealSlot.Dinner, MealSlot.Snack], fresh.On);
    }

    [Fact]
    public void Meal_prefs_survive_firebase_dropping_empty_lists()
    {
        // Firebase stores no empty arrays or maps, so "only Meal 1" comes back without "on".
        var back = JsonSerializer.Deserialize<MealPrefs>("{\"trackMacros\":false,\"on\":null,\"times\":null,\"ramadanTimes\":null}", Web)!.Normalize();

        Assert.Equal([MealSlot.Meal1], back.On);
        Assert.Equal("13:00", back.Times[MealSlot.Meal1]);
        Assert.Equal("18:30", back.RamadanTimes[MealSlot.Dinner]);
    }

    [Fact]
    public void Old_meal_times_and_switched_off_meals_carry_over_and_post_workout_becomes_optional()
    {
        var old = new NutritionProfile
        {
            MealTimes = new() { [MealSlot.Meal1] = "12:00", [MealSlot.PostWorkout] = "17:30", [MealSlot.Dinner] = "20:00", [MealSlot.Snack] = "22:00" },
            OffSlots = [MealSlot.Snack]
        };

        var unused = MealPrefs.FromLegacy(old, hasUpcomingAfternoonMeal: false);
        Assert.Equal([MealSlot.Meal1, MealSlot.Dinner], unused.On);
        Assert.Equal("12:00", unused.Times[MealSlot.Meal1]);
        Assert.Equal("17:30", unused.Times[MealSlot.PostWorkout]);
        Assert.False(unused.TrackMacros);

        var inUse = MealPrefs.FromLegacy(old, hasUpcomingAfternoonMeal: true);
        Assert.Equal([MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner], inUse.On);

        old.OffSlots = [MealSlot.PostWorkout];
        Assert.DoesNotContain(MealSlot.PostWorkout, MealPrefs.FromLegacy(old, hasUpcomingAfternoonMeal: true).On);
    }

    [Fact]
    public void Someone_without_an_old_profile_gets_the_defaults()
    {
        var prefs = MealPrefs.FromLegacy(null, hasUpcomingAfternoonMeal: false);

        Assert.Equal([MealSlot.Meal1, MealSlot.Dinner, MealSlot.Snack], prefs.On);
        Assert.Equal("16:30", prefs.Times[MealSlot.PostWorkout]);
    }

    [Fact]
    public void Old_catalog_rows_without_nutrition_still_load()
    {
        var meal = JsonSerializer.Deserialize<MealCatalogItem>("{\"name\":\"Mujaddara\",\"mealType\":\"Vegan\"}", Web)!;

        Assert.Null(meal.Nutrition);
        Assert.Equal(1, meal.ServingsYield);
        Assert.Empty(meal.IngredientLines);
        Assert.Equal(MealType.Vegetarian, meal.MealType);
    }
}
