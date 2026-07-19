using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;
using Microsoft.JSInterop;

namespace MealPlanner.Shared.Services;

public class FirebaseMealService : IMealService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly IJSRuntime _js;
    private readonly IUserService _userService;
    private readonly IMealCatalogService _catalogService;

    public FirebaseMealService(
        HttpClient http, 
        FirebaseOptions options, 
        IJSRuntime js, 
        IUserService userService, 
        IMealCatalogService catalogService)
    {
        _http = http;
        _baseUrl = options.DatabaseUrl.TrimEnd('/');
        _js = js;
        _userService = userService;
        _catalogService = catalogService;
    }

    public async Task<List<Meal>> GetWeekAsync(DateOnly start, DateOnly end)
    {
        var username = await _js.InvokeAsync<string?>("localStorage.getItem", "username");
        if (string.IsNullOrWhiteSpace(username)) return new List<Meal>();

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
        var username = await _js.InvokeAsync<string?>("localStorage.getItem", "username");
        if (string.IsNullOrWhiteSpace(username)) return;

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

    public async Task<bool> IsSeededAsync()
    {
        return await _catalogService.IsCatalogSeededAsync();
    }

    public async Task SeedAsync(List<Meal> meals)
    {
        var catalogMeals = new List<MealCatalogItem>
        {
            new() { Id = "m1", Name = "رز وفاصولية حب مع لحمة", Cuisine = Cuisine.Syrian, MealType = MealType.Meat, Ingredients = new() { "فاصولياء خضراء", "لحم غنم", "أرز", "ثوم", "كزبرة", "سمنة" }, SideDishes = new() { "مخلل", "بصل أخضر" } },
            new() { Id = "m2", Name = "مفركة بطاطا", Cuisine = Cuisine.Syrian, MealType = MealType.Vegetarian, Ingredients = new() { "بطاطا", "بصل", "بيض", "زيت زيتون", "ملح", "فلفل أسود" }, SideDishes = new() { "خبز", "مخلل", "سلطة" } },
            new() { Id = "m3", Name = "فتة حمص وفول", Cuisine = Cuisine.Syrian, MealType = MealType.Vegetarian, Ingredients = new() { "حمص مسلوق", "فول مسلوق", "خبز محمص", "لبن", "طحينة", "ثوم", "ليمون", "كمون" }, SideDishes = new() { "مخلل", "نعناع" } },
            new() { Id = "m4", Name = "شوربة عدس مع خبز", Cuisine = Cuisine.Syrian, MealType = MealType.Vegan, Ingredients = new() { "عدس أحمر", "بصل", "كمون", "زيت", "خبز مقلي", "ليمون" }, SideDishes = new() { "فجل", "بصل أخضر" } },
            new() { Id = "m5", Name = "دجاج مشوي مع رز", Cuisine = Cuisine.Syrian, MealType = MealType.Chicken, Ingredients = new() { "دجاج كامل", "أرز", "بهارات مشكلة", "ثوم", "ليمون", "سمنة" }, SideDishes = new() { "لبن", "سلطة" } },
            new() { Id = "m6", Name = "ملوخية مع أرز", Cuisine = Cuisine.Syrian, MealType = MealType.Chicken, Ingredients = new() { "ملوخية ورق", "صدور دجاج", "كزبرة", "ثوم", "أرز شعيرية", "ليمون" }, SideDishes = new() { "خبز محمص", "فلفل حار" } },
            new() { Id = "m7", Name = "معكرونة بالصلصة الحمراء", Cuisine = Cuisine.Lebanese, MealType = MealType.Vegetarian, Ingredients = new() { "معكرونة", "صلصة طماطم", "ثوم", "ريحان", "زيت زيتون" }, SideDishes = new() { "سلطة", "خبز بالثوم" } },
            new() { Id = "m8", Name = "كباب تركي", Cuisine = Cuisine.Turkish, MealType = MealType.Meat, Ingredients = new() { "لحم مفروم", "بقدونس", "بصل", "بهارات الكباب", "طماطم" }, SideDishes = new() { "خبز", "مخلل", "بطاطا مقلية" } },
            new() { Id = "m9", Name = "شيش طاووق", Cuisine = Cuisine.Lebanese, MealType = MealType.Chicken, Ingredients = new() { "صدور دجاج", "زبادي", "ثوم", "ليمون", "بهارات" }, SideDishes = new() { "خبز", "بطاطا مقلية", "سلطة" } },
            new() { Id = "m10", Name = "مجدرة بالبرغل", Cuisine = Cuisine.Syrian, MealType = MealType.Vegan, Ingredients = new() { "عدس بني", "برغل خشن", "بصل", "زيت زيتون" }, SideDishes = new() { "سلطة", "لبن", "مخلل" } },
            new() { Id = "m11", Name = "صيادية سمك", Cuisine = Cuisine.Lebanese, MealType = MealType.Fish, Ingredients = new() { "سمك فيليه", "أرز", "بصل محروق", "بهارات الصيادية", "ليمون", "مكسرات" }, SideDishes = new() { "سلطة" } },
            new() { Id = "m12", Name = "يبرق (ورق عنب باللحمة)", Cuisine = Cuisine.Syrian, MealType = MealType.Meat, Ingredients = new() { "ورق عنب", "لحم مفروم", "أرز", "لية غنم", "ثوم", "ليمون", "بهارات" }, SideDishes = new() { "خبز", "لبن", "سلطة" } }
        };

        await _catalogService.SeedCatalogAsync(catalogMeals);
    }
}
