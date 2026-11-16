namespace MealPlanner.Shared.Models;

/// <summary>
/// Two friends syncing meals and shopping. Stored on both sides at users/{key}/sync. Meals are mirrored both
/// ways; servings, eaten and skipped stay personal. Both use one shopping list, kept at users/{ListHost}/shopping.
/// </summary>
public class SyncLink
{
    public string Partner { get; set; } = string.Empty;
    public string ListHost { get; set; } = string.Empty;
    public DateOnly Since { get; set; }
}

/// <summary>A request to sync, stored with the person asked (users/{key}/syncRequest). One at a time.</summary>
public class SyncRequest
{
    public string From { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// The one number a friend sees for the protein challenge, published while nutrition sharing is on
/// (users/{key}/shareStats): protein days in the week of <see cref="Monday"/>. Never targets or body data.
/// </summary>
public class ShareStats
{
    public DateOnly Monday { get; set; }
    public int ProteinDays { get; set; }
}
