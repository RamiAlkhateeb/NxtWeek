using Nxt.UI;

namespace MealPlanner.Shared.Services;

/// <summary>Exposes the app's own <see cref="ILocalizationService"/> to the shared Nxt.UI components.</summary>
public sealed class MakdousLocale : INxtLocale, IDisposable
{
    private readonly ILocalizationService _loc;

    public MakdousLocale(ILocalizationService loc)
    {
        _loc = loc;
        _loc.OnLanguageChanged += RaiseChanged;
    }

    public string Language => _loc.CurrentLanguage;
    public bool IsRtl => _loc.IsRtl;

    public IReadOnlyList<NxtLanguage> Languages =>
        _loc.AvailableLanguages.Select(l => new NxtLanguage(l.Code, l.NativeName, l.Flag, l.Direction == "rtl")).ToList();

    public Task SetLanguageAsync(string code) => _loc.SetLanguageAsync(code);

    public event Action? Changed;

    private void RaiseChanged() => Changed?.Invoke();

    public void Dispose() => _loc.OnLanguageChanged -= RaiseChanged;
}
