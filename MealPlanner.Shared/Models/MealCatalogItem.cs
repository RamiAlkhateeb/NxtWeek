using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MealPlanner.Shared.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Cuisine
{
    Syrian,
    Turkish,
    Lebanese
}

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
    public Cuisine Cuisine { get; set; }
    public MealType MealType { get; set; }
    public List<string> Ingredients { get; set; } = new();
    public List<string> SideDishes { get; set; } = new();
}
