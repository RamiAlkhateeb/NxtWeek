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
    DatabaseUrl = "https://meal-planner-af799-default-rtdb.europe-west1.firebasedatabase.app/",
    // ApiKey is the Firebase Web API key — a public client identifier, not a secret.
    // Find it in Firebase Console > Project settings > General > Your apps > Web app > apiKey.
    ApiKey = "AIzaSyC2eq_N2mGv0lGI2cjGvq2v5aeALL7rq2Y",
    AuthDomain = "meal-planner-af799.firebaseapp.com",
    ProjectId = "meal-planner-af799"
});

builder.Services.AddScoped<IUserService, FirebaseUserService>();
builder.Services.AddScoped<IMealCatalogService, FirebaseMealCatalogService>();
builder.Services.AddScoped<FirebaseMealService>();
builder.Services.AddScoped<IMealService>(sp => sp.GetRequiredService<FirebaseMealService>());
builder.Services.AddScoped<IMealCacheService, LocalStorageMealCacheService>();
builder.Services.AddScoped<ISuggestionService, RandomSuggestionService>();
builder.Services.AddScoped<IAuthService, FirebaseAuthService>();


await builder.Build().RunAsync();
