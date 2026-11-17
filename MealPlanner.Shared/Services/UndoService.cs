namespace MealPlanner.Shared.Services;

/// <summary>What the undo bar shows: a message and the action that puts things back.</summary>
public sealed record UndoOffer(string Text, Func<Task> Undo, int Id);

/// <summary>
/// "Removed · Undo": a short-lived offer to reverse the last change, shown by <c>UndoBar</c> in the layout.
/// A newer offer replaces the visible one; an offer goes after <see cref="Seconds"/> seconds.
/// </summary>
public sealed class UndoService
{
    public const int Seconds = 6;
    private int nextId;

    public UndoOffer? Current { get; private set; }
    public event Action? Changed;

    public void Offer(string text, Func<Task> undo)
    {
        var offer = new UndoOffer(text, undo, ++nextId);
        Current = offer;
        Changed?.Invoke();
        _ = ExpireAsync(offer.Id);
    }

    /// <summary>Runs the offered undo once. False when there was nothing to undo or it failed.</summary>
    public async Task<bool> UndoAsync()
    {
        if (Current is not { } offer) return false;
        Dismiss();
        try { await offer.Undo(); return true; }
        catch { return false; }
    }

    public void Dismiss()
    {
        Current = null;
        Changed?.Invoke();
    }

    private async Task ExpireAsync(int id)
    {
        await Task.Delay(TimeSpan.FromSeconds(Seconds));
        if (Current?.Id == id) Dismiss();
    }
}
