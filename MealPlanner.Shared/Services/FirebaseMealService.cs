using System.Net.Http.Json;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public class FirebaseMealService : IMealService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public FirebaseMealService(HttpClient http, FirebaseOptions options)
    {
        _http = http;
        _baseUrl = options.DatabaseUrl.TrimEnd('/');
    }

    public async Task<List<Meal>> GetWeekAsync(DateOnly start, DateOnly end)
    {
        var all = await _http.GetFromJsonAsync<Dictionary<string, MealDto>>($"{_baseUrl}/meals.json");
        if (all is null) return new List<Meal>();

        return all
            .Select(kvp => kvp.Value.ToModel(kvp.Key))
            .Where(m => m.Date >= start && m.Date <= end)
            .OrderBy(m => m.Date)
            .ToList();
    }

    public async Task UpsertMealAsync(Meal meal)
    {
        var dto = MealDto.FromModel(meal);
        await _http.PutAsJsonAsync($"{_baseUrl}/meals/{meal.Id}.json", dto);
    }

    public async Task<bool> IsSeededAsync()
    {
        var flag = await _http.GetFromJsonAsync<bool?>($"{_baseUrl}/seeded.json");
        return flag == true;
    }

    public async Task SeedAsync(List<Meal> meals)
    {
        foreach (var meal in meals)
            await UpsertMealAsync(meal);

        await _http.PutAsJsonAsync($"{_baseUrl}/seeded.json", true);
    }

    private class MealDto
    {
        public string Date { get; set; } = string.Empty;
        public string DayName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }

        public static MealDto FromModel(Meal m) => new()
        {
            Date = m.Date.ToString("yyyy-MM-dd"),
            DayName = m.DayName,
            Name = m.Name,
            PhotoUrl = m.PhotoUrl
        };

        public Meal ToModel(string id) => new()
        {
            Id = id,
            Date = DateOnly.Parse(Date),
            DayName = DayName,
            Name = Name,
            PhotoUrl = PhotoUrl
        };
    }
}