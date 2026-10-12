using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>Resolves the picture shown for a meal on cards, sheets and detail pages.</summary>
public interface IMealImageService
{
    /// <summary>Loads the signed-in user's own meal photos once. Pages call this before they render meals.</summary>
    Task EnsureLoadedAsync();
    bool HasPhoto(string mealId);
    /// <summary>Stores a picture (a small data: URL) for a meal, for this user only.</summary>
    Task SetPhotoAsync(string mealId, string dataUrl);
    Task RemovePhotoAsync(string mealId);

    string GetImageUrl(MealCatalogItem? meal);
    string GetImageUrl(Meal? meal);

    /// <summary>Resolution for a meal we only know by name and type (cached week rows).</summary>
    string GetImageUrl(string? name, MealType? mealType, IEnumerable<string>? tags = null, string? photoUrl = null);
}
