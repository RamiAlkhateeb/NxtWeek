using System;
using System.Collections.Generic;
using System.Linq;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>
/// Every meal gets a picture, including ones a user just typed in. Rather than
/// depending on remote stock photography we ship a small set of flat dish
/// illustrations and pick the closest one:
///
///   1. an explicit <see cref="MealCatalogItem.PhotoUrl"/> the user supplied,
///   2. an archetype tag on the meal (seeded meals carry these),
///   3. a keyword in the meal name, matched in every supported language,
///   4. the meal type as a last resort.
///
/// The keyword table lives here, not in the pages, so adding a language means
/// touching exactly one place.
/// </summary>
public class MealImageService : IMealImageService
{
    private const string BasePath = "img/meals/";

    /// <summary>Illustrations that exist on disk under <see cref="BasePath"/>.</summary>
    private static readonly HashSet<string> Archetypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "meat", "chicken", "fish", "vegetarian", "vegan",
        "pasta", "soup", "rice", "bread", "salad", "burger", "pizza", "stew", "eggs", "potato"
    };

    /// <summary>
    /// archetype -> name fragments that imply it, across ar/en/de. Matching is
    /// case-insensitive substring, so fragments should be distinctive stems
    /// ("nudel" catches "Nudeln", "Bandnudeln").
    /// </summary>
    private static readonly (string Archetype, string[] Keywords)[] Keywords =
    [
        ("soup",    ["شوربة", "حساء", "soup", "broth", "suppe", "eintopf", "brühe"]),
        ("pasta",   ["معكرونة", "مكرونة", "باستا", "pasta", "spaghetti", "noodle", "lasagn", "penne", "linguine",
                     "nudel", "spätzle", "spaetzle", "maultasche"]),
        ("pizza",   ["بيتزا", "مناقيش", "pizza", "flammkuchen"]),
        ("burger",  ["برغر", "همبرغر", "burger", "sandwich", "wrap", "شاورما", "döner", "doener"]),
        ("rice",    ["أرز", "رز", "برغل", "مجدرة", "rice", "risotto", "reis", "bulgur"]),
        ("salad",   ["سلطة", "salad", "salat", "coleslaw"]),
        ("potato",  ["بطاطا", "بطاطس", "potato", "fries", "chips", "kartoffel", "pommes", "rösti", "roesti",
                     "puffer", "püree", "pueree", "schnitzel", "currywurst"]),
        ("bread",   ["خبز", "فتة", "bread", "toast", "brot", "brezn", "brezel", "baguette", "brotzeit"]),
        ("eggs",    ["بيض", "egg", "omelet", "shakshuka", "شكشوكة", "ei ", "eier", "spiegelei", "pfannkuchen"]),
        ("fish",    ["سمك", "تونة", "قريدس", "شرمبس", "fish", "salmon", "tuna", "shrimp", "prawn", "scampi",
                     "fisch", "lachs", "forelle", "matjes", "thunfisch"]),
        ("chicken", ["دجاج", "فروج", "طاووق", "chicken", "poultry", "hähnchen", "haehnchen", "huhn", "geflügel",
                     "geschnetzeltes", "frikassee"]),
        ("stew",    ["يخنة", "محشي", "ملوخية", "stew", "casserole", "curry", "pie", "gulasch", "roulade",
                     "auflauf", "klopse", "geschmort"]),
        ("meat",    ["لحم", "لحمة", "كبة", "كفتة", "meat", "beef", "lamb", "steak", "mince", "fleisch", "rind",
                     "hack", "frikadelle", "wurst", "bratwurst"]),
        ("vegan",   ["نباتي صرف", "عدس", "فول", "حمص", "vegan", "lentil", "chickpea", "linsen", "erbsen",
                     "grünkohl", "gruenkohl", "kürbis", "kuerbis"]),
        ("vegetarian", ["نباتي", "vegetarian", "veggie", "cheese", "vegetarisch", "käse", "kaese", "gemüse", "gemuese"])
    ];

    public string GetImageUrl(MealCatalogItem? meal) =>
        meal is null ? Url("vegetarian") : GetImageUrl(meal.Name, meal.MealType, meal.Tags, meal.PhotoUrl);

    public string GetImageUrl(Meal? meal) =>
        meal is null ? Url("vegetarian") : GetImageUrl(meal.Name, meal.MealType, tags: null, meal.PhotoUrl);

    public string GetImageUrl(string? name, MealType? mealType, IEnumerable<string>? tags = null, string? photoUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(photoUrl)) return photoUrl;

        if (tags is not null)
        {
            var tagged = tags.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t) && Archetypes.Contains(t.Trim()));
            if (tagged is not null) return Url(tagged.Trim().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var haystack = name.ToLowerInvariant();
            foreach (var (archetype, keywords) in Keywords)
            {
                if (keywords.Any(k => haystack.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    return Url(archetype);
                }
            }
        }

        return Url(mealType switch
        {
            MealType.Meat => "meat",
            MealType.Chicken => "chicken",
            MealType.Fish => "fish",
            MealType.Vegan => "vegan",
            _ => "vegetarian"
        });
    }

    private static string Url(string archetype) => $"{BasePath}{archetype}.svg";
}
