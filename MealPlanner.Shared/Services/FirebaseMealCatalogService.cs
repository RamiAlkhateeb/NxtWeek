using System;
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

    public async Task<List<MealCatalogItem>> GetFilteredMealsAsync(MealType? mealType, string? language = null)
    {
        var meals = await GetAllMealsAsync();
        var query = meals.AsEnumerable();

        if (mealType is not null)
        {
            query = query.Where(m => m.MealType == mealType.Value);
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            query = query.Where(m => string.Equals(m.Language, language, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }

    public async Task UpsertMealAsync(MealCatalogItem meal)
    {
        if (string.IsNullOrWhiteSpace(meal.Id)) throw new ArgumentException("A meal ID is required.", nameof(meal));
        var dto = MealCatalogItemDto.FromModel(meal);
        using var response = await _http.PutAsJsonAsync($"{_baseUrl}/{CatalogPath}/{meal.Id}.json", dto);
        response.EnsureSuccessStatusCode();
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
    public async Task DeleteMealAsync(string id)
    {
        using var response = await _http.DeleteAsync($"{_baseUrl}/{CatalogPath}/{id}.json");
        response.EnsureSuccessStatusCode();
    }
    public async Task<List<MealCatalogItem>> SearchMealsAsync(string query)
    {
        var normalized = NormalizeName(query);
        return string.IsNullOrEmpty(normalized) ? await GetAllMealsAsync() : (await GetAllMealsAsync()).Where(x => NormalizeName(x.Name).Contains(normalized)).ToList();
    }

    /// <summary>
    /// True only when every language the app ships already has meals in the catalog.
    /// Checking a fixed pair of languages here would silently skip the seed for any
    /// language added later.
    /// </summary>
    public async Task<bool> IsCatalogSeededAsync()
    {
        var seeded = await GetSeededLanguagesAsync();
        return LocalizationService.SupportedLanguageCodes.All(seeded.Contains);
    }

    public async Task<IReadOnlyCollection<string>> GetSeededLanguagesAsync()
    {
        var all = await GetAllMealsAsync();
        return all
            .Select(m => string.IsNullOrWhiteSpace(m.Language) ? "ar" : m.Language)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task SeedCatalogAsync(List<MealCatalogItem> meals)
    {
        var existing = await GetAllMealsAsync();
        var existingNames = existing.Select(x => NormalizeName(x.Name)).ToHashSet(StringComparer.Ordinal);
        var existingIds = existing.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var seed in meals)
        {
            if (existingIds.Contains(seed.Id) || existingNames.Contains(NormalizeName(seed.Name)))
            {
                await BackfillNutritionAsync(existing, seed);
                continue;
            }
            seed.IsSeed = true;
            seed.CreatedAt = seed.CreatedAt == default ? DateTimeOffset.UtcNow : seed.CreatedAt;
            await UpsertMealAsync(seed);
        }
    }

    // A seed meal that already exists (same id, or a same-named older seed row) gets the
    // seed's nutrition, measured ingredients and slots, but only where it has none yet,
    // so values a user entered are never overwritten.
    private async Task BackfillNutritionAsync(List<MealCatalogItem> existing, MealCatalogItem seed)
    {
        if (seed.Nutrition is null && seed.IngredientLines.Count == 0) return;
        var row = existing.FirstOrDefault(x => x.Id == seed.Id)
            ?? existing.FirstOrDefault(x => x.IsSeed && x.Language == seed.Language && NormalizeName(x.Name) == NormalizeName(seed.Name));
        if (row is null) return;

        var patch = new Dictionary<string, object>();
        if (row.Nutrition is null && seed.Nutrition is not null) patch["nutrition"] = seed.Nutrition;
        if (row.IngredientLines.Count == 0 && seed.IngredientLines.Count > 0)
        {
            patch["ingredientLines"] = seed.IngredientLines;
            patch["servingsYield"] = seed.ServingsYield;
        }
        if (row.SuitableSlots.Count == 0 && seed.SuitableSlots.Count > 0) patch["suitableSlots"] = seed.SuitableSlots;
        if (row.PreparationMinutes is null && seed.PreparationMinutes is not null) patch["preparationMinutes"] = seed.PreparationMinutes;
        if (patch.Count == 0) return;

        using var response = await _http.PatchAsJsonAsync($"{_baseUrl}/{CatalogPath}/{row.Id}.json", patch);
        response.EnsureSuccessStatusCode();
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
        public string PhotoUrl { get; set; } = string.Empty;
        public int? PreparationMinutes { get; set; }
        public bool IsArchived { get; set; }
        public string? Language { get; set; }
        public NutritionInfo? Nutrition { get; set; }
        public int? ServingsYield { get; set; }
        public List<IngredientLine>? IngredientLines { get; set; }
        public List<MealSlot>? SuitableSlots { get; set; }

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
            PhotoUrl = m.PhotoUrl,
            PreparationMinutes = m.PreparationMinutes,
            IsArchived = m.IsArchived,
            Language = string.IsNullOrWhiteSpace(m.Language) ? "ar" : m.Language,
            Nutrition = m.Nutrition,
            ServingsYield = m.ServingsYield,
            IngredientLines = m.IngredientLines,
            SuitableSlots = m.SuitableSlots
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
            PhotoUrl = PhotoUrl,
            PreparationMinutes = PreparationMinutes,
            IsArchived = IsArchived,
            Language = string.IsNullOrWhiteSpace(Language) ? "ar" : Language,
            Nutrition = Nutrition,
            ServingsYield = ServingsYield is > 0 ? ServingsYield.Value : 1,
            IngredientLines = IngredientLines ?? new(),
            SuitableSlots = SuitableSlots ?? new()
        };
    }

    private static string NormalizeName(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    private static string NormalizeDisplayName(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
