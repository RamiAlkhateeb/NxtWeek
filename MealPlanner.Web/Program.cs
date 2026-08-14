using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MealPlanner.Shared.Services;
using MealPlanner.Web;
using Microsoft.Extensions.DependencyInjection;
using MealPlanner.Web.Services;

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
builder.Services.AddScoped<IMealCacheService, LocalStorageMealCacheService>();
builder.Services.AddScoped<ISuggestionService, RandomSuggestionService>();
builder.Services.AddScoped<IAuthService, LocalAuthService>();
builder.Services.AddScoped<IDataExportService, LocalDataExportService>();
builder.Services.AddScoped<IDataImportService, LocalDataImportService>();
builder.Services.AddScoped<BrowserPreferenceService>();
builder.Services.AddScoped<IThemeService>(sp => sp.GetRequiredService<BrowserPreferenceService>());
builder.Services.AddScoped<ILanguageService>(sp => sp.GetRequiredService<BrowserPreferenceService>());


await builder.Build().RunAsync();
