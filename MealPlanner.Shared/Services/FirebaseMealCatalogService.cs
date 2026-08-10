using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public class FirebaseMealCatalogService : IMealCatalogService
{
    private const string CatalogPath = "MealCatalog";
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public FirebaseMealCatalogService(HttpClient http, FirebaseOptions options)
    {
        _http = http;
        _baseUrl = options.DatabaseUrl.TrimEnd('/');
    }

    public async Task<List<MealCatalogItem>> GetAllMealsAsync()
    {
        var all = await _http.GetFromJsonAsync<Dictionary<string, MealCatalogItemDto>>($"{_baseUrl}/{CatalogPath}.json");
        if (all is null) return new List<MealCatalogItem>();

        return all.Select(kvp => kvp.Value.ToModel(kvp.Key)).ToList();
    }

    public async Task<MealCatalogItem?> GetMealByIdAsync(string id)
    {
        var dto = await _http.GetFromJsonAsync<MealCatalogItemDto>($"{_baseUrl}/{CatalogPath}/{id}.json");
        return dto?.ToModel(id);
    }

    public async Task<List<MealCatalogItem>> GetFilteredMealsAsync(MealType? mealType)
    {
        var meals = await GetAllMealsAsync();
        var query = meals.AsEnumerable();

        if (mealType is not null)
        {
            query = query.Where(m => m.MealType == mealType.Value);
        }

        return query.ToList();
    }

    public async Task UpsertMealAsync(MealCatalogItem meal)
    {
        if (string.IsNullOrWhiteSpace(meal.Id)) throw new ArgumentException("A meal ID is required.", nameof(meal));
        var dto = MealCatalogItemDto.FromModel(meal);
        await _http.PutAsJsonAsync($"{_baseUrl}/{CatalogPath}/{meal.Id}.json", dto);
    }

    public async Task<MealCatalogItem> CreateMealAsync(MealCatalogItem meal)
    {
        meal.Name = NormalizeDisplayName(meal.Name);
        if (string.IsNullOrWhiteSpace(meal.Name)) throw new ArgumentException("Meal name is required.", nameof(meal));
        var existing = (await GetAllMealsAsync()).FirstOrDefault(x => NormalizeName(x.Name) == NormalizeName(meal.Name));
        if (existing is not null) return existing;
        meal.Id = string.IsNullOrWhiteSpace(meal.Id) ? Guid.NewGuid().ToString("N") : meal.Id;
        meal.CreatedAt = meal.CreatedAt == default ? DateTimeOffset.UtcNow : meal.CreatedAt;
        await UpsertMealAsync(meal);
        return meal;
    }

    public Task UpdateMealAsync(MealCatalogItem meal) => UpsertMealAsync(meal);
    public Task DeleteMealAsync(string id) => _http.DeleteAsync($"{_baseUrl}/{CatalogPath}/{id}.json");
    public async Task<List<MealCatalogItem>> SearchMealsAsync(string query)
    {
        var normalized = NormalizeName(query);
        return string.IsNullOrEmpty(normalized) ? await GetAllMealsAsync() : (await GetAllMealsAsync()).Where(x => NormalizeName(x.Name).Contains(normalized)).ToList();
    }

    public async Task<bool> IsCatalogSeededAsync()
    {
        return (await GetAllMealsAsync()).Count > 0;
    }

    public async Task SeedCatalogAsync(List<MealCatalogItem> meals)
    {
        var existing = await GetAllMealsAsync();
        var existingNames = existing.Select(x => NormalizeName(x.Name)).ToHashSet(StringComparer.Ordinal);
        var existingIds = existing.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var seed in meals)
        {
            if (existingIds.Contains(seed.Id) || existingNames.Contains(NormalizeName(seed.Name))) continue;
            seed.IsSeed = true;
            seed.CreatedAt = seed.CreatedAt == default ? DateTimeOffset.UtcNow : seed.CreatedAt;
            await UpsertMealAsync(seed);
        }
    }

    private class MealCatalogItemDto
    {
        public string Name { get; set; } = string.Empty;
        public MealType MealType { get; set; }
        public List<string>? Ingredients { get; set; }
        public List<string>? SideDishes { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public bool IsSeed { get; set; }
        public List<string>? Tags { get; set; }
        public string? ImageUrl { get; set; }
        public int? PreparationMinutes { get; set; }
        public bool IsArchived { get; set; }

        public static MealCatalogItemDto FromModel(MealCatalogItem m) => new()
        {
            Name = m.Name,
            MealType = m.MealType,
            Ingredients = m.Ingredients,
            SideDishes = m.SideDishes,
            CreatedAt = m.CreatedAt,
            CreatedBy = m.CreatedBy,
            IsSeed = m.IsSeed,
            Tags = m.Tags,
            ImageUrl = m.ImageUrl,
            PreparationMinutes = m.PreparationMinutes,
            IsArchived = m.IsArchived
        };

        public MealCatalogItem ToModel(string id) => new()
        {
            Id = id,
            Name = Name,
            MealType = MealType,
            Ingredients = Ingredients ?? new(),
            SideDishes = SideDishes ?? new(),
            CreatedAt = CreatedAt,
            CreatedBy = CreatedBy,
            IsSeed = IsSeed,
            Tags = Tags ?? new(),
            ImageUrl = ImageUrl,
            PreparationMinutes = PreparationMinutes,
            IsArchived = IsArchived
        };
    }

    private static string NormalizeName(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    private static string NormalizeDisplayName(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
