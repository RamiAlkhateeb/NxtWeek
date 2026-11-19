using System.Text.Json;
using System.Text.Json.Nodes;

namespace MealPlanner.Shared.Nutrition;

/// <summary>
/// Which AI answers: Gemini with the user's own key, otherwise (or when Gemini fails) the free photo worker.
/// Pure and tested; the services pass in the two calls.
/// </summary>
public static class AiFallback
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>Gemini when there is a key, falling back to the worker on any failure; the worker alone without a key.
    /// When both fail, the worker's error is thrown (it is the one the user can do nothing about but wait).</summary>
    public static async Task<T> RunAsync<T>(Func<Task<bool>> hasKey, Func<Task<T>> gemini, Func<Task<T>> worker)
    {
        bool keyed;
        try { keyed = await hasKey(); } catch { keyed = false; }
        if (keyed)
        {
            try { return await gemini(); }
            catch (OperationCanceledException) { throw; }
            catch { /* quota, bad key or an odd reply: the worker is next */ }
        }
        return await worker();
    }

    /// <summary>The worker's /details reply ({ "details": {...} }) as the same shape Gemini gives, or null.</summary>
    public static SuggestedMealDetails? ParseDetails(JsonNode? body)
    {
        if (body?["details"] is not JsonObject details) return null;
        try { return details.Deserialize<SuggestedMealDetails>(Web); }
        catch (JsonException) { return null; }
    }

    /// <summary>The worker's /store reply ({ "domain", "emoji" }), or null.</summary>
    public static StoreLogoReply? ParseStore(JsonNode? body)
    {
        if (body is not JsonObject o) return null;
        try { return o.Deserialize<StoreLogoReply>(Web); }
        catch (JsonException) { return null; }
    }
}
