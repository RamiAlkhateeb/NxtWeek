using Microsoft.JSInterop;

namespace MealPlanner.Shared.Services;

/// <summary>
/// Calls back when data under one database path changes, using Firebase's REST streaming
/// (<c>nxtweek.watch</c>), and when the tab comes back into view. Pages watch the narrowest path they show
/// (this week's days, the shared shopping list) and reload it; the returned handle stops the watch.
/// </summary>
public sealed class LiveWatch(IJSRuntime js, FirebaseRest db)
{
    /// <param name="path">For example "users/{key}/days" (the caller escapes keys).</param>
    /// <param name="startAt">Only children with keys from this one on (for days: a yyyy-MM-dd date).</param>
    public async Task<IAsyncDisposable> WatchAsync(string path, Func<Task> onChange, string? startAt = null)
    {
        var url = db.Url(path);
        if (startAt is not null)
            url += $"?orderBy={Uri.EscapeDataString("\"$key\"")}&startAt={Uri.EscapeDataString($"\"{startAt}\"")}";
        var target = new Target(onChange);
        var reference = DotNetObjectReference.Create(target);
        try
        {
            var id = await js.InvokeAsync<int>("nxtweek.watch", url, reference, nameof(Target.Changed));
            return new Handle(js, id, reference);
        }
        catch (JSException)
        {
            reference.Dispose();
            return new Handle(js, 0, null);
        }
    }

    public static string Key(string value) => FirebaseRest.Key(value);

    private sealed class Target(Func<Task> onChange)
    {
        [JSInvokable]
        public Task Changed() => onChange();
    }

    private sealed class Handle(IJSRuntime js, int id, IDisposable? reference) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (id > 0)
            {
                try { await js.InvokeVoidAsync("nxtweek.unwatch", id); }
                catch (JSDisconnectedException) { }
                catch (JSException) { }
            }
            reference?.Dispose();
        }
    }
}
