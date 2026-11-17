namespace MealPlanner.Shared.Models;

public class ShoppingShop
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>When the store was made; stores from before this was kept read as the earliest.</summary>
    public DateTime CreatedAtUtc { get; set; }
    public Dictionary<string, ShoppingItem> Items { get; set; } = new();
    /// <summary>The store's web domain ("lidl.de"), found by Gemini from its name, for its logo.</summary>
    public string? LogoDomain { get; set; }
    /// <summary>An emoji for the store, shown when there is no logo.</summary>
    public string? Emoji { get; set; }
    /// <summary>True once Gemini was asked, so a store it does not know is not asked about again.</summary>
    public bool LogoChecked { get; set; }
}
