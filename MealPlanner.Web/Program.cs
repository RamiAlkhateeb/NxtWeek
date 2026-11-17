using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MealPlanner.Shared.Services;
using MealPlanner.Web;
using Microsoft.Extensions.DependencyInjection;
using MealPlanner.Web.Services;
using Nxt.UI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Database requests go through the offline handler: reads are remembered, writes wait in an outbox without a connection.
builder.Services.AddScoped<MealPlanner.Shared.Offline.OfflineState>();
builder.Services.AddScoped(sp => new MealPlanner.Shared.Offline.OfflineHandler(
    sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(), sp.GetRequiredService<FirebaseOptions>(), sp.GetRequiredService<MealPlanner.Shared.Offline.OfflineState>())
{ InnerHandler = new HttpClientHandler() });
builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<MealPlanner.Shared.Offline.OfflineHandler>(), disposeHandler: false)
{ BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });


builder.Services.AddSingleton(new FirebaseOptions
{
    DatabaseUrl = "https://meal-planner-af799-default-rtdb.europe-west1.firebasedatabase.app/"
});

builder.Services.AddScoped<FirebaseUserService>();
builder.Services.AddScoped<IUserService>(sp => sp.GetRequiredService<FirebaseUserService>());
builder.Services.AddScoped<FirebaseMealCatalogService>();
builder.Services.AddScoped<IMealCatalogService>(sp => sp.GetRequiredService<FirebaseMealCatalogService>());
builder.Services.AddScoped<FirebaseMealService>();
builder.Services.AddScoped<IMealService>(sp => sp.GetRequiredService<FirebaseMealService>());
builder.Services.AddScoped<IShoppingListService, FirebaseShoppingListService>();
builder.Services.AddScoped<IMealCacheService, LocalStorageMealCacheService>();
builder.Services.AddScoped<ISuggestionService, RandomSuggestionService>();
builder.Services.AddScoped<IWeekFillService, WeekFillService>();
builder.Services.AddScoped<IMealAssignService, MealAssignService>();
builder.Services.AddScoped<INutritionStore, FirebaseNutritionStore>();
builder.Services.AddScoped<IDayPlanService>(sp => new DayPlanService(sp.GetRequiredService<INutritionStore>()));
builder.Services.AddScoped<ISyncService, FirebaseSyncService>();
builder.Services.AddScoped<LiveWatch>();
builder.Services.AddScoped<IMealDetailsService, GeminiMealDetailsService>();
builder.Services.AddScoped<INutritionEstimator, GeminiNutritionEstimator>();
builder.Services.AddScoped<IAuthService, LocalAuthService>();
builder.Services.AddScoped<IDataExportService, LocalDataExportService>();
builder.Services.AddScoped<IDataImportService, LocalDataImportService>();
builder.Services.AddScoped<ILanguageService, BrowserPreferenceService>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
builder.Services.AddScoped<INxtLocale, MakdousLocale>();
builder.Services.AddNxtUi(o =>
{
    o.App = NxtAppId.Makdous;
    o.LogoUrl = "logo.png";
    o.Description = new()
    {
        ["ar"] = "خطّط وجبات أسبوعك وقائمة مشترياتك مع العائلة والأصدقاء.",
        ["en"] = "Plan your week's meals and shopping with family and friends.",
        ["de"] = "Plane die Mahlzeiten und Einkäufe deiner Woche mit Familie und Freunden."
    };
    o.LegacyStorageKeys = new()
    {
        [Nxt.UI.Ai.AiSettingsService.ApiKeyKey] = "nxtweek.geminiApiKey",
        [Nxt.UI.Ai.AiSettingsService.ModelKey] = "nxtweek.geminiModel",
        [ThemeService.StorageKey] = "nxtweek.theme"
    };
});
builder.Services.AddScoped<IMealImageService, MealImageService>();
builder.Services.AddSingleton(new MealPhotoOptions());
builder.Services.AddScoped<IMealPhotoService, MealPhotoService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddSingleton<IAisleService, AisleService>();

await builder.Build().RunAsync();
