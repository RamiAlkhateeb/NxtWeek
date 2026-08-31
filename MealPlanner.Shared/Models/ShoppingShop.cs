namespace MealPlanner.Shared.Models;

public class ShoppingShop
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, ShoppingItem> Items { get; set; } = new();
}
