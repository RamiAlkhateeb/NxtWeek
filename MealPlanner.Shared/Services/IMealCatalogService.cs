using System.Collections.Generic;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public interface IMealCatalogService
{
    Task<List<MealCatalogItem>> GetAllMealsAsync();
    Task<MealCatalogItem?> GetMealByIdAsync(string id);
    Task<List<MealCatalogItem>> GetFilteredMealsAsync(MealType? mealType, string? language = null);
    Task<List<MealCatalogItem>> SearchMealsAsync(string query);
    Task<MealCatalogItem> CreateMealAsync(MealCatalogItem meal);
    Task UpsertMealAsync(MealCatalogItem meal);
    Task UpdateMealAsync(MealCatalogItem meal);
    /// <summary>Saves only a meal's recipe steps (a PATCH), so nothing else about the meal is overwritten.</summary>
    Task SaveStepsAsync(string mealId, List<RecipeStep> steps);
    Task DeleteMealAsync(string id);
    Task<bool> IsCatalogSeededAsync();
    Task<IReadOnlyCollection<string>> GetSeededLanguagesAsync();
    Task SeedCatalogAsync(List<MealCatalogItem> meals);
}
