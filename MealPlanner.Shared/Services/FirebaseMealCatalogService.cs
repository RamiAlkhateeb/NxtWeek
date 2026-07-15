using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public class FirebaseMealCatalogService : IMealCatalogService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public FirebaseMealCatalogService(HttpClient http, FirebaseOptions options)
    {
        _http = http;
        _baseUrl = options.DatabaseUrl.TrimEnd('/');
    }

    public async Task<List<MealCatalogItem>> GetAllMealsAsync()
    {
        var all = await _http.GetFromJsonAsync<Dictionary<string, MealCatalogItemDto>>($"{_baseUrl}/mealCatalog.json");
        if (all is null) return new List<MealCatalogItem>();

        return all.Select(kvp => kvp.Value.ToModel(kvp.Key)).ToList();
    }

    public async Task<MealCatalogItem?> GetMealByIdAsync(string id)
    {
        var dto = await _http.GetFromJsonAsync<MealCatalogItemDto>($"{_baseUrl}/mealCatalog/{id}.json");
        return dto?.ToModel(id);
    }

    public async Task<List<MealCatalogItem>> GetFilteredMealsAsync(List<Cuisine>? cuisines, MealType? mealType)
    {
        var meals = await GetAllMealsAsync();
        var query = meals.AsEnumerable();

        if (cuisines is not null && cuisines.Count > 0)
        {
            query = query.Where(m => cuisines.Contains(m.Cuisine));
        }

        if (mealType is not null)
        {
            query = query.Where(m => m.MealType == mealType.Value);
        }

        return query.ToList();
    }

    public async Task UpsertMealAsync(MealCatalogItem meal)
    {
        var dto = MealCatalogItemDto.FromModel(meal);
        await _http.PutAsJsonAsync($"{_baseUrl}/mealCatalog/{meal.Id}.json", dto);
    }

    public async Task<bool> IsCatalogSeededAsync()
    {
        var flag = await _http.GetFromJsonAsync<bool?>($"{_baseUrl}/catalogSeeded.json");
        return flag == true;
    }

    public async Task SeedCatalogAsync(List<MealCatalogItem> meals)
    {
        foreach (var meal in meals)
        {
            await UpsertMealAsync(meal);
        }
        await _http.PutAsJsonAsync($"{_baseUrl}/catalogSeeded.json", true);
    }

    private class MealCatalogItemDto
    {
        public string Name { get; set; } = string.Empty;
        public Cuisine Cuisine { get; set; }
        public MealType MealType { get; set; }
        public List<string>? Ingredients { get; set; }
        public List<string>? SideDishes { get; set; }

        public static MealCatalogItemDto FromModel(MealCatalogItem m) => new()
        {
            Name = m.Name,
            Cuisine = m.Cuisine,
            MealType = m.MealType,
            Ingredients = m.Ingredients,
            SideDishes = m.SideDishes
        };

        public MealCatalogItem ToModel(string id) => new()
        {
            Id = id,
            Name = Name,
            Cuisine = Cuisine,
            MealType = MealType,
            Ingredients = Ingredients ?? new(),
            SideDishes = SideDishes ?? new()
        };
    }
}
