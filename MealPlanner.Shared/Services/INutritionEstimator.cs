using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;

namespace MealPlanner.Shared.Services;

/// <summary>Estimates per-serving nutrition for a meal. Results always carry <see cref="NutritionSource.Estimate"/>.</summary>
public interface INutritionEstimator
{
    Task<NutritionInfo> EstimateAsync(MealCatalogItem meal, CancellationToken cancellationToken = default);
}

/// <summary>
/// Placeholder for the AI estimate: sums a small ingredient table. The short delay keeps the
/// "Estimating…" state honest about what the real call will feel like.
/// </summary>
public sealed class FakeNutritionEstimator : INutritionEstimator
{
    public async Task<NutritionInfo> EstimateAsync(MealCatalogItem meal, CancellationToken cancellationToken = default)
    {
        await Task.Delay(700, cancellationToken);
        return IngredientEstimate.For(meal);
    }
}
