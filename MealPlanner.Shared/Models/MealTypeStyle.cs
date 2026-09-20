namespace MealPlanner.Shared.Models;

/// <summary>
/// Maps a <see cref="MealType"/> to the CSS class suffix used for its colour
/// treatment (illustration tile background, badge colour). One place to add a
/// meal type's styling hook instead of repeating a switch in every component.
/// </summary>
public static class MealTypeStyle
{
    public static string Slug(MealType? type) => type switch
    {
        MealType.Meat => "meat",
        MealType.Chicken => "chicken",
        MealType.Fish => "fish",
        MealType.Vegetarian => "vegetarian",
        MealType.Vegan => "vegan",
        _ => "default"
    };

    /// <summary>Class for the soft-tinted tile behind a meal illustration.</summary>
    public static string TileClass(MealType? type) => $"type-tile-{Slug(type)}";

    /// <summary>Class for a type badge/chip.</summary>
    public static string BadgeClass(MealType? type) => $"type-badge-{Slug(type)}";
}
