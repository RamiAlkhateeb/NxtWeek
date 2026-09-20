using System;
using System.Collections.Generic;

namespace MealPlanner.Shared.Models;

/// <summary>
/// Quick-pick side dishes offered in the meal editor. The list is per language:
/// a German cook is not looking for "مخلل", and translating the Arabic list would
/// produce side dishes nobody actually serves.
/// </summary>
public static class SideDishOptions
{
    private static readonly Dictionary<string, List<string>> ByLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = new() { "أرز", "خبز", "سلطة", "مخلل", "لبن", "بطاطا مقلية" },
        ["en"] = new() { "Rice", "Bread", "Salad", "Pickles", "Yogurt", "French fries" },
        ["de"] = new() { "Reis", "Brot", "Salat", "Gewürzgurken", "Kartoffeln", "Pommes frites" }
    };

    /// <summary>Side dishes for the given language code, falling back to Arabic.</summary>
    public static IReadOnlyList<string> For(string? language) =>
        ByLanguage.TryGetValue(language ?? "", out var list) ? list : ByLanguage["ar"];

    /// <summary>Arabic options, kept for callers that have no language context.</summary>
    public static IReadOnlyList<string> Options => ByLanguage["ar"];
}
