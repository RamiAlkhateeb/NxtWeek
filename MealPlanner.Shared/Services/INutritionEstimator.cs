using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;

namespace MealPlanner.Shared.Services;

/// <summary>Estimates per-serving nutrition for a meal. Results always carry <see cref="NutritionSource.Estimate"/>.</summary>
public interface INutritionEstimator
{
    Task<NutritionInfo> EstimateAsync(MealCatalogItem meal, CancellationToken cancellationToken = default);
}

/// <summary>
/// "Estimate from ingredients": asks the AI (Gemini with the user's key, otherwise the photo worker) and fills missing
/// ingredient lines on the meal while at it; when that fails, sums a small ingredient table. Either way the result is an estimate.
/// </summary>
public sealed class AiNutritionEstimator(IMealDetailsService details) : INutritionEstimator
{
    public async Task<NutritionInfo> EstimateAsync(MealCatalogItem meal, CancellationToken cancellationToken = default)
    {
        SuggestedMealDetails? suggestion = null;
        try { suggestion = await details.SuggestAsync(meal); }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { /* the table below */ }
        cancellationToken.ThrowIfCancellationRequested();
        if (suggestion is not null)
        {
            // Work on a copy so the nutrition comes back fresh even when the meal already has some.
            var copy = new MealCatalogItem { Ingredients = meal.Ingredients.ToList(), IngredientLines = meal.IngredientLines.ToList(), ServingsYield = meal.ServingsYield };
            MealDetailsMapper.Apply(copy, suggestion);
            if (meal.IngredientLines.Count == 0)
            {
                meal.IngredientLines = copy.IngredientLines;
                if (meal.Ingredients.Count == 0) meal.Ingredients = copy.Ingredients;
                meal.ServingsYield = copy.ServingsYield;
            }
            return copy.Nutrition!;
        }
        return IngredientEstimate.For(meal);
    }
}
