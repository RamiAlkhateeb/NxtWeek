using System;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    bool IsArabic { get; }
    bool IsEnglish { get; }
    string Direction { get; }
    string this[string key] { get; }
    string T(string key);
    Task InitializeAsync();
    Task SetLanguageAsync(string language);
    event Action? OnLanguageChanged;
    string GetDayName(DayOfWeek day);
    string GetDayName(DateOnly date);
    string GetMealTypeName(MealType type);
}
