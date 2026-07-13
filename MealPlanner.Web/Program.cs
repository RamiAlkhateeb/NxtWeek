using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MealPlanner.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddBlazoredLocalStorage();

builder.Services.AddSingleton(new FirebaseOptions
{
    DatabaseUrl = "https://YOUR-PROJECT-ID-default-rtdb.firebaseio.com"
});
builder.Services.AddScoped<HttpClient>(_ => new HttpClient()); // separate client for Firebase calls, no BaseAddress
builder.Services.AddScoped<IMealService, FirebaseMealService>();
builder.Services.AddScoped<IMealCacheService, LocalStorageMealCacheService>();
builder.Services.AddScoped<ISuggestionService, RandomSuggestionService>();


await builder.Build().RunAsync();
