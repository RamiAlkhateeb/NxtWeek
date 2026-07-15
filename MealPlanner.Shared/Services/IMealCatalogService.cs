using System.Collections.Generic;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public interface IMealCatalogService
{
    Task<List<MealCatalogItem>> GetAllMealsAsync();
    Task<MealCatalogItem?> GetMealByIdAsync(string id);
    Task<List<MealCatalogItem>> GetFilteredMealsAsync(List<Cuisine>? cuisines, MealType? mealType);
    Task UpsertMealAsync(MealCatalogItem meal);
    Task<bool> IsCatalogSeededAsync();
    Task SeedCatalogAsync(List<MealCatalogItem> meals);
}
