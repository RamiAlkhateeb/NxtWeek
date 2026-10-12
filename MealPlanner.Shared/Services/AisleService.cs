namespace MealPlanner.Shared.Services;

public sealed record Aisle(string Key, string Color, string LabelKey);

/// <summary>Sorts a shopping item into a supermarket aisle from its name (ar/en/de keywords).</summary>
public interface IAisleService
{
    IReadOnlyList<Aisle> All { get; }
    Aisle Get(string? key);
    /// <summary>The aisle stored on the item, or one guessed from its name.</summary>
    Aisle For(string name, string? storedKey = null);
}

public sealed class AisleService : IAisleService
{
    public static readonly Aisle Produce = new("produce", "#22a85a", "aisle_produce");
    public static readonly Aisle Bakery = new("bakery", "#e0a458", "aisle_bakery");
    public static readonly Aisle Dairy = new("dairy", "#6aa7d8", "aisle_dairy");
    public static readonly Aisle Meat = new("meat", "#e8553f", "aisle_meat");
    public static readonly Aisle Pantry = new("pantry", "#a47e4f", "aisle_pantry");
    public static readonly Aisle Other = new("other", "#98989f", "aisle_other");

    public IReadOnlyList<Aisle> All { get; } = [Produce, Bakery, Dairy, Meat, Pantry, Other];

    // Checked in order, first hit wins ("زيت زيتون" must reach pantry before "زيتون" hits produce).
    private static readonly (Aisle Aisle, string[] Words)[] Rules =
    [
        (Pantry, ["زيت", "أرز", "رز", "برغل", "عدس", "حمص", "فول", "معكرونة", "مكرونة", "سكر", "ملح", "طحين", "دقيق", "بهارات", "كمون", "صلصة", "رب ", "معلب", "تونة", "شاي", "قهوة",
                  "oil", "rice", "bulgur", "lentil", "chickpea", "pasta", "sugar", "salt", "flour", "spice", "sauce", "tuna", "tea", "coffee", "beans",
                  "öl", "reis", "linsen", "nudel", "zucker", "salz", "mehl", "gewürz", "soße", "sosse", "thunfisch", "tee", "kaffee", "bohnen"]),
        (Bakery, ["خبز", "عجين", "كعك", "معجنات", "مناقيش", "bread", "dough", "bun", "pita", "toast", "baguette", "brot", "teig", "brötchen", "brezel"]),
        (Dairy, ["حليب", "لبن", "جبن", "جبنة", "زبدة", "قشطة", "كريمة", "بيض", "زبادي", "milk", "yogurt", "yoghurt", "cheese", "butter", "cream", "egg", "milch", "joghurt", "käse", "kaese", "sahne", "ei "]),
        (Meat, ["لحم", "لحمة", "دجاج", "فخاد", "صدور", "سمك", "قريدس", "شرمبس", "كفتة", "مفروم", "meat", "beef", "lamb", "chicken", "fish", "salmon", "shrimp", "mince", "sausage", "fleisch", "rind", "hähnchen", "huhn", "fisch", "lachs", "hack", "wurst"]),
        (Produce, ["بندورة", "طماطم", "بصل", "ثوم", "بطاطا", "بطاطس", "جزر", "خيار", "خس", "ليمون", "بقدونس", "كزبرة", "نعنع", "فلفل", "باذنجان", "كوسا", "ملوخية", "فاصوليا", "بازلاء", "فطر", "تفاح", "موز", "برتقال", "زيتون", "ورق عنب", "خضار", "فواكه", "سبانخ", "ريحان",
                    "tomato", "onion", "garlic", "potato", "carrot", "cucumber", "lettuce", "lemon", "parsley", "coriander", "mint", "pepper", "eggplant", "zucchini", "spinach", "mushroom", "apple", "banana", "orange", "olive", "herb", "basil", "vegetable", "fruit",
                    "tomate", "zwiebel", "knoblauch", "kartoffel", "möhre", "karotte", "gurke", "salat", "zitrone", "petersilie", "paprika", "aubergine", "zucchini", "spinat", "pilz", "apfel", "banane", "orange", "gemüse", "obst"]),
    ];

    public Aisle Get(string? key) => All.FirstOrDefault(a => a.Key == key) ?? Other;

    public Aisle For(string name, string? storedKey = null)
    {
        if (!string.IsNullOrWhiteSpace(storedKey)) return Get(storedKey);
        var lower = (name ?? "").Trim().ToLowerInvariant() + " ";
        foreach (var (aisle, words) in Rules)
            if (words.Any(w => lower.Contains(w, StringComparison.OrdinalIgnoreCase))) return aisle;
        return Other;
    }
}
