using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>A language the user can pick in Settings.</summary>
public sealed record LanguageOption(string Code, string NativeName, string Flag, string Direction);

public interface ILocalizationService
{
    string CurrentLanguage { get; }

    /// <summary>Every language the app ships. Drives the Settings switcher.</summary>
    IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    /// <summary>Arabic is the only right-to-left language today, but callers should
    /// prefer <see cref="IsRtl"/> or <see cref="Direction"/> over this flag. It exists
    /// for the few places that genuinely need Arabic-specific behaviour.</summary>
    bool IsArabic { get; }
    bool IsRtl { get; }
    string Direction { get; }
    CultureInfo Culture { get; }

    string this[string key] { get; }
    string T(string key);
    string T(string key, params object[] args);

    Task InitializeAsync();
    Task SetLanguageAsync(string language);
    event Action? OnLanguageChanged;

    string GetDayName(DayOfWeek day);
    string GetDayName(DateOnly date);
    string GetShortDayName(DayOfWeek day);
    string GetMonthName(int month);
    string GetMealTypeName(MealType type);

    /// <summary>"Monday, Feb 3" / "الاثنين 3 شباط" / "Montag, 3. Feb."</summary>
    string FormatDayDate(DateOnly date);
    string FormatMonthYear(DateOnly date);
}
