namespace MealPlanner.Shared.Nutrition;

/// <summary>One line of a shared shopping list: the item and its amount as shown on screen (may be empty).</summary>
public sealed record ShareLine(string Name, string Amount);

/// <summary>A store's open items, in shop order. <see cref="Store"/> is null when the list has only one store.</summary>
public sealed record ShareGroup(string? Store, IReadOnlyList<ShareLine> Lines);

/// <summary>
/// The shopping list as plain text for WhatsApp and similar: a title, then each store's open items as "• name: amount".
/// Bought items are left out. Pure, so it is tested and reads the same in every language.
/// </summary>
public static class ShoppingText
{
    public static string Build(string title, IEnumerable<ShareGroup> groups)
    {
        var parts = new List<string> { title };
        foreach (var group in groups.Where(g => g.Lines.Count > 0))
        {
            var block = new List<string>();
            if (!string.IsNullOrWhiteSpace(group.Store)) block.Add($"*{group.Store.Trim()}*");
            block.AddRange(group.Lines.Select(l => string.IsNullOrWhiteSpace(l.Amount) ? $"• {l.Name.Trim()}" : $"• {l.Name.Trim()}: {l.Amount.Trim()}"));
            parts.Add(string.Join("\n", block));
        }
        return string.Join("\n\n", parts);
    }
}
