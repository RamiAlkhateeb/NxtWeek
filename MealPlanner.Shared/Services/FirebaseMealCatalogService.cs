using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public sealed class FirebaseMealCatalogService(FirebaseRest db) : IMealCatalogService
{
    private const string CatalogPath = "MealCatalog";
    private static string MealPath(string id) => $"{CatalogPath}/{FirebaseRest.Key(id)}";

    public async Task<List<MealCatalogItem>> GetAllMealsAsync()
    {
        var all = await db.GetAsync<Dictionary<string, MealCatalogItemDto>>(CatalogPath);
        if (all is null) return new List<MealCatalogItem>();

        return all.Select(kvp => kvp.Value.ToModel(kvp.Key)).ToList();
    }

    public async Task<MealCatalogItem?> GetMealByIdAsync(string id)
    {
        var dto = await db.GetAsync<MealCatalogItemDto>(MealPath(id));
        return dto?.ToModel(id);
    }

    public async Task UpsertMealAsync(MealCatalogItem meal)
    {
        if (string.IsNullOrWhiteSpace(meal.Id)) throw new ArgumentException("A meal ID is required.", nameof(meal));
        var dto = MealCatalogItemDto.FromModel(meal);
        await db.PutAsync(MealPath(meal.Id), dto);
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

    public async Task SaveStepsAsync(string mealId, List<RecipeStep> steps)
    {
        await db.PatchAsync(MealPath(mealId), new { steps });
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

        await db.PatchAsync(MealPath(row.Id), patch);
        // A later seed with the same name (an older row matched twice) must see this row as filled.
        if (patch.ContainsKey("nutrition")) row.Nutrition = seed.Nutrition;
        if (patch.ContainsKey("ingredientLines")) row.IngredientLines = seed.IngredientLines;
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
        public List<RecipeStep>? Steps { get; set; }
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
            Steps = m.Steps.Count > 0 ? m.Steps : null,
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
            Steps = Steps ?? new(),
            SuitableSlots = SuitableSlots ?? new()
        };
    }

    private static string NormalizeName(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    private static string NormalizeDisplayName(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
