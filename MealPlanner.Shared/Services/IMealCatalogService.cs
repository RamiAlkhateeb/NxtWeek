using System.Collections.Generic;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public interface IMealCatalogService
{
    Task<List<MealCatalogItem>> GetAllMealsAsync();
    Task<MealCatalogItem?> GetMealByIdAsync(string id);
    Task<List<MealCatalogItem>> GetFilteredMealsAsync(List<Cuisine>? cuisines, MealType? mealType);
    Task<List<MealCatalogItem>> SearchMealsAsync(string query);
    Task<MealCatalogItem> CreateMealAsync(MealCatalogItem meal);
    Task UpsertMealAsync(MealCatalogItem meal);
    Task UpdateMealAsync(MealCatalogItem meal);
    Task DeleteMealAsync(string id);
    Task<bool> IsCatalogSeededAsync();
    Task SeedCatalogAsync(List<MealCatalogItem> meals);
}
