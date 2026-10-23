using System.Text.Json;

namespace MealPlanner.Tests;

// Firebase is reached with System.Net.Http.Json, i.e. the web (camelCase) defaults.
public class NutritionJsonTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Plan_day_round_trips_with_readable_slot_and_type_names()
    {
        var day = new PlanDay
        {
            Date = new DateOnly(2026, 10, 19),
            Type = DayType.Rest,
            Slots = { [MealSlot.PostWorkout] = new SlotEntry { MealId = "shake", Servings = 1.5, PrepBatches = 2 } }
        };

        var json = JsonSerializer.Serialize(day, Web);
        var back = JsonSerializer.Deserialize<PlanDay>(json, Web)!;

        Assert.Contains("\"PostWorkout\"", json);
        Assert.Contains("\"Rest\"", json);
        Assert.Equal(DayType.Rest, back.Type);
        Assert.Equal(1.5, back.Slots[MealSlot.PostWorkout].Servings);
        Assert.Equal(2, back.Slots[MealSlot.PostWorkout].PrepBatches);
    }

    [Fact]
    public void Day_without_type_reads_as_null()
    {
        var back = JsonSerializer.Deserialize<PlanDay>("{\"slots\":{}}", Web)!;

        Assert.Null(back.Type);
    }

    [Fact]
    public void Profile_round_trips()
    {
        var p = new NutritionProfile { Goal = Goal.Keep, WeeklyAdjustKcal = -150 };
        p.Overrides.Training.Kcal = 2000;

        var back = JsonSerializer.Deserialize<NutritionProfile>(JsonSerializer.Serialize(p, Web), Web)!;

        Assert.Equal(Goal.Keep, back.Goal);
        Assert.Equal(-150, back.WeeklyAdjustKcal);
        Assert.Equal(2000, back.Overrides.Training.Kcal);
        Assert.Equal("13:00", back.MealTimes[MealSlot.Meal1]);
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
