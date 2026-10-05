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

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });


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
builder.Services.AddScoped<IGeminiPlannerService, GeminiPlannerService>();
builder.Services.AddScoped<IAuthService, LocalAuthService>();
builder.Services.AddScoped<IDataExportService, LocalDataExportService>();
builder.Services.AddScoped<IDataImportService, LocalDataImportService>();
builder.Services.AddScoped<ILanguageService, BrowserPreferenceService>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
builder.Services.AddScoped<INxtLocale, MakdousLocale>();
builder.Services.AddNxtUi(o =>
{
    o.App = NxtAppId.Makdous;
    o.LogoUrl = "logo.svg";
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
builder.Services.AddSingleton<IMealImageService, MealImageService>();

await builder.Build().RunAsync();
