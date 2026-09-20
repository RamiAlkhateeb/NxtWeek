using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>Resolves the picture shown for a meal on cards, sheets and detail pages.</summary>
public interface IMealImageService
{
    string GetImageUrl(MealCatalogItem? meal);
    string GetImageUrl(Meal? meal);

    /// <summary>Resolution for a meal we only know by name and type (cached week rows).</summary>
    string GetImageUrl(string? name, MealType? mealType, IEnumerable<string>? tags = null, string? photoUrl = null);
}
