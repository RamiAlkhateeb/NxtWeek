using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;
using Nxt.UI.Ai;

namespace MealPlanner.Shared.Services;

public enum MealDetailsResult { Filled, AlreadyComplete, NoKey, Failed }

/// <summary>Ingredients and nutrition for a meal from Gemini, with the user's own key.</summary>
public interface IMealDetailsService
{
    Task<bool> IsAvailableAsync();
    Task<SuggestedMealDetails> SuggestAsync(MealCatalogItem meal);
    /// <summary>Fills a meal's missing ingredients and nutrition and saves it. Never throws.</summary>
    Task<MealDetailsResult> FillMissingAsync(MealCatalogItem meal);
}

public sealed class GeminiMealDetailsService(GeminiClient gemini, IMealCatalogService catalog) : IMealDetailsService
{
    private static readonly Dictionary<string, string> LanguageNames = new() { ["ar"] = "Arabic", ["en"] = "English", ["de"] = "German" };

    public Task<bool> IsAvailableAsync() => gemini.HasKeyAsync();

    public Task<SuggestedMealDetails> SuggestAsync(MealCatalogItem meal)
    {
        var language = LanguageNames.GetValueOrDefault(string.IsNullOrWhiteSpace(meal.Language) ? "ar" : meal.Language, "Arabic");
        var known = meal.Ingredients.Count > 0 ? $"The cook listed these ingredients, keep them and add amounts: {string.Join(", ", meal.Ingredients)}." : "";
        var prompt = $"""
            You are a nutrition assistant for a home-cooking meal planner.
            Dish: "{meal.Name}" (category: {meal.MealType}). {known}
            Give the ingredients for ONE typical home serving of this dish, with amounts, and its nutrition per serving.
            - "name": the ingredient in {language}, as a shopper would write it.
            - "id": the same ingredient in English camelCase (e.g. "chickenBreast", "rice", "oliveOil").
            - "unit": "g", "ml" or "pc"; "quantity": amount per serving in that unit.
            - "aisle": one of produce, meat, dairy, pantry, bakery, other.
            - "servings": how many servings the recipe usually makes.
            - kcal, protein, carbs, fat, fibre (grams) per serving; 4*protein + 4*carbs + 9*fat should be close to kcal.
            """;
        var schema = new
        {
            type = "OBJECT",
            properties = new
            {
                ingredients = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            id = new { type = "STRING" },
                            name = new { type = "STRING" },
                            quantity = new { type = "NUMBER" },
                            unit = new { type = "STRING", @enum = new[] { "g", "ml", "pc" } },
                            aisle = new { type = "STRING", @enum = new[] { "produce", "meat", "dairy", "pantry", "bakery", "other" } }
                        },
                        required = new[] { "id", "name", "quantity", "unit", "aisle" }
                    }
                },
                servings = new { type = "INTEGER" },
                kcal = new { type = "NUMBER" },
                protein = new { type = "NUMBER" },
                carbs = new { type = "NUMBER" },
                fat = new { type = "NUMBER" },
                fibre = new { type = "NUMBER" }
            },
            required = new[] { "ingredients", "servings", "kcal", "protein", "carbs", "fat", "fibre" }
        };
        return gemini.GenerateJsonAsync<SuggestedMealDetails>(prompt, schema, MealDetailsMapper.Validate);
    }

    public async Task<MealDetailsResult> FillMissingAsync(MealCatalogItem meal)
    {
        if (!MealDetailsMapper.NeedsDetails(meal)) return MealDetailsResult.AlreadyComplete;
        try
        {
            if (!await gemini.HasKeyAsync()) return MealDetailsResult.NoKey;
            var details = await SuggestAsync(meal);
            if (MealDetailsMapper.Apply(meal, details)) await catalog.UpdateMealAsync(meal);
            return MealDetailsResult.Filled;
        }
        catch (Exception)
        {
            return MealDetailsResult.Failed;
        }
    }
}
