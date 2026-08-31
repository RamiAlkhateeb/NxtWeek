namespace MealPlanner.Shared.Models;

public class ShoppingItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsBought { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? BoughtAtUtc { get; set; }
}
