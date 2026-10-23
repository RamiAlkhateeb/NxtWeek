using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MealPlanner.Shared.Models;

[JsonConverter(typeof(MealTypeJsonConverter))]
public enum MealType
{
    Meat,
    Chicken,
    Fish,
    Vegetarian
}

/// <summary>
/// Reads and writes the enum as its name. "Vegan" was merged into Vegetarian, but rows
/// already stored in Firebase (and in the local cache) still say "Vegan"; throwing on
/// them would make the whole catalog unreadable, so they load as Vegetarian.
/// </summary>
public sealed class MealTypeJsonConverter : JsonConverter<MealType>
{
    public override MealType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Enum.TryParse<MealType>(reader.GetString(), ignoreCase: true, out var type) ? type : MealType.Vegetarian;

    public override void Write(Utf8JsonWriter writer, MealType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

public class MealCatalogItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MealType MealType { get; set; }
    public List<string> Ingredients { get; set; } = new();
    public List<string> SideDishes { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsSeed { get; set; }
    public List<string> Tags { get; set; } = new();
    public string PhotoUrl { get; set; } = string.Empty;
    public int? PreparationMinutes { get; set; }
    public bool IsArchived { get; set; }
    public string Language { get; set; } = "ar";
    // Per serving. Null means "No nutrition yet": the meal is left out of suggestions and totals.
    public NutritionInfo? Nutrition { get; set; }
    // How many servings one cooked batch makes.
    public int ServingsYield { get; set; } = 1;
    // Measured ingredients per serving, for the shopping list. Ingredients (free text) stays for older rows.
    public List<IngredientLine> IngredientLines { get; set; } = new();
}
