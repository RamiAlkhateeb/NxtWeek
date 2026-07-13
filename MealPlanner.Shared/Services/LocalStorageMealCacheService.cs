using Blazored.LocalStorage;
using MealPlanner.Shared.Models;
using MealPlanner.Shared.Services;

namespace MealPlanner.Web.Services;

public class LocalStorageMealCacheService : IMealCacheService
{
    private const string Key = "meals-cache";
    private readonly ILocalStorageService _storage;

    public LocalStorageMealCacheService(ILocalStorageService storage) => _storage = storage;

    public async Task<List<Meal>?> GetCachedWeekAsync()
        => await _storage.GetItemAsync<List<Meal>>(Key);

    public async Task SetCachedWeekAsync(List<Meal> meals)
        => await _storage.SetItemAsync(Key, meals);
}