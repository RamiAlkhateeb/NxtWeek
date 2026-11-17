using MealPlanner.Shared.Nutrition;
using MealPlanner.Shared.Components.Nutrition;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MealPlanner.Shared.Models;
using Microsoft.JSInterop;

namespace MealPlanner.Shared.Services;

/// <summary>
/// AI-generated meal photos, shared by everyone: one small JPEG data URL per meal at MealPhotos/{mealId}, kept
/// apart from the catalog so the catalog download stays small. Which meals have a photo is read once with a
/// shallow request; each photo is fetched only when a meal is shown, and kept in memory for the session.
/// New photos are drawn by the Makdous photo worker (Cloudflare Workers AI, free, no key; see photo-worker/).
/// A user's own private photo (<see cref="IMealImageService"/>) always wins over the shared one.
/// </summary>
public interface IMealPhotoService
{
    /// <summary>The shared photo of a meal, or null when there is none (or it cannot be read).</summary>
    Task<string?> GetAsync(string mealId);
    /// <summary>Whether a shared photo exists for the meal (from the one-request index; nothing is downloaded).</summary>
    Task<bool> HasAsync(string mealId);
    /// <summary>Draws a photo with the photo worker, shrinks it to 480 px and shares it. Throws <see cref="MealPhotoException"/>.</summary>
    /// <param name="replace">Redraw a meal that already has a photo ("New photo").</param>
    Task<string> GenerateAsync(MealCatalogItem meal, bool replace = false);
    /// <summary>How many photos this account has had drawn this week (Monday to Sunday, UTC), of <see cref="MealPhotoService.WeeklyLimit"/>.</summary>
    Task<PhotoQuota> GetQuotaAsync();
    /// <summary>The owner's code that lifts the weekly limit, kept on this device only (localStorage).</summary>
    Task<string?> GetOwnerCodeAsync();
    Task SetOwnerCodeAsync(string? code);
    /// <summary>Raised with the meal id when a photo was generated, so every picture of that meal updates.</summary>
    event Action<string>? Changed;
}

/// <summary>This week's photos: <see cref="Used"/> of <see cref="Limit"/>, counting again from <see cref="ResetsOn"/> (a Monday, UTC).</summary>
public sealed record PhotoQuota(int Used, int Limit, DateOnly ResetsOn)
{
    public int Left => Math.Max(0, Limit - Used);

    /// <summary>The Monday (UTC) of the week that contains <paramref name="utcNow"/>; the worker counts the same weeks.</summary>
    public static DateOnly MondayOf(DateTime utcNow)
    {
        var day = DateOnly.FromDateTime(utcNow);
        return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }
}

public enum MealPhotoProblem
{
    /// <summary>This account has had this week's photos; more from Monday.</summary>
    WeeklyLimit,
    /// <summary>Today's free allowance is used up; try again tomorrow.</summary>
    DailyLimit,
    /// <summary>Too many photos asked for in a minute.</summary>
    Busy,
    /// <summary>The model could not draw this dish.</summary>
    Failed,
    /// <summary>The photo service could not be reached (offline, not deployed yet).</summary>
    Unavailable
}

public sealed class MealPhotoException(MealPhotoProblem problem, string? detail = null)
    : Exception(detail ?? problem.ToString())
{
    public MealPhotoProblem Problem { get; } = problem;

    /// <summary>The problem in the user's words.</summary>
    public static string Describe(ILocalizationService loc, Exception ex) =>
        ex is MealPhotoException { Problem: MealPhotoProblem.WeeklyLimit }
            ? loc.T("nut_photo_err_weekly", loc.Num(MealPhotoService.WeeklyLimit))
            : loc[MessageKey(ex)];

    /// <summary>The translation key that explains the problem to the user.</summary>
    public static string MessageKey(Exception ex) => ex is MealPhotoException p
        ? p.Problem switch
        {
            MealPhotoProblem.WeeklyLimit => "nut_photo_err_weekly",
            MealPhotoProblem.DailyLimit => "nut_photo_err_daily",
            MealPhotoProblem.Busy => "nut_photo_err_busy",
            MealPhotoProblem.Failed => "nut_photo_err_failed",
            _ => "nut_photo_err_unavailable"
        }
        : "nut_photo_err_unavailable";
}

public sealed class MealPhotoOptions
{
    public string WorkerUrl { get; set; } = "https://makdous-photos.rami13195.workers.dev";
}

public sealed class MealPhotoService(HttpClient http, FirebaseRest db, MealPhotoOptions photoOptions, IJSRuntime js, IAuthService auth) : IMealPhotoService
{
    /// <summary>Photos each account may have drawn per week. The worker enforces it (WEEKLY_LIMIT in photo-worker/wrangler.toml).</summary>
    public const int WeeklyLimit = 10;

    public const int Size = 480;

    private readonly Dictionary<string, string?> photos = new();
    private readonly Dictionary<string, Task<string?>> loading = new();
    private Task<HashSet<string>>? index;

    public event Action<string>? Changed;


    // A meal without its own photo shows its English twin's (the seeded n12 / de_n12 → en_n12).
    public async Task<string?> GetAsync(string mealId) =>
        await GetOwnAsync(mealId) ?? (MealTwins.EnglishTwinId(mealId) is { } twin ? await GetOwnAsync(twin) : null);

    private async Task<string?> GetOwnAsync(string mealId)
    {
        if (string.IsNullOrWhiteSpace(mealId)) return null;
        if (photos.TryGetValue(mealId, out var known)) return known;
        if (!(await IndexAsync()).Contains(mealId)) return null;
        if (!loading.TryGetValue(mealId, out var task)) loading[mealId] = task = FetchAsync(mealId);
        return await task;
    }

    public async Task<bool> HasAsync(string mealId) =>
        await HasOwnAsync(mealId) || (MealTwins.EnglishTwinId(mealId) is { } twin && await HasOwnAsync(twin));

    private async Task<bool> HasOwnAsync(string mealId) =>
        photos.TryGetValue(mealId, out var p) && p is not null || (await IndexAsync()).Contains(mealId);

    private Task<HashSet<string>> IndexAsync() => index ??= LoadIndexAsync();

    private async Task<HashSet<string>> LoadIndexAsync()
    {
        try
        {
            var shallow = await db.GetAsync<JsonObject>("MealPhotos", "shallow=true");
            return shallow?.Select(child => child.Key).ToHashSet() ?? [];
        }
        catch
        {
            index = null; // try again next time
            return [];
        }
    }

    private async Task<string?> FetchAsync(string mealId)
    {
        try
        {
            var url = await db.GetAsync<string?>($"MealPhotos/{FirebaseRest.Key(mealId)}");
            url = url is not null && url.StartsWith("data:image/", StringComparison.Ordinal) ? url : null;
            photos[mealId] = url;
            return url;
        }
        catch { return null; }
        finally { loading.Remove(mealId); }
    }

    public async Task<string> GenerateAsync(MealCatalogItem meal, bool replace = false)
    {
        string raw;
        string? twinId;
        try
        {
            var user = (await auth.GetCurrentUserAsync())?.Uid;
            if (string.IsNullOrWhiteSpace(user)) throw new MealPhotoException(MealPhotoProblem.Unavailable, "Not signed in.");
            using var response = await http.PostAsJsonAsync($"{photoOptions.WorkerUrl.TrimEnd('/')}/photo", new { mealId = meal.Id, user, replace, ownerCode = await GetOwnerCodeAsync() ?? "" });
            var body = await ReadAsync(response);
            var image = Text(body, "image");
            twinId = Text(body, "twin");
            var status = (int)response.StatusCode;
            if (status == 200 && string.IsNullOrEmpty(image)) status = 502;
            switch (status)
            {
                case 200:
                    raw = image!;
                    break;
                case 409:
                    // Someone else drew it a moment ago: show theirs.
                    photos.Remove(meal.Id);
                    index = null;
                    return await GetAsync(meal.Id) ?? throw new MealPhotoException(MealPhotoProblem.Failed);
                case 429:
                    throw new MealPhotoException(Text(body, "error") switch
                    {
                        "weekly_limit" => MealPhotoProblem.WeeklyLimit,
                        "daily_limit" => MealPhotoProblem.DailyLimit,
                        _ => MealPhotoProblem.Busy
                    });
                case 502:
                    throw new MealPhotoException(MealPhotoProblem.Failed, Text(body, "detail"));
                default:
                    throw new MealPhotoException(MealPhotoProblem.Unavailable, $"{status} {Text(body, "error")}");
            }
        }
        catch (HttpRequestException ex) { throw new MealPhotoException(MealPhotoProblem.Unavailable, ex.Message); }
        catch (TaskCanceledException ex) { throw new MealPhotoException(MealPhotoProblem.Unavailable, ex.Message); }

        var small = await js.InvokeAsync<string?>("nxtweek.squareJpeg", raw, Size)
            ?? throw new MealPhotoException(MealPhotoProblem.Failed, "The picture could not be read.");
        await StoreAsync(meal.Id, small);
        // Drawn from the English twin that had none: it is the same dish, so it gets the photo too.
        if (twinId is { Length: > 0 } t && !await HasOwnAsync(t))
        {
            try { await StoreAsync(t, small); } catch { /* the meal itself has its photo */ }
        }
        return small;
    }

    private async Task StoreAsync(string mealId, string dataUrl)
    {
        await db.PutAsync($"MealPhotos/{FirebaseRest.Key(mealId)}", dataUrl);
        photos[mealId] = dataUrl;
        (await IndexAsync()).Add(mealId);
        Changed?.Invoke(mealId);
    }

    private const string OwnerCodeKey = "nxtweek.photoOwnerCode";

    public async Task<string?> GetOwnerCodeAsync()
    {
        try { return await js.InvokeAsync<string?>("localStorage.getItem", OwnerCodeKey) is { Length: > 0 } code ? code : null; }
        catch { return null; }
    }

    public async Task SetOwnerCodeAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) await js.InvokeVoidAsync("localStorage.removeItem", OwnerCodeKey);
        else await js.InvokeVoidAsync("localStorage.setItem", OwnerCodeKey, code.Trim());
    }

    public async Task<PhotoQuota> GetQuotaAsync()
    {
        var monday = PhotoQuota.MondayOf(DateTime.UtcNow);
        var used = 0;
        var user = (await auth.GetCurrentUserAsync())?.Uid;
        if (!string.IsNullOrWhiteSpace(user))
        {
            try { used = await db.GetAsync<int?>($"PhotoQuota/{FirebaseRest.Key(user)}/{monday:yyyy-MM-dd}") ?? 0; }
            catch { /* shown as unused; the worker still counts */ }
        }
        return new PhotoQuota(used, WeeklyLimit, monday.AddDays(7));
    }

    private static string? Text(JsonObject? body, string name) =>
        body is not null && body.TryGetPropertyValue(name, out var node) && node is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    private static async Task<JsonObject?> ReadAsync(HttpResponseMessage response)
    {
        try { return await response.Content.ReadFromJsonAsync<JsonObject>(); }
        catch { return null; }
    }
}
