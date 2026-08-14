using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MealPlanner.Shared.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MealType
{
    Meat,
    Chicken,
    Fish,
    Vegetarian,
    Vegan
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
}
