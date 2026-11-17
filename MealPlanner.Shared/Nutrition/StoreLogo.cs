using System.Globalization;
using System.Text.RegularExpressions;

namespace MealPlanner.Shared.Nutrition;

/// <summary>Gemini's answer about a store: its web domain (when it is a known chain) and an emoji.</summary>
public sealed class StoreLogoReply
{
    public string? Domain { get; set; }
    public string? Emoji { get; set; }
}

/// <summary>A store's look: the domain its logo is drawn from, and an emoji when there is none. Pure and tested.</summary>
public sealed record StoreLook(string? Domain, string? Emoji)
{
    public static readonly StoreLook None = new(null, null);
}

public static partial class StoreLogo
{
    [GeneratedRegex(@"^(?=.{4,63}$)([a-z0-9]([a-z0-9-]*[a-z0-9])?\.)+[a-z]{2,24}$")]
    private static partial Regex DomainPattern();

    /// <summary>
    /// Keeps only a plain domain ("www." and any scheme or path dropped) and a single short emoji; anything else is
    /// left out, so a wrong guess never shows a broken picture or text.
    /// </summary>
    public static StoreLook Clean(StoreLogoReply? reply)
    {
        if (reply is null) return StoreLook.None;
        var domain = (reply.Domain ?? "").Trim().ToLowerInvariant();
        domain = Regex.Replace(domain, @"^[a-z]+://", "");
        domain = domain.Split('/', '?', '#')[0];
        if (domain.StartsWith("www.", StringComparison.Ordinal)) domain = domain[4..];
        var emoji = (reply.Emoji ?? "").Trim();
        return new StoreLook(DomainPattern().IsMatch(domain) ? domain : null, IsEmoji(emoji) ? emoji : null);
    }

    /// <summary>The logo picture for a domain: the site's own icon, 128 px, served by Google's favicon service.</summary>
    public static string IconUrl(string domain) => $"https://www.google.com/s2/favicons?sz=128&domain={Uri.EscapeDataString(domain)}";

    /// <summary>The letter shown when a store has neither logo nor emoji.</summary>
    public static string Initial(string name)
    {
        var text = name.Trim();
        if (text.Length == 0) return "?";
        var first = StringInfo.GetNextTextElement(text);
        return first.ToUpper(CultureInfo.CurrentCulture);
    }

    // One emoji (possibly with joiners and variation selectors): no letters or digits, at most 3 text elements.
    private static bool IsEmoji(string value)
    {
        if (value.Length is 0 or > 16) return false;
        if (value.Any(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c < 0x2000)) return false;
        return new StringInfo(value).LengthInTextElements == 1;
    }
}
