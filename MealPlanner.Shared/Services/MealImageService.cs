using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
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
public class MealImageService(HttpClient http, FirebaseOptions options, IAuthService auth) : IMealImageService
{
    // A user's own pictures are kept under their profile (not in the shared catalog, which every page downloads
    // in full) and read into memory once.
    private readonly Dictionary<string, string> _photos = new();
    private string? _loadedFor;
    private string DbUrl(string path) => $"{options.DatabaseUrl.TrimEnd('/')}/{path}.json";

    public async Task EnsureLoadedAsync()
    {
        var user = await auth.GetCurrentUserAsync();
        if (user is null || string.IsNullOrWhiteSpace(user.Uid) || _loadedFor == user.Uid) return;
        try
        {
            var stored = await http.GetFromJsonAsync<Dictionary<string, string>>(DbUrl($"users/{Uri.EscapeDataString(user.Uid)}/mealPhotos"));
            _photos.Clear();
            foreach (var pair in stored ?? new()) _photos[pair.Key] = pair.Value;
            _loadedFor = user.Uid;
        }
        catch { /* offline: fall back to the illustrations */ }
    }

    public bool HasPhoto(string mealId) => _photos.ContainsKey(mealId);

    public async Task SetPhotoAsync(string mealId, string dataUrl)
    {
        var user = await auth.GetCurrentUserAsync() ?? throw new InvalidOperationException("Not signed in.");
        using var response = await http.PutAsJsonAsync(DbUrl($"users/{Uri.EscapeDataString(user.Uid)}/mealPhotos/{Uri.EscapeDataString(mealId)}"), dataUrl);
        response.EnsureSuccessStatusCode();
        _photos[mealId] = dataUrl;
    }

    public async Task RemovePhotoAsync(string mealId)
    {
        var user = await auth.GetCurrentUserAsync() ?? throw new InvalidOperationException("Not signed in.");
        using var response = await http.DeleteAsync(DbUrl($"users/{Uri.EscapeDataString(user.Uid)}/mealPhotos/{Uri.EscapeDataString(mealId)}"));
        response.EnsureSuccessStatusCode();
        _photos.Remove(mealId);
    }

    private const string BasePath = "img/meals/";

    /// <summary>Illustrations that exist on disk under <see cref="BasePath"/>.</summary>
    private static readonly HashSet<string> Archetypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "meat", "chicken", "fish", "vegetarian",
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
        ("vegetarian", ["نباتي", "vegetarian", "veggie", "cheese", "vegetarisch", "käse", "kaese", "gemüse", "gemuese", "عدس", "فول", "حمص", "vegan", "lentil", "chickpea", "linsen", "erbsen", "grünkohl", "gruenkohl", "kürbis", "kuerbis"])
    ];

    public string GetImageUrl(MealCatalogItem? meal) =>
        meal is null ? Url("vegetarian") : GetImageUrl(meal.Name, meal.MealType, meal.Tags, OwnPhoto(meal.Id) ?? meal.PhotoUrl);

    public string GetImageUrl(Meal? meal) =>
        meal is null ? Url("vegetarian") : GetImageUrl(meal.Name, meal.MealType, tags: null, OwnPhoto(meal.MealId) ?? meal.PhotoUrl);

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
            _ => "vegetarian"
        });
    }

    private string? OwnPhoto(string? mealId) => !string.IsNullOrWhiteSpace(mealId) && _photos.TryGetValue(mealId, out var url) ? url : null;

    private static string Url(string archetype) => $"{BasePath}{archetype}.svg";
}
