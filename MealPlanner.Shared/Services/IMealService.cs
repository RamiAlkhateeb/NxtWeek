using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public interface IMealService
{
    Task<List<Meal>> GetWeekAsync(DateOnly start, DateOnly end);
    Task UpsertMealAsync(Meal meal);
    Task<bool> IsSeededAsync();
    Task SeedAsync(List<Meal> meals);
}