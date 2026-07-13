using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public interface IMealCacheService
{
    Task<List<Meal>?> GetCachedWeekAsync();
    Task SetCachedWeekAsync(List<Meal> meals);
}