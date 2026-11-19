using MealPlanner.Shared.Nutrition;
using Nxt.UI.Ai;

namespace MealPlanner.Shared.Services;

/// <summary>Finds a store's look (logo domain and emoji) from its name.</summary>
public interface IStoreLogoService
{
    /// <summary>Null when the answer failed; <see cref="StoreLook.None"/> when the AI does not know the store.</summary>
    Task<StoreLook?> FindAsync(string storeName);
}

/// <summary>Asks Gemini, with the user's own key, which chain a store name is and what its website is; without a key
/// (or when Gemini fails) the photo worker's free AI (POST /store). Both answers are cleaned by <see cref="StoreLogo.Clean"/>.</summary>
public sealed class StoreLogoService(GeminiClient gemini, IWorkerAi worker) : IStoreLogoService
{
    private static readonly object Schema = new
    {
        type = "OBJECT",
        properties = new
        {
            domain = new { type = "STRING", description = "The store's main website domain, e.g. lidl.de, or an empty string." },
            emoji = new { type = "STRING", description = "One emoji for this kind of shop." }
        },
        required = new[] { "domain", "emoji" }
    };

    public async Task<StoreLook?> FindAsync(string storeName)
    {
        try
        {
            var prompt = $"""
                A family wrote "{storeName.Trim()}" as the name of a shop on their shopping list (any language).
                If it is a known supermarket, grocery, bakery, butcher, pharmacy or other chain, give its main website domain
                (just the domain, like "lidl.de", "carrefour.com", "aldi-sued.de"). If it is not a known chain, give "".
                Also give one emoji that fits the kind of shop (🛒 supermarket, 🥖 bakery, 🥩 butcher, 🥬 greengrocer,
                💊 pharmacy, 🐟 fishmonger, 🧀 cheese shop, 🏪 corner shop).
                """;
            return StoreLogo.Clean(await AiFallback.RunAsync(gemini.HasKeyAsync,
                () => gemini.GenerateJsonAsync<StoreLogoReply>(prompt, Schema),
                () => worker.FindStoreAsync(storeName)));
        }
        catch (Exception)
        {
            return null; // a logo is a nicety: the store works without one
        }
    }
}
