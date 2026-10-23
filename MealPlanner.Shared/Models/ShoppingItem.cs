namespace MealPlanner.Shared.Models;

public class ShoppingItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsBought { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? BoughtAtUtc { get; set; }

    /// <summary>How many (or how much, together with <see cref="Unit"/>). Items from before quantities existed read as 1.</summary>
    public int Quantity { get; set; } = 1;
    public string? Unit { get; set; }

    /// <summary>Aisle key ("produce", "dairy"…). Null means "work it out from the name".</summary>
    public string? Aisle { get; set; }

    /// <summary>Name of the meal this ingredient was added for, when it came from one.</summary>
    public string? SourceMeal { get; set; }

    /// <summary>Set on items "Add this week's ingredients" created ("rice|g"); those are kept in line with the plan. Null for typed items.</summary>
    public string? IngredientKey { get; set; }

    /// <summary>How many planned meals need this item.</summary>
    public int UsedIn { get; set; }
}
