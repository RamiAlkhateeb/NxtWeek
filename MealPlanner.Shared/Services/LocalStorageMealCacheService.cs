using System.Text.Json;
using MealPlanner.Shared.Models;
using MealPlanner.Shared.Services;
using Microsoft.JSInterop;

namespace MealPlanner.Web.Services;

public class LocalStorageMealCacheService : IMealCacheService
{
    private const string Key = "meals-cache";
    private readonly IJSRuntime _js;

    public LocalStorageMealCacheService(IJSRuntime js) => _js = js;

    public async Task<List<Meal>?> GetCachedWeekAsync()
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", Key);
        if (string.IsNullOrEmpty(json)) return null;

        return JsonSerializer.Deserialize<List<Meal>>(json);
    }

    public async Task SetCachedWeekAsync(List<Meal> meals)
    {
        var json = JsonSerializer.Serialize(meals);
        await _js.InvokeVoidAsync("localStorage.setItem", Key, json);
    }
}