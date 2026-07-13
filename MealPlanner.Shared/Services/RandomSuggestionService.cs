using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public class RandomSuggestionService : ISuggestionService
{
    private readonly List<string> _pool = new()
    {
        "رز وعدس", "شوربة خضار", "دجاج مشوي مع رز", "فتوش وفلافل",
        "معكرونة بالصلصة", "مفركة بطاطا", "فتة حمص وفول", "ملوخية مع أرز",
        "مسخن دجاج", "شوربة عدس مع خبز", "رز وفاصولية حب مع لحمة", "كبسة دجاج"
    };

    public Task<List<string>> GetSuggestionsAsync(List<Meal> recentMeals, int count = 5)
    {
        var usedNames = recentMeals.Select(m => m.Name).ToHashSet();
        var available = _pool.Where(m => !usedNames.Contains(m)).ToList();

        var rng = new Random();
        var picks = available
            .OrderBy(_ => rng.Next())
            .Take(count)
            .ToList();

        return Task.FromResult(picks);
    }
}