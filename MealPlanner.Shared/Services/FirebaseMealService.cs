using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace MealPlanner.Shared.Services;

public class FirebaseMealService : IMealService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly IJSRuntime _js;
    private readonly IUserService _userService;
    private readonly IMealCatalogService _catalogService;
    private readonly IAuthService _auth;

    public FirebaseMealService(
        HttpClient http, 
        FirebaseOptions options, 
        IJSRuntime js, 
        IUserService userService, 
        IMealCatalogService catalogService,
        IAuthService auth)
    {
        _http = http;
        _baseUrl = options.DatabaseUrl.TrimEnd('/');
        _js = js;
        _userService = userService;
        _catalogService = catalogService;
        _auth = auth;
    }

    public async Task<List<Meal>> GetWeekAsync(DateOnly start, DateOnly end)
    {
        var user = await _auth.GetCurrentUserAsync();
        if (user is null || string.IsNullOrWhiteSpace(user.Uid)) return new List<Meal>();
        var username = user.Uid;

        var planEntries = await _userService.GetWeeklyPlanAsync(username, start, end);
        var planDict = planEntries.ToDictionary(e => e.Date, e => e);

        var catalog = await _catalogService.GetAllMealsAsync();
        var catalogDict = catalog.ToDictionary(m => m.Id, m => m);

        var meals = new List<Meal>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var meal = new Meal
            {
                Id = date.ToString("yyyy-MM-dd"),
                Date = date,
                DayName = date.DayOfWeek.ToString(),
                Name = ""
            };

            if (planDict.TryGetValue(date, out var entry))
            {
                meal.MealId = entry.MealId;
                meal.IsFavorite = entry.IsFavorite;
                if (catalogDict.TryGetValue(entry.MealId, out var catItem))
                {
                    meal.Name = catItem.Name;
                    meal.Ingredients = catItem.Ingredients;
                    meal.SideDishes = catItem.SideDishes;
                    meal.Cuisine = catItem.Cuisine;
                    meal.MealType = catItem.MealType;
                }
            }

            meals.Add(meal);
        }

        return meals.OrderBy(m => m.Date).ToList();
    }

    public async Task UpsertMealAsync(Meal meal)
    {
        var user = await _auth.GetCurrentUserAsync();
        if (user is null || string.IsNullOrWhiteSpace(user.Uid)) return;
        var username = user.Uid;

        var catalog = await _catalogService.GetAllMealsAsync();
        var existing = catalog.FirstOrDefault(c => c.Name.Equals(meal.Name, StringComparison.OrdinalIgnoreCase));
        
        string mealId;
        if (existing is not null)
        {
            mealId = existing.Id;
            existing.Cuisine = meal.Cuisine ?? existing.Cuisine;
            existing.MealType = meal.MealType ?? existing.MealType;
            existing.Ingredients = meal.Ingredients.Count > 0 ? meal.Ingredients : existing.Ingredients;
            existing.SideDishes = meal.SideDishes.Count > 0 ? meal.SideDishes : existing.SideDishes;
            await _catalogService.UpsertMealAsync(existing);
        }
        else
        {
            mealId = Guid.NewGuid().ToString("N");
            var newItem = new MealCatalogItem
            {
                Id = mealId,
                Name = meal.Name,
                Cuisine = meal.Cuisine ?? Cuisine.Syrian,
                MealType = meal.MealType ?? MealType.Vegetarian,
                Ingredients = meal.Ingredients,
                SideDishes = meal.SideDishes
            };
            await _catalogService.UpsertMealAsync(newItem);
        }

        var entry = new WeeklyPlanEntry
        {
            Date = meal.Date,
            MealId = mealId,
            IsFavorite = meal.IsFavorite
        };
        await _userService.SaveWeeklyPlanEntryAsync(username, entry);
    }

    public async Task SeedAsync(List<Meal> meals)
    {
        var catalogMeals = new List<MealCatalogItem>
{
    new() { Id = "m1", Name = "رز وفاصوليا خضراء باللحمة", Cuisine = Cuisine.Syrian, MealType = MealType.Meat, Ingredients = new() { "فاصوليا خضراء", "لحم غنم", "أرز", "ثوم", "كزبرة", "سمنة" }, SideDishes = new() { "مخلل", "بصل أخضر" } },

    new() { Id = "m2", Name = "ملوخية مع أرز", Cuisine = Cuisine.Syrian, MealType = MealType.Chicken, Ingredients = new() { "ملوخية", "دجاج", "ثوم", "كزبرة", "أرز", "ليمون" }, SideDishes = new() { "خبز", "فلفل حار" } },

    new() { Id = "m3", Name = "يخنة بطاطا باللحمة", Cuisine = Cuisine.Syrian, MealType = MealType.Meat, Ingredients = new() { "بطاطا", "لحم", "طماطم", "بصل", "ثوم" }, SideDishes = new() { "أرز", "سلطة" } },

    new() { Id = "m5", Name = "مجدرة", Cuisine = Cuisine.Syrian, MealType = MealType.Vegan, Ingredients = new() { "عدس", "برغل", "بصل", "زيت زيتون" }, SideDishes = new() { "لبن", "مخلل", "سلطة" } },

    new() { Id = "m6", Name = "مفركة بطاطا", Cuisine = Cuisine.Syrian, MealType = MealType.Vegetarian, Ingredients = new() { "بطاطا", "بيض", "بصل", "زيت زيتون" }, SideDishes = new() { "سلطة", "خبز" } },

    new() { Id = "m7", Name = "شوربة عدس", Cuisine = Cuisine.Syrian, MealType = MealType.Vegan, Ingredients = new() { "عدس أحمر", "بصل", "كمون", "ليمون" }, SideDishes = new() { "خبز محمص" } },

    new() { Id = "m8", Name = "فتة حمص", Cuisine = Cuisine.Syrian, MealType = MealType.Vegetarian, Ingredients = new() { "حمص", "لبن", "طحينة", "خبز", "ثوم" }, SideDishes = new() { "مخلل" } },

    new() { Id = "m9", Name = "دجاج مشوي مع أرز", Cuisine = Cuisine.Syrian, MealType = MealType.Chicken, Ingredients = new() { "دجاج", "أرز", "ثوم", "ليمون" }, SideDishes = new() { "سلطة", "لبن" } },

    new() { Id = "m10", Name = "شيش طاووق", Cuisine = Cuisine.Syrian, MealType = MealType.Chicken, Ingredients = new() { "صدور دجاج", "لبن", "ثوم", "ليمون" }, SideDishes = new() { "بطاطا", "خبز", "ثومية" } },

    new() { Id = "m11", Name = "كبة مقلية", Cuisine = Cuisine.Syrian, MealType = MealType.Meat, Ingredients = new() { "برغل", "لحم مفروم", "بصل", "جوز" }, SideDishes = new() { "لبن", "سلطة" } },

    new() { Id = "m12", Name = "يبرق", Cuisine = Cuisine.Syrian, MealType = MealType.Meat, Ingredients = new() { "ورق عنب", "لحم مفروم", "أرز", "ثوم", "ليمون" }, SideDishes = new() { "لبن" } },

    new() { Id = "m13", Name = "محشي كوسا", Cuisine = Cuisine.Syrian, MealType = MealType.Meat, Ingredients = new() { "كوسا", "لحم مفروم", "أرز", "طماطم" }, SideDishes = new() { "لبن" } },

    new() { Id = "m15", Name = "رز وبازلاء مع الجزر", Cuisine = Cuisine.Syrian, MealType = MealType.Vegetarian, Ingredients = new() { "أرز", "بازلاء", "جزر", "بصل" }, SideDishes = new() { "لبن", "سلطة" } },

    new() { Id = "m16", Name = "معكرونة بالصلصة الحمراء", Cuisine = Cuisine.Syrian, MealType = MealType.Vegetarian, Ingredients = new() { "معكرونة", "صلصة طماطم", "ثوم", "ريحان" }, SideDishes = new() { "سلطة" } },

    new() { Id = "m18", Name = "شاورما دجاج", Cuisine = Cuisine.Syrian, MealType = MealType.Chicken, Ingredients = new() { "دجاج", "ثوم", "بهارات شاورما", "ليمون" }, SideDishes = new() { "بطاطا", "ثومية", "مخلل" } },

    new() { Id = "m19", Name = "ورق عنب", Cuisine = Cuisine.Syrian, MealType = MealType.Vegan, Ingredients = new() { "ورق عنب", "أرز", "بقدونس", "طماطم", "زيت زيتون" }, SideDishes = new() { "سلطة" } },

};

        await _catalogService.SeedCatalogAsync(catalogMeals);
    }

    public async Task<bool> IsSeededAsync()
    {
        return await _catalogService.IsCatalogSeededAsync();
    }

}
