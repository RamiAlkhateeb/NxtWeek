using System.Text.RegularExpressions;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

/// <summary>
/// Recipe steps to and from the text a person types: one step per line, numbering and bullets dropped, and a time
/// in the line ("10 min", "١٠ دقائق", "10 Minuten") becomes the step's timer. Pure and tested.
/// </summary>
public static partial class RecipeSteps
{
    public const int MaxSteps = 20;
    public const int MaxMinutes = 600;

    [GeneratedRegex(@"^\s*(?:\d+\s*[.)\-:]|[-•*·])\s*")]
    private static partial Regex Numbering();

    [GeneratedRegex(@"(\d{1,3})\s*(?:min(?:ute)?s?\b|Min(?:\.|uten?\b)|دقيقة|دقائق|د\b)", RegexOptions.IgnoreCase)]
    private static partial Regex Duration();

    public static List<RecipeStep> Parse(string? text)
    {
        var steps = new List<RecipeStep>();
        foreach (var raw in (text ?? "").Split('\n'))
        {
            var line = Numbering().Replace(WesternDigits(raw).Trim(), "").Trim();
            if (line.Length == 0) continue;
            var time = Duration().Match(line);
            int? minutes = time.Success && int.TryParse(time.Groups[1].Value, out var m) && m > 0 && m <= MaxMinutes ? m : null;
            steps.Add(new RecipeStep { Text = line, Minutes = minutes });
            if (steps.Count == MaxSteps) break;
        }
        return steps;
    }

    /// <summary>
    /// The steps as editable text. A step whose timer is not written in its own line gets it appended with
    /// <paramref name="minutesFormat"/> (for example "{0} min"), so saving the text again keeps every timer.
    /// </summary>
    public static string ToText(IEnumerable<RecipeStep> steps, string minutesFormat = "{0} min") =>
        string.Join("\n", steps.Select(s =>
        {
            var text = s.Text.Trim();
            if (s.Minutes is not { } minutes) return text;
            var written = Duration().Match(WesternDigits(text));
            return written.Success && written.Groups[1].Value == minutes.ToString() ? text : $"{text} ({string.Format(minutesFormat, minutes)})";
        }));

    /// <summary>Arabic-Indic and Persian digits to 0–9, so "١٠ دقائق" is read as 10 minutes.</summary>
    public static string WesternDigits(string text) =>
        string.Concat(text.Select(c => c is >= '\u0660' and <= '\u0669' ? (char)('0' + c - '\u0660')
                                     : c is >= '\u06F0' and <= '\u06F9' ? (char)('0' + c - '\u06F0') : c));
}
