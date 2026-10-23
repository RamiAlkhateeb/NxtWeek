using System.Globalization;
using MealPlanner.Shared.Services;

namespace MealPlanner.Shared.Components.Nutrition;

/// <summary>Number formatting for nutrition values.</summary>
public static class NutFormat
{
    private static readonly CultureInfo Latin = CultureInfo.GetCultureInfo("en-US");

    // Arabic screens keep Western digits and separators, as everywhere else in the app.
    private static CultureInfo NumberCulture(ILocalizationService loc) => loc.IsArabic ? Latin : loc.Culture;

    /// <summary>Whole number with the language's grouping: "1,950" / "1.950".</summary>
    public static string Num(this ILocalizationService loc, double value) =>
        Math.Round(value, MidpointRounding.AwayFromZero).ToString("N0", NumberCulture(loc));

    /// <summary>One decimal: "69.4" / "69,4".</summary>
    public static string Dec1(this ILocalizationService loc, double value) => value.ToString("N1", NumberCulture(loc));

    /// <summary>Servings: "1", "1.5" / "1,5".</summary>
    public static string Servings(this ILocalizationService loc, double value) =>
        value % 1 == 0 ? value.ToString("0", NumberCulture(loc)) : value.ToString("0.#", NumberCulture(loc));

    /// <summary>A value for an input field: no grouping, at most one decimal ("1950", "12,5").</summary>
    public static string Input(this ILocalizationService loc, double value) => value.ToString("0.#", NumberCulture(loc));

    /// <summary>Parses what a user typed, accepting either decimal separator. Empty or invalid is null.</summary>
    public static double? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = text.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : null;
    }
}
