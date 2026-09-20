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
                    meal.PhotoUrl = catItem.PhotoUrl;
                    meal.Ingredients = catItem.Ingredients;
                    meal.SideDishes = catItem.SideDishes;
                    meal.MealType = catItem.MealType;
                    meal.Tags = catItem.Tags;
                    meal.Language = string.IsNullOrWhiteSpace(catItem.Language) ? "ar" : catItem.Language;
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
        var normalizedName = NormalizeName(meal.Name);
        var existing = catalog.FirstOrDefault(c => NormalizeName(c.Name) == normalizedName);
        
        string mealId;
        if (existing is not null)
        {
            mealId = existing.Id;
            existing.MealType = meal.MealType ?? existing.MealType;
            existing.Ingredients = meal.Ingredients.Count > 0 ? meal.Ingredients : existing.Ingredients;
            existing.SideDishes = meal.SideDishes.Count > 0 ? meal.SideDishes : existing.SideDishes;
            if (!string.IsNullOrWhiteSpace(meal.Language))
            {
                existing.Language = meal.Language;
            }
            await _catalogService.UpdateMealAsync(existing);
        }
        else
        {
            var newItem = new MealCatalogItem
            {
                Name = meal.Name,
                MealType = meal.MealType ?? MealType.Vegetarian,
                Ingredients = meal.Ingredients,
                SideDishes = meal.SideDishes,
                Language = string.IsNullOrWhiteSpace(meal.Language) ? "ar" : meal.Language
            };
            newItem = await _catalogService.CreateMealAsync(newItem);
            mealId = newItem.Id;
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
    new() { Id = "m1", Name = "رز وفاصوليا خضراء باللحمة", MealType = MealType.Meat, Ingredients = new() { "فاصوليا خضراء", "لحم غنم", "أرز", "ثوم", "كزبرة", "سمنة" }, SideDishes = new() { "مخلل", "بصل أخضر" } },

    new() { Id = "m2", Name = "ملوخية مع أرز", MealType = MealType.Chicken, Ingredients = new() { "ملوخية", "دجاج", "ثوم", "كزبرة", "أرز", "ليمون" }, SideDishes = new() { "خبز", "فلفل حار" } },

    new() { Id = "m3", Name = "يخنة بطاطا باللحمة", MealType = MealType.Meat, Ingredients = new() { "بطاطا", "لحم", "طماطم", "بصل", "ثوم" }, SideDishes = new() { "أرز", "سلطة" } },

    new() { Id = "m5", Name = "مجدرة", MealType = MealType.Vegan, Ingredients = new() { "عدس", "برغل", "بصل", "زيت زيتون" }, SideDishes = new() { "لبن", "مخلل", "سلطة" } },

    new() { Id = "m6", Name = "مفركة بطاطا", MealType = MealType.Vegetarian, Ingredients = new() { "بطاطا", "بيض", "بصل", "زيت زيتون" }, SideDishes = new() { "سلطة", "خبز" } },

    new() { Id = "m7", Name = "شوربة عدس", MealType = MealType.Vegan, Ingredients = new() { "عدس أحمر", "بصل", "كمون", "ليمون" }, SideDishes = new() { "خبز محمص" } },

    new() { Id = "m8", Name = "فتة حمص", MealType = MealType.Vegetarian, Ingredients = new() { "حمص", "لبن", "طحينة", "خبز", "ثوم" }, SideDishes = new() { "مخلل" } },

    new() { Id = "m9", Name = "دجاج مشوي مع أرز", MealType = MealType.Chicken, Ingredients = new() { "دجاج", "أرز", "ثوم", "ليمون" }, SideDishes = new() { "سلطة", "لبن" } },

    new() { Id = "m10", Name = "شيش طاووق", MealType = MealType.Chicken, Ingredients = new() { "صدور دجاج", "لبن", "ثوم", "ليمون" }, SideDishes = new() { "بطاطا", "خبز", "ثومية" } },

    new() { Id = "m11", Name = "كبة مقلية", MealType = MealType.Meat, Ingredients = new() { "برغل", "لحم مفروم", "بصل", "جوز" }, SideDishes = new() { "لبن", "سلطة" } },

    new() { Id = "m12", Name = "يبرق", MealType = MealType.Meat, Ingredients = new() { "ورق عنب", "لحم مفروم", "أرز", "ثوم", "ليمون" }, SideDishes = new() { "لبن" } },

    new() { Id = "m13", Name = "محشي كوسا", MealType = MealType.Meat, Ingredients = new() { "كوسا", "لحم مفروم", "أرز", "طماطم" }, SideDishes = new() { "لبن" } },

    new() { Id = "m15", Name = "رز وبازلاء مع الجزر", MealType = MealType.Vegetarian, Ingredients = new() { "أرز", "بازلاء", "جزر", "بصل" }, SideDishes = new() { "لبن", "سلطة" } },

    new() { Id = "m16", Name = "معكرونة بالصلصة الحمراء", MealType = MealType.Vegetarian, Ingredients = new() { "معكرونة", "صلصة طماطم", "ثوم", "ريحان" }, SideDishes = new() { "سلطة" } },

    new() { Id = "m18", Name = "شاورما دجاج", MealType = MealType.Chicken, Ingredients = new() { "دجاج", "ثوم", "بهارات شاورما", "ليمون" }, SideDishes = new() { "بطاطا", "ثومية", "مخلل" } },

    new() { Id = "m19", Name = "ورق عنب", MealType = MealType.Vegan, Ingredients = new() { "ورق عنب", "أرز", "بقدونس", "طماطم", "زيت زيتون" }, SideDishes = new() { "سلطة" } },

new() { Id = "m20", Name = "أرضي شوكي مع اللحمة والرز", MealType = MealType.Meat, Ingredients = new() { "أرضي شوكي", "لحم", "أرز", "بصل", "ثوم" }, SideDishes = new() { "لبن" } },

new() { Id = "m21", Name = "إندومي مع تونة", MealType = MealType.Fish, Ingredients = new() { "إندومي", "تونة", "بصل" }, SideDishes = new() { "ليمون" } },

new() { Id = "m22", Name = "برغر لحمة", MealType = MealType.Meat, Ingredients = new() { "خبز برغر", "لحم مفروم", "خس", "طماطم", "جبنة" }, SideDishes = new() { "بطاطا مقلية" } },

new() { Id = "m23", Name = "برغر دجاج", MealType = MealType.Chicken, Ingredients = new() { "خبز برغر", "صدر دجاج", "خس", "طماطم" }, SideDishes = new() { "بطاطا مقلية" } },

new() { Id = "m24", Name = "برغل مع حمص", MealType = MealType.Vegetarian, Ingredients = new() { "برغل", "حمص", "زيت زيتون", "ليمون" }, SideDishes = new() { "سلطة" } },

new() { Id = "m25", Name = "بطاطا بالبندورة", MealType = MealType.Vegan, Ingredients = new() { "بطاطا", "طماطم", "بصل", "ثوم" }, SideDishes = new() { "أرز" } },

new() { Id = "m26", Name = "بطاطا مسلوقة", MealType = MealType.Vegan, Ingredients = new() { "بطاطا", "ملح", "زيت زيتون" }, SideDishes = new() { "لبن" } },

new() { Id = "m27", Name = "بطاطا وبيض", MealType = MealType.Vegetarian, Ingredients = new() { "بطاطا", "بيض", "بصل" }, SideDishes = new() { "خبز" } },

new() { Id = "m28", Name = "بيتزا بيت", MealType = MealType.Vegetarian, Ingredients = new() { "عجينة بيتزا", "صلصة طماطم", "جبنة موزاريلا", "خضار" }, SideDishes = new() { "سلطة" } },

new() { Id = "m29", Name = "خضار وأرز مع لحمة ناعمة", MealType = MealType.Meat, Ingredients = new() { "خضار مشكلة", "أرز", "لحمة ناعمة", "بصل" }, SideDishes = new() { "لبن" } },

new() { Id = "m30", Name = "دونر", MealType = MealType.Meat, Ingredients = new() { "خبز دونر", "لحمة دونر", "خس", "طماطم", "ثومية" }, SideDishes = new() { "بطاطا مقلية" } },

new() { Id = "m31", Name = "رز وبازيلا مع اللحمة", MealType = MealType.Meat, Ingredients = new() { "أرز", "بازيلا", "لحم", "جزر" }, SideDishes = new() { "لبن" } },

new() { Id = "m32", Name = "رز وفاصولية حب مع لحمة", MealType = MealType.Meat, Ingredients = new() { "فاصولية بيضاء", "لحم", "أرز", "ثوم" }, SideDishes = new() { "مخلل" } },

new() { Id = "m33", Name = "فاصولية بالبندورة مع أرز", MealType = MealType.Vegan, Ingredients = new() { "فاصولية خضراء", "طماطم", "ثوم", "أرز" }, SideDishes = new() { "سلطة" } },

new() { Id = "m34", Name = "رز مع القريدس", MealType = MealType.Fish, Ingredients = new() { "قريدس", "أرز", "ثوم", "ذرة" }, SideDishes = new() { "سلطة" } },

new() { Id = "m35", Name = "فطر مقلي مع معكرونة", MealType = MealType.Vegetarian, Ingredients = new() { "فطر", "بقسماط", "معكرونة", "ثوم" }, SideDishes = new() { "سلطة" } },

new() { Id = "m36", Name = "سمك مقلي", MealType = MealType.Fish, Ingredients = new() { "سمك", "دقيق", "ليمون", "ثوم" }, SideDishes = new() { "أرز", "سلطة" } },

new() { Id = "m37", Name = "سمك ورز", MealType = MealType.Fish, Ingredients = new() { "سمك", "أرز", "ليمون", "ثوم" }, SideDishes = new() { "سلطة" } },

new() { Id = "m38", Name = "شرمبس مع معكرونة", MealType = MealType.Fish, Ingredients = new() { "شرمبس", "معكرونة", "ثوم", "زيت زيتون" }, SideDishes = new() { "خبز" } },

new() { Id = "m39", Name = "شوربة عدس مع بطاطا بالفرن", MealType = MealType.Vegan, Ingredients = new() { "عدس أحمر", "بطاطا", "بصل", "كمون" }, SideDishes = new() { "خبز" } },

new() { Id = "m40", Name = "شوربة بطاطا", MealType = MealType.Vegan, Ingredients = new() { "بطاطا", "بصل", "ثوم", "كريمة نباتية" }, SideDishes = new() { "خبز محمص" } },

new() { Id = "m41", Name = "تونة مع حمص", MealType = MealType.Fish, Ingredients = new() { "تونة", "حمص", "ليمون", "زيت زيتون" }, SideDishes = new() { "خبز" } },

new() { Id = "m42", Name = "فتة حمص وفول", MealType = MealType.Vegetarian, Ingredients = new() { "حمص", "فول", "لبن", "طحينة", "خبز", "ثوم" }, SideDishes = new() { "مخلل" } },

new() { Id = "m43", Name = "فخاد دجاج مع برغل", MealType = MealType.Chicken, Ingredients = new() { "فخاد دجاج", "برغل", "بصل", "بهارات" }, SideDishes = new() { "لبن" } },

new() { Id = "m44", Name = "فخاد دجاج مع أرز", MealType = MealType.Chicken, Ingredients = new() { "فخاد دجاج", "أرز", "بصل", "بهارات" }, SideDishes = new() { "سلطة" } },

new() { Id = "m45", Name = "فول وحمص", MealType = MealType.Vegan, Ingredients = new() { "فول", "حمص", "ليمون", "زيت زيتون", "ثوم" }, SideDishes = new() { "خبز" } },

new() { Id = "m46", Name = "قريدس مع معكرونة", MealType = MealType.Fish, Ingredients = new() { "قريدس", "معكرونة", "ثوم", "زيت زيتون" }, SideDishes = new() { "سلطة" } },

new() { Id = "m47", Name = "معكرونة بالجبنة والذرة والفطر", MealType = MealType.Vegetarian, Ingredients = new() { "معكرونة", "فطر", "ذرة", "جبنة" }, SideDishes = new() { "سلطة" } },

new() { Id = "m48", Name = "مناقيش", MealType = MealType.Vegetarian, Ingredients = new() { "عجينة", "زعتر", "زيت زيتون", "جبنة" }, SideDishes = new() { "شاي" } },

new() { Id = "m49", Name = "همبرغر", MealType = MealType.Meat, Ingredients = new() { "خبز برغر", "لحم مفروم", "خس", "طماطم", "مخلل" }, SideDishes = new() { "بطاطا مقلية" } },

    // English meals
    new() { Id = "en_m1", Name = "Grilled Chicken Breast with Rice", MealType = MealType.Chicken, Language = "en", Ingredients = new() { "Chicken breast", "Rice", "Garlic", "Lemon", "Olive oil" }, SideDishes = new() { "Green salad", "Yogurt" } },
    new() { Id = "en_m2", Name = "Spaghetti Bolognese", MealType = MealType.Meat, Language = "en", Ingredients = new() { "Spaghetti", "Minced beef", "Tomato sauce", "Onion", "Parmesan" }, SideDishes = new() { "Garlic bread", "Side salad" } },
    new() { Id = "en_m3", Name = "Beef Burger with Fries", MealType = MealType.Meat, Language = "en", Ingredients = new() { "Burger bun", "Beef patty", "Lettuce", "Tomato", "Cheddar" }, SideDishes = new() { "French fries", "Coleslaw" } },
    new() { Id = "en_m4", Name = "Red Lentil Soup", MealType = MealType.Vegan, Language = "en", Ingredients = new() { "Red lentils", "Onion", "Cumin", "Lemon", "Carrot" }, SideDishes = new() { "Toasted pita bread" } },
    new() { Id = "en_m5", Name = "Grilled Salmon with Vegetables", MealType = MealType.Fish, Language = "en", Ingredients = new() { "Salmon fillet", "Lemon", "Dill", "Asparagus", "Olive oil" }, SideDishes = new() { "Mashed potatoes", "Salad" } },
    new() { Id = "en_m6", Name = "Chicken Fajitas", MealType = MealType.Chicken, Language = "en", Ingredients = new() { "Chicken strips", "Bell peppers", "Onion", "Tortilla wraps", "Fajita spices" }, SideDishes = new() { "Guacamole", "Sour cream" } },
    new() { Id = "en_m7", Name = "Margherita Pizza", MealType = MealType.Vegetarian, Language = "en", Ingredients = new() { "Pizza dough", "Tomato sauce", "Mozzarella", "Fresh basil" }, SideDishes = new() { "Caesar salad" } },
    new() { Id = "en_m8", Name = "Crispy Fish and Chips", MealType = MealType.Fish, Language = "en", Ingredients = new() { "White fish fillet", "Batter", "Potatoes", "Lemon" }, SideDishes = new() { "Tartar sauce", "Peas" } },
    new() { Id = "en_m9", Name = "Vegetable Stir Fry with Noodles", MealType = MealType.Vegan, Language = "en", Ingredients = new() { "Noodles", "Broccoli", "Carrots", "Soy sauce", "Sesame oil" }, SideDishes = new() { "Spring rolls" } },
    new() { Id = "en_m10", Name = "Beef Steak with Mashed Potatoes", MealType = MealType.Meat, Language = "en", Ingredients = new() { "Beef steak", "Potatoes", "Butter", "Black pepper", "Garlic" }, SideDishes = new() { "Steamed vegetables" } },
    new() { Id = "en_m11", Name = "Chicken Shawarma Wrap", MealType = MealType.Chicken, Language = "en", Ingredients = new() { "Chicken", "Pita bread", "Garlic sauce", "Pickles", "Spices" }, SideDishes = new() { "French fries" } },
    new() { Id = "en_m12", Name = "Creamy Mushroom Pasta", MealType = MealType.Vegetarian, Language = "en", Ingredients = new() { "Penne pasta", "Mushrooms", "Cream", "Garlic", "Parmesan" }, SideDishes = new() { "Garlic bread" } },
    new() { Id = "en_m13", Name = "Tuna Salad Sandwich", MealType = MealType.Fish, Language = "en", Ingredients = new() { "Canned tuna", "Mayonnaise", "Celery", "Whole wheat bread" }, SideDishes = new() { "Potato chips", "Pickles" } },
    new() { Id = "en_m14", Name = "Shepherd's Pie", MealType = MealType.Meat, Language = "en", Ingredients = new() { "Minced lamb or beef", "Mashed potatoes", "Peas", "Carrots", "Gravy" }, SideDishes = new() { "Green salad" } },
    new() { Id = "en_m15", Name = "Shakshuka with Warm Bread", MealType = MealType.Vegetarian, Language = "en", Ingredients = new() { "Eggs", "Tomatoes", "Bell peppers", "Onion", "Cumin" }, SideDishes = new() { "Pita bread" } },
    new() { Id = "en_m16", Name = "Baked Chicken Thighs with Potatoes", MealType = MealType.Chicken, Language = "en", Ingredients = new() { "Chicken thighs", "Potatoes", "Rosemary", "Garlic", "Olive oil" }, SideDishes = new() { "Cucumber yogurt salad" } },
    new() { Id = "en_m17", Name = "Chickpea and Spinach Stew", MealType = MealType.Vegan, Language = "en", Ingredients = new() { "Chickpeas", "Spinach", "Tomato puree", "Garlic", "Coriander" }, SideDishes = new() { "Rice" } },
    new() { Id = "en_m18", Name = "Shrimp Scampi with Pasta", MealType = MealType.Fish, Language = "en", Ingredients = new() { "Shrimp", "Linguine", "Garlic", "Butter", "Lemon juice", "Parsley" }, SideDishes = new() { "Crusty bread" } },

    // German meals. Tags drive the dish illustration picked by IMealImageService,
    // so keep at least one archetype tag on every entry.
    new() { Id = "de_m1", Name = "Käsespätzle", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "pasta" }, Ingredients = new() { "Spätzle", "Bergkäse", "Zwiebeln", "Butter", "Schnittlauch" }, SideDishes = new() { "Grüner Salat" } },
    new() { Id = "de_m2", Name = "Rinderrouladen mit Rotkohl", MealType = MealType.Meat, Language = "de", Tags = new() { "stew" }, Ingredients = new() { "Rinderrouladen", "Senf", "Speck", "Gewürzgurken", "Zwiebeln" }, SideDishes = new() { "Rotkohl", "Kartoffelklöße" } },
    new() { Id = "de_m3", Name = "Wiener Schnitzel mit Kartoffelsalat", MealType = MealType.Meat, Language = "de", Tags = new() { "potato" }, Ingredients = new() { "Kalbsschnitzel", "Semmelbrösel", "Ei", "Mehl", "Butterschmalz" }, SideDishes = new() { "Kartoffelsalat", "Zitrone" } },
    new() { Id = "de_m4", Name = "Gulasch mit Nudeln", MealType = MealType.Meat, Language = "de", Tags = new() { "stew" }, Ingredients = new() { "Rindfleisch", "Zwiebeln", "Paprikapulver", "Tomatenmark", "Rotwein" }, SideDishes = new() { "Bandnudeln", "Gurkensalat" } },
    new() { Id = "de_m5", Name = "Frikadellen mit Kartoffelpüree", MealType = MealType.Meat, Language = "de", Tags = new() { "potato" }, Ingredients = new() { "Hackfleisch", "Brötchen", "Ei", "Zwiebeln", "Petersilie" }, SideDishes = new() { "Kartoffelpüree", "Erbsen" } },
    new() { Id = "de_m6", Name = "Königsberger Klopse", MealType = MealType.Meat, Language = "de", Tags = new() { "stew" }, Ingredients = new() { "Hackfleisch", "Kapern", "Sahne", "Zitrone", "Lorbeer" }, SideDishes = new() { "Salzkartoffeln", "Rote Bete" } },
    new() { Id = "de_m7", Name = "Hähnchenschenkel mit Ofenkartoffeln", MealType = MealType.Chicken, Language = "de", Tags = new() { "potato" }, Ingredients = new() { "Hähnchenschenkel", "Kartoffeln", "Rosmarin", "Knoblauch", "Olivenöl" }, SideDishes = new() { "Kräuterquark", "Grüner Salat" } },
    new() { Id = "de_m8", Name = "Zürcher Geschnetzeltes", MealType = MealType.Chicken, Language = "de", Tags = new() { "stew" }, Ingredients = new() { "Hähnchenbrust", "Champignons", "Sahne", "Weißwein", "Zwiebeln" }, SideDishes = new() { "Rösti", "Reis" } },
    new() { Id = "de_m9", Name = "Hähnchenfrikassee mit Reis", MealType = MealType.Chicken, Language = "de", Tags = new() { "rice" }, Ingredients = new() { "Hähnchenfleisch", "Erbsen", "Spargel", "Sahne", "Zitrone" }, SideDishes = new() { "Reis" } },
    new() { Id = "de_m10", Name = "Currywurst mit Pommes", MealType = MealType.Meat, Language = "de", Tags = new() { "potato" }, Ingredients = new() { "Bratwurst", "Tomatenketchup", "Currypulver", "Paprikapulver" }, SideDishes = new() { "Pommes frites", "Mayonnaise" } },
    new() { Id = "de_m11", Name = "Lachs mit Dill und Kartoffeln", MealType = MealType.Fish, Language = "de", Tags = new() { "fish" }, Ingredients = new() { "Lachsfilet", "Dill", "Zitrone", "Butter", "Kartoffeln" }, SideDishes = new() { "Gurkensalat" } },
    new() { Id = "de_m12", Name = "Fischstäbchen mit Kartoffelsalat", MealType = MealType.Fish, Language = "de", Tags = new() { "fish", "potato" }, Ingredients = new() { "Fischstäbchen", "Kartoffeln", "Essig", "Öl", "Zwiebeln" }, SideDishes = new() { "Remoulade" } },
    new() { Id = "de_m13", Name = "Matjes nach Hausfrauenart", MealType = MealType.Fish, Language = "de", Tags = new() { "fish" }, Ingredients = new() { "Matjesfilet", "Saure Sahne", "Äpfel", "Zwiebeln", "Gewürzgurken" }, SideDishes = new() { "Pellkartoffeln" } },
    new() { Id = "de_m14", Name = "Forelle Müllerin Art", MealType = MealType.Fish, Language = "de", Tags = new() { "fish" }, Ingredients = new() { "Forelle", "Mehl", "Butter", "Zitrone", "Petersilie" }, SideDishes = new() { "Salzkartoffeln", "Blattsalat" } },
    new() { Id = "de_m15", Name = "Kartoffelpuffer mit Apfelmus", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "potato" }, Ingredients = new() { "Kartoffeln", "Zwiebeln", "Ei", "Mehl", "Muskat" }, SideDishes = new() { "Apfelmus" } },
    new() { Id = "de_m16", Name = "Gemüseauflauf mit Käse", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "stew" }, Ingredients = new() { "Zucchini", "Brokkoli", "Möhren", "Sahne", "Gouda" }, SideDishes = new() { "Baguette" } },
    new() { Id = "de_m17", Name = "Flammkuchen", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "pizza" }, Ingredients = new() { "Flammkuchenteig", "Schmand", "Zwiebeln", "Frühlingszwiebeln" }, SideDishes = new() { "Feldsalat" } },
    new() { Id = "de_m18", Name = "Maultaschen in der Brühe", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "soup" }, Ingredients = new() { "Maultaschen", "Gemüsebrühe", "Zwiebeln", "Schnittlauch" }, SideDishes = new() { "Kartoffelsalat" } },
    new() { Id = "de_m19", Name = "Spinat mit Spiegelei und Kartoffeln", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "eggs" }, Ingredients = new() { "Blattspinat", "Eier", "Kartoffeln", "Knoblauch", "Muskat" }, SideDishes = new() { "Salzkartoffeln" } },
    new() { Id = "de_m20", Name = "Pfannkuchen mit Apfelmus", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "eggs" }, Ingredients = new() { "Mehl", "Milch", "Eier", "Zucker", "Butter" }, SideDishes = new() { "Apfelmus", "Zimtzucker" } },
    new() { Id = "de_m21", Name = "Linsensuppe", MealType = MealType.Vegan, Language = "de", Tags = new() { "soup" }, Ingredients = new() { "Tellerlinsen", "Möhren", "Sellerie", "Lauch", "Lorbeer" }, SideDishes = new() { "Bauernbrot" } },
    new() { Id = "de_m22", Name = "Erbsensuppe", MealType = MealType.Vegan, Language = "de", Tags = new() { "soup" }, Ingredients = new() { "Trockenerbsen", "Möhren", "Kartoffeln", "Majoran", "Zwiebeln" }, SideDishes = new() { "Roggenbrot" } },
    new() { Id = "de_m23", Name = "Grünkohleintopf", MealType = MealType.Vegan, Language = "de", Tags = new() { "stew" }, Ingredients = new() { "Grünkohl", "Kartoffeln", "Zwiebeln", "Senf", "Gemüsebrühe" }, SideDishes = new() { "Senf", "Brot" } },
    new() { Id = "de_m24", Name = "Kürbissuppe mit Ingwer", MealType = MealType.Vegan, Language = "de", Tags = new() { "soup" }, Ingredients = new() { "Hokkaidokürbis", "Ingwer", "Kokosmilch", "Zwiebeln", "Gemüsebrühe" }, SideDishes = new() { "Kürbiskerne", "Brot" } },
    new() { Id = "de_m25", Name = "Gemüsepfanne mit Reis", MealType = MealType.Vegan, Language = "de", Tags = new() { "rice" }, Ingredients = new() { "Paprika", "Zucchini", "Möhren", "Sojasoße", "Reis" }, SideDishes = new() { "Sesam" } },
    new() { Id = "de_m26", Name = "Brotzeit mit Obatzda", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "bread" }, Ingredients = new() { "Camembert", "Butter", "Zwiebeln", "Paprikapulver", "Brezn" }, SideDishes = new() { "Radieschen", "Gewürzgurken" } },
    new() { Id = "de_m27", Name = "Bunter Sommersalat mit Feta", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "salad" }, Ingredients = new() { "Blattsalat", "Tomaten", "Gurke", "Feta", "Olivenöl" }, SideDishes = new() { "Baguette" } }
};

        foreach (var catalogMeal in catalogMeals)
        {
            if (string.IsNullOrWhiteSpace(catalogMeal.Language))
            {
                catalogMeal.Language = "ar";
            }
        }
        await _catalogService.SeedCatalogAsync(catalogMeals);
    }

    public async Task<bool> IsSeededAsync()
    {
        return await _catalogService.IsCatalogSeededAsync();
    }

    private static string NormalizeName(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

}

