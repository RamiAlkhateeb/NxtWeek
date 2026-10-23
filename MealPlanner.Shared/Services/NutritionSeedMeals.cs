using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>
/// High-protein everyday meals with nutrition per serving and measured ingredients, in all
/// three languages. Seeded alongside the main catalog; generated from the 3.0 nutrition prototype.
/// </summary>
public static class NutritionSeedMeals
{
    public static List<MealCatalogItem> All() => new()
    {
    // chickenRice
    new() { Id = "n1", Name = "دجاج مع أرز", MealType = MealType.Chicken, Language = "ar", Tags = new() { "chicken" },
        PreparationMinutes = 25, ServingsYield = 2,
        Nutrition = new() { Kcal = 590, Protein = 48, Carbs = 62, Fat = 14, Fibre = 4, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner },
        Ingredients = new() { "صدر دجاج", "أرز", "بصل", "زيت زيتون" },
        IngredientLines = new() { new() { Id = "chickenBreast", Name = "صدر دجاج", QtyPerServing = 150, Unit = "g", Aisle = "meat" }, new() { Id = "rice", Name = "أرز", QtyPerServing = 70, Unit = "g", Aisle = "pantry" }, new() { Id = "onion", Name = "بصل", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "زيت زيتون", QtyPerServing = 5, Unit = "ml", Aisle = "pantry" } } },
    // chickenRice
    new() { Id = "en_n1", Name = "Chicken and rice", MealType = MealType.Chicken, Language = "en", Tags = new() { "chicken" },
        PreparationMinutes = 25, ServingsYield = 2,
        Nutrition = new() { Kcal = 590, Protein = 48, Carbs = 62, Fat = 14, Fibre = 4, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner },
        Ingredients = new() { "Chicken breast", "Rice, dry", "Onion", "Olive oil" },
        IngredientLines = new() { new() { Id = "chickenBreast", Name = "Chicken breast", QtyPerServing = 150, Unit = "g", Aisle = "meat" }, new() { Id = "rice", Name = "Rice, dry", QtyPerServing = 70, Unit = "g", Aisle = "pantry" }, new() { Id = "onion", Name = "Onion", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "Olive oil", QtyPerServing = 5, Unit = "ml", Aisle = "pantry" } } },
    // chickenRice
    new() { Id = "de_n1", Name = "Hähnchen mit Reis", MealType = MealType.Chicken, Language = "de", Tags = new() { "chicken" },
        PreparationMinutes = 25, ServingsYield = 2,
        Nutrition = new() { Kcal = 590, Protein = 48, Carbs = 62, Fat = 14, Fibre = 4, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner },
        Ingredients = new() { "Hähnchenbrust", "Reis, trocken", "Zwiebel", "Olivenöl" },
        IngredientLines = new() { new() { Id = "chickenBreast", Name = "Hähnchenbrust", QtyPerServing = 150, Unit = "g", Aisle = "meat" }, new() { Id = "rice", Name = "Reis, trocken", QtyPerServing = 70, Unit = "g", Aisle = "pantry" }, new() { Id = "onion", Name = "Zwiebel", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "Olivenöl", QtyPerServing = 5, Unit = "ml", Aisle = "pantry" } } },
    // salmonBowl
    new() { Id = "n2", Name = "سلمون مع أرز وبروكلي", MealType = MealType.Fish, Language = "ar", Tags = new() { "fish" },
        PreparationMinutes = 25, ServingsYield = 1,
        Nutrition = new() { Kcal = 640, Protein = 42, Carbs = 58, Fat = 24, Fibre = 6, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "سلمون", "أرز", "بروكلي", "ليمون", "زيت زيتون" },
        IngredientLines = new() { new() { Id = "salmon", Name = "سلمون", QtyPerServing = 150, Unit = "g", Aisle = "meat" }, new() { Id = "rice", Name = "أرز", QtyPerServing = 70, Unit = "g", Aisle = "pantry" }, new() { Id = "broccoli", Name = "بروكلي", QtyPerServing = 150, Unit = "g", Aisle = "produce" }, new() { Id = "lemon", Name = "ليمون", QtyPerServing = 0.25, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "زيت زيتون", QtyPerServing = 5, Unit = "ml", Aisle = "pantry" } } },
    // salmonBowl
    new() { Id = "en_n2", Name = "Salmon, rice and broccoli", MealType = MealType.Fish, Language = "en", Tags = new() { "fish" },
        PreparationMinutes = 25, ServingsYield = 1,
        Nutrition = new() { Kcal = 640, Protein = 42, Carbs = 58, Fat = 24, Fibre = 6, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "Salmon fillet", "Rice, dry", "Broccoli", "Lemon", "Olive oil" },
        IngredientLines = new() { new() { Id = "salmon", Name = "Salmon fillet", QtyPerServing = 150, Unit = "g", Aisle = "meat" }, new() { Id = "rice", Name = "Rice, dry", QtyPerServing = 70, Unit = "g", Aisle = "pantry" }, new() { Id = "broccoli", Name = "Broccoli", QtyPerServing = 150, Unit = "g", Aisle = "produce" }, new() { Id = "lemon", Name = "Lemon", QtyPerServing = 0.25, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "Olive oil", QtyPerServing = 5, Unit = "ml", Aisle = "pantry" } } },
    // salmonBowl
    new() { Id = "de_n2", Name = "Lachs mit Reis und Brokkoli", MealType = MealType.Fish, Language = "de", Tags = new() { "fish" },
        PreparationMinutes = 25, ServingsYield = 1,
        Nutrition = new() { Kcal = 640, Protein = 42, Carbs = 58, Fat = 24, Fibre = 6, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "Lachsfilet", "Reis, trocken", "Brokkoli", "Zitrone", "Olivenöl" },
        IngredientLines = new() { new() { Id = "salmon", Name = "Lachsfilet", QtyPerServing = 150, Unit = "g", Aisle = "meat" }, new() { Id = "rice", Name = "Reis, trocken", QtyPerServing = 70, Unit = "g", Aisle = "pantry" }, new() { Id = "broccoli", Name = "Brokkoli", QtyPerServing = 150, Unit = "g", Aisle = "produce" }, new() { Id = "lemon", Name = "Zitrone", QtyPerServing = 0.25, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "Olivenöl", QtyPerServing = 5, Unit = "ml", Aisle = "pantry" } } },
    // shakshuka
    new() { Id = "n3", Name = "شكشوكة بالبيض", MealType = MealType.Vegetarian, Language = "ar", Tags = new() { "eggs" },
        PreparationMinutes = 20, ServingsYield = 2,
        Nutrition = new() { Kcal = 430, Protein = 26, Carbs = 22, Fat = 26, Fibre = 6, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "بيض", "طماطم معلبة", "فلفل حلو", "بصل", "زيت زيتون" },
        IngredientLines = new() { new() { Id = "eggs", Name = "بيض", QtyPerServing = 2, Unit = "pc", Aisle = "dairy" }, new() { Id = "tomatoes", Name = "طماطم معلبة", QtyPerServing = 200, Unit = "g", Aisle = "pantry" }, new() { Id = "pepper", Name = "فلفل حلو", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }, new() { Id = "onion", Name = "بصل", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "زيت زيتون", QtyPerServing = 10, Unit = "ml", Aisle = "pantry" } } },
    // shakshuka
    new() { Id = "en_n3", Name = "Shakshuka with eggs", MealType = MealType.Vegetarian, Language = "en", Tags = new() { "eggs" },
        PreparationMinutes = 20, ServingsYield = 2,
        Nutrition = new() { Kcal = 430, Protein = 26, Carbs = 22, Fat = 26, Fibre = 6, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "Eggs", "Tinned tomatoes", "Bell pepper", "Onion", "Olive oil" },
        IngredientLines = new() { new() { Id = "eggs", Name = "Eggs", QtyPerServing = 2, Unit = "pc", Aisle = "dairy" }, new() { Id = "tomatoes", Name = "Tinned tomatoes", QtyPerServing = 200, Unit = "g", Aisle = "pantry" }, new() { Id = "pepper", Name = "Bell pepper", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }, new() { Id = "onion", Name = "Onion", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "Olive oil", QtyPerServing = 10, Unit = "ml", Aisle = "pantry" } } },
    // shakshuka
    new() { Id = "de_n3", Name = "Shakshuka mit Eiern", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "eggs" },
        PreparationMinutes = 20, ServingsYield = 2,
        Nutrition = new() { Kcal = 430, Protein = 26, Carbs = 22, Fat = 26, Fibre = 6, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "Eier", "Dosentomaten", "Paprika", "Zwiebel", "Olivenöl" },
        IngredientLines = new() { new() { Id = "eggs", Name = "Eier", QtyPerServing = 2, Unit = "pc", Aisle = "dairy" }, new() { Id = "tomatoes", Name = "Dosentomaten", QtyPerServing = 200, Unit = "g", Aisle = "pantry" }, new() { Id = "pepper", Name = "Paprika", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }, new() { Id = "onion", Name = "Zwiebel", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "Olivenöl", QtyPerServing = 10, Unit = "ml", Aisle = "pantry" } } },
    // lentilSoup
    new() { Id = "n4", Name = "شوربة عدس", MealType = MealType.Vegetarian, Language = "ar", Tags = new() { "soup" },
        PreparationMinutes = 35, ServingsYield = 4,
        Nutrition = new() { Kcal = 380, Protein = 22, Carbs = 54, Fat = 8, Fibre = 14, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "عدس أحمر", "جزر", "بصل", "ليمون", "زيت زيتون" },
        IngredientLines = new() { new() { Id = "redLentils", Name = "عدس أحمر", QtyPerServing = 80, Unit = "g", Aisle = "pantry" }, new() { Id = "carrot", Name = "جزر", QtyPerServing = 1, Unit = "pc", Aisle = "produce" }, new() { Id = "onion", Name = "بصل", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }, new() { Id = "lemon", Name = "ليمون", QtyPerServing = 0.25, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "زيت زيتون", QtyPerServing = 10, Unit = "ml", Aisle = "pantry" } } },
    // lentilSoup
    new() { Id = "en_n4", Name = "Red lentil soup", MealType = MealType.Vegetarian, Language = "en", Tags = new() { "soup" },
        PreparationMinutes = 35, ServingsYield = 4,
        Nutrition = new() { Kcal = 380, Protein = 22, Carbs = 54, Fat = 8, Fibre = 14, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "Red lentils", "Carrot", "Onion", "Lemon", "Olive oil" },
        IngredientLines = new() { new() { Id = "redLentils", Name = "Red lentils", QtyPerServing = 80, Unit = "g", Aisle = "pantry" }, new() { Id = "carrot", Name = "Carrot", QtyPerServing = 1, Unit = "pc", Aisle = "produce" }, new() { Id = "onion", Name = "Onion", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }, new() { Id = "lemon", Name = "Lemon", QtyPerServing = 0.25, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "Olive oil", QtyPerServing = 10, Unit = "ml", Aisle = "pantry" } } },
    // lentilSoup
    new() { Id = "de_n4", Name = "Rote Linsensuppe", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "soup" },
        PreparationMinutes = 35, ServingsYield = 4,
        Nutrition = new() { Kcal = 380, Protein = 22, Carbs = 54, Fat = 8, Fibre = 14, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "Rote Linsen", "Karotte", "Zwiebel", "Zitrone", "Olivenöl" },
        IngredientLines = new() { new() { Id = "redLentils", Name = "Rote Linsen", QtyPerServing = 80, Unit = "g", Aisle = "pantry" }, new() { Id = "carrot", Name = "Karotte", QtyPerServing = 1, Unit = "pc", Aisle = "produce" }, new() { Id = "onion", Name = "Zwiebel", QtyPerServing = 0.5, Unit = "pc", Aisle = "produce" }, new() { Id = "lemon", Name = "Zitrone", QtyPerServing = 0.25, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "Olivenöl", QtyPerServing = 10, Unit = "ml", Aisle = "pantry" } } },
    // tunaWrap
    new() { Id = "n5", Name = "ساندويش تونة", MealType = MealType.Fish, Language = "ar", Tags = new() { "bread" },
        PreparationMinutes = 10, ServingsYield = 1,
        Nutrition = new() { Kcal = 410, Protein = 38, Carbs = 36, Fat = 11, Fibre = 5, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner },
        Ingredients = new() { "تونة معلبة", "خبز لفائف", "لبن", "خس" },
        IngredientLines = new() { new() { Id = "tuna", Name = "تونة معلبة", QtyPerServing = 120, Unit = "g", Aisle = "pantry" }, new() { Id = "wrap", Name = "خبز لفائف", QtyPerServing = 1, Unit = "pc", Aisle = "bakery" }, new() { Id = "yogurt", Name = "لبن", QtyPerServing = 50, Unit = "g", Aisle = "dairy" }, new() { Id = "lettuce", Name = "خس", QtyPerServing = 50, Unit = "g", Aisle = "produce" } } },
    // tunaWrap
    new() { Id = "en_n5", Name = "Tuna salad wrap", MealType = MealType.Fish, Language = "en", Tags = new() { "bread" },
        PreparationMinutes = 10, ServingsYield = 1,
        Nutrition = new() { Kcal = 410, Protein = 38, Carbs = 36, Fat = 11, Fibre = 5, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner },
        Ingredients = new() { "Tuna, tinned", "Wholemeal wrap", "Yogurt", "Lettuce" },
        IngredientLines = new() { new() { Id = "tuna", Name = "Tuna, tinned", QtyPerServing = 120, Unit = "g", Aisle = "pantry" }, new() { Id = "wrap", Name = "Wholemeal wrap", QtyPerServing = 1, Unit = "pc", Aisle = "bakery" }, new() { Id = "yogurt", Name = "Yogurt", QtyPerServing = 50, Unit = "g", Aisle = "dairy" }, new() { Id = "lettuce", Name = "Lettuce", QtyPerServing = 50, Unit = "g", Aisle = "produce" } } },
    // tunaWrap
    new() { Id = "de_n5", Name = "Thunfisch-Wrap", MealType = MealType.Fish, Language = "de", Tags = new() { "bread" },
        PreparationMinutes = 10, ServingsYield = 1,
        Nutrition = new() { Kcal = 410, Protein = 38, Carbs = 36, Fat = 11, Fibre = 5, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.PostWorkout, MealSlot.Dinner },
        Ingredients = new() { "Thunfisch, Dose", "Vollkorn-Wrap", "Joghurt", "Salat" },
        IngredientLines = new() { new() { Id = "tuna", Name = "Thunfisch, Dose", QtyPerServing = 120, Unit = "g", Aisle = "pantry" }, new() { Id = "wrap", Name = "Vollkorn-Wrap", QtyPerServing = 1, Unit = "pc", Aisle = "bakery" }, new() { Id = "yogurt", Name = "Joghurt", QtyPerServing = 50, Unit = "g", Aisle = "dairy" }, new() { Id = "lettuce", Name = "Salat", QtyPerServing = 50, Unit = "g", Aisle = "produce" } } },
    // shake
    new() { Id = "n6", Name = "شيك بروتين مع موز", MealType = MealType.Vegetarian, Language = "ar",
        PreparationMinutes = 3, ServingsYield = 1,
        Nutrition = new() { Kcal = 310, Protein = 32, Carbs = 38, Fat = 3, Fibre = 3, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.PostWorkout, MealSlot.Snack },
        Ingredients = new() { "بروتين مصل اللبن", "موز", "حليب" },
        IngredientLines = new() { new() { Id = "whey", Name = "بروتين مصل اللبن", QtyPerServing = 30, Unit = "g", Aisle = "pantry" }, new() { Id = "banana", Name = "موز", QtyPerServing = 1, Unit = "pc", Aisle = "produce" }, new() { Id = "milk", Name = "حليب", QtyPerServing = 250, Unit = "ml", Aisle = "dairy" } } },
    // shake
    new() { Id = "en_n6", Name = "Protein shake and banana", MealType = MealType.Vegetarian, Language = "en",
        PreparationMinutes = 3, ServingsYield = 1,
        Nutrition = new() { Kcal = 310, Protein = 32, Carbs = 38, Fat = 3, Fibre = 3, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.PostWorkout, MealSlot.Snack },
        Ingredients = new() { "Whey protein", "Banana", "Milk" },
        IngredientLines = new() { new() { Id = "whey", Name = "Whey protein", QtyPerServing = 30, Unit = "g", Aisle = "pantry" }, new() { Id = "banana", Name = "Banana", QtyPerServing = 1, Unit = "pc", Aisle = "produce" }, new() { Id = "milk", Name = "Milk", QtyPerServing = 250, Unit = "ml", Aisle = "dairy" } } },
    // shake
    new() { Id = "de_n6", Name = "Proteinshake mit Banane", MealType = MealType.Vegetarian, Language = "de",
        PreparationMinutes = 3, ServingsYield = 1,
        Nutrition = new() { Kcal = 310, Protein = 32, Carbs = 38, Fat = 3, Fibre = 3, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.PostWorkout, MealSlot.Snack },
        Ingredients = new() { "Whey-Protein", "Banane", "Milch" },
        IngredientLines = new() { new() { Id = "whey", Name = "Whey-Protein", QtyPerServing = 30, Unit = "g", Aisle = "pantry" }, new() { Id = "banana", Name = "Banane", QtyPerServing = 1, Unit = "pc", Aisle = "produce" }, new() { Id = "milk", Name = "Milch", QtyPerServing = 250, Unit = "ml", Aisle = "dairy" } } },
    // cottage
    new() { Id = "n7", Name = "جبنة قريش مع توت", MealType = MealType.Vegetarian, Language = "ar",
        PreparationMinutes = 3, ServingsYield = 1,
        Nutrition = new() { Kcal = 220, Protein = 24, Carbs = 18, Fat = 5, Fibre = 3, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.PostWorkout, MealSlot.Snack },
        Ingredients = new() { "جبنة قريش", "توت" },
        IngredientLines = new() { new() { Id = "cottage", Name = "جبنة قريش", QtyPerServing = 200, Unit = "g", Aisle = "dairy" }, new() { Id = "berries", Name = "توت", QtyPerServing = 80, Unit = "g", Aisle = "produce" } } },
    // cottage
    new() { Id = "en_n7", Name = "Cottage cheese and berries", MealType = MealType.Vegetarian, Language = "en",
        PreparationMinutes = 3, ServingsYield = 1,
        Nutrition = new() { Kcal = 220, Protein = 24, Carbs = 18, Fat = 5, Fibre = 3, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.PostWorkout, MealSlot.Snack },
        Ingredients = new() { "Cottage cheese", "Berries" },
        IngredientLines = new() { new() { Id = "cottage", Name = "Cottage cheese", QtyPerServing = 200, Unit = "g", Aisle = "dairy" }, new() { Id = "berries", Name = "Berries", QtyPerServing = 80, Unit = "g", Aisle = "produce" } } },
    // cottage
    new() { Id = "de_n7", Name = "Hüttenkäse mit Beeren", MealType = MealType.Vegetarian, Language = "de",
        PreparationMinutes = 3, ServingsYield = 1,
        Nutrition = new() { Kcal = 220, Protein = 24, Carbs = 18, Fat = 5, Fibre = 3, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.PostWorkout, MealSlot.Snack },
        Ingredients = new() { "Hüttenkäse", "Beeren" },
        IngredientLines = new() { new() { Id = "cottage", Name = "Hüttenkäse", QtyPerServing = 200, Unit = "g", Aisle = "dairy" }, new() { Id = "berries", Name = "Beeren", QtyPerServing = 80, Unit = "g", Aisle = "produce" } } },
    // shawarma
    new() { Id = "n8", Name = "طبق شاورما دجاج", MealType = MealType.Chicken, Language = "ar", Tags = new() { "rice" },
        PreparationMinutes = 30, ServingsYield = 2,
        Nutrition = new() { Kcal = 560, Protein = 44, Carbs = 48, Fat = 20, Fibre = 7, Source = NutritionSource.Manual },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "فخذ دجاج", "أرز", "خيار", "لبن", "خبز عربي" },
        IngredientLines = new() { new() { Id = "chickenThigh", Name = "فخذ دجاج", QtyPerServing = 160, Unit = "g", Aisle = "meat" }, new() { Id = "rice", Name = "أرز", QtyPerServing = 60, Unit = "g", Aisle = "pantry" }, new() { Id = "cucumber", Name = "خيار", QtyPerServing = 80, Unit = "g", Aisle = "produce" }, new() { Id = "yogurt", Name = "لبن", QtyPerServing = 40, Unit = "g", Aisle = "dairy" }, new() { Id = "pita", Name = "خبز عربي", QtyPerServing = 1, Unit = "pc", Aisle = "bakery" } } },
    // shawarma
    new() { Id = "en_n8", Name = "Chicken shawarma bowl", MealType = MealType.Chicken, Language = "en", Tags = new() { "rice" },
        PreparationMinutes = 30, ServingsYield = 2,
        Nutrition = new() { Kcal = 560, Protein = 44, Carbs = 48, Fat = 20, Fibre = 7, Source = NutritionSource.Manual },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "Chicken thigh", "Rice, dry", "Cucumber", "Yogurt", "Pita bread" },
        IngredientLines = new() { new() { Id = "chickenThigh", Name = "Chicken thigh", QtyPerServing = 160, Unit = "g", Aisle = "meat" }, new() { Id = "rice", Name = "Rice, dry", QtyPerServing = 60, Unit = "g", Aisle = "pantry" }, new() { Id = "cucumber", Name = "Cucumber", QtyPerServing = 80, Unit = "g", Aisle = "produce" }, new() { Id = "yogurt", Name = "Yogurt", QtyPerServing = 40, Unit = "g", Aisle = "dairy" }, new() { Id = "pita", Name = "Pita bread", QtyPerServing = 1, Unit = "pc", Aisle = "bakery" } } },
    // shawarma
    new() { Id = "de_n8", Name = "Hähnchen-Shawarma-Bowl", MealType = MealType.Chicken, Language = "de", Tags = new() { "rice" },
        PreparationMinutes = 30, ServingsYield = 2,
        Nutrition = new() { Kcal = 560, Protein = 44, Carbs = 48, Fat = 20, Fibre = 7, Source = NutritionSource.Manual },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "Hähnchenschenkel", "Reis, trocken", "Gurke", "Joghurt", "Fladenbrot" },
        IngredientLines = new() { new() { Id = "chickenThigh", Name = "Hähnchenschenkel", QtyPerServing = 160, Unit = "g", Aisle = "meat" }, new() { Id = "rice", Name = "Reis, trocken", QtyPerServing = 60, Unit = "g", Aisle = "pantry" }, new() { Id = "cucumber", Name = "Gurke", QtyPerServing = 80, Unit = "g", Aisle = "produce" }, new() { Id = "yogurt", Name = "Joghurt", QtyPerServing = 40, Unit = "g", Aisle = "dairy" }, new() { Id = "pita", Name = "Fladenbrot", QtyPerServing = 1, Unit = "pc", Aisle = "bakery" } } },
    // mujaddara
    new() { Id = "n9", Name = "مجدرة", MealType = MealType.Vegetarian, Language = "ar", Tags = new() { "stew" },
        PreparationMinutes = 40, ServingsYield = 3,
        Nutrition = new() { Kcal = 520, Protein = 19, Carbs = 78, Fat = 14, Fibre = 12, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "عدس بني", "أرز", "بصل", "زيت زيتون" },
        IngredientLines = new() { new() { Id = "brownLentils", Name = "عدس بني", QtyPerServing = 70, Unit = "g", Aisle = "pantry" }, new() { Id = "rice", Name = "أرز", QtyPerServing = 50, Unit = "g", Aisle = "pantry" }, new() { Id = "onion", Name = "بصل", QtyPerServing = 1, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "زيت زيتون", QtyPerServing = 10, Unit = "ml", Aisle = "pantry" } } },
    // mujaddara
    new() { Id = "en_n9", Name = "Mujaddara", MealType = MealType.Vegetarian, Language = "en", Tags = new() { "stew" },
        PreparationMinutes = 40, ServingsYield = 3,
        Nutrition = new() { Kcal = 520, Protein = 19, Carbs = 78, Fat = 14, Fibre = 12, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "Brown lentils", "Rice, dry", "Onion", "Olive oil" },
        IngredientLines = new() { new() { Id = "brownLentils", Name = "Brown lentils", QtyPerServing = 70, Unit = "g", Aisle = "pantry" }, new() { Id = "rice", Name = "Rice, dry", QtyPerServing = 50, Unit = "g", Aisle = "pantry" }, new() { Id = "onion", Name = "Onion", QtyPerServing = 1, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "Olive oil", QtyPerServing = 10, Unit = "ml", Aisle = "pantry" } } },
    // mujaddara
    new() { Id = "de_n9", Name = "Mujaddara", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "stew" },
        PreparationMinutes = 40, ServingsYield = 3,
        Nutrition = new() { Kcal = 520, Protein = 19, Carbs = 78, Fat = 14, Fibre = 12, Source = NutritionSource.Estimate },
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "Braune Linsen", "Reis, trocken", "Zwiebel", "Olivenöl" },
        IngredientLines = new() { new() { Id = "brownLentils", Name = "Braune Linsen", QtyPerServing = 70, Unit = "g", Aisle = "pantry" }, new() { Id = "rice", Name = "Reis, trocken", QtyPerServing = 50, Unit = "g", Aisle = "pantry" }, new() { Id = "onion", Name = "Zwiebel", QtyPerServing = 1, Unit = "pc", Aisle = "produce" }, new() { Id = "oil", Name = "Olivenöl", QtyPerServing = 10, Unit = "ml", Aisle = "pantry" } } },
    // koshari (no nutrition on purpose: shows the "Estimate from ingredients" flow)
    new() { Id = "n10", Name = "كشري", MealType = MealType.Vegetarian, Language = "ar", Tags = new() { "rice" },
        PreparationMinutes = 45, ServingsYield = 4,
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "أرز", "عدس بني", "معكرونة", "بصل" },
        IngredientLines = new() { new() { Id = "rice", Name = "أرز", QtyPerServing = 60, Unit = "g", Aisle = "pantry" }, new() { Id = "brownLentils", Name = "عدس بني", QtyPerServing = 40, Unit = "g", Aisle = "pantry" }, new() { Id = "pasta", Name = "معكرونة", QtyPerServing = 40, Unit = "g", Aisle = "pantry" }, new() { Id = "onion", Name = "بصل", QtyPerServing = 1, Unit = "pc", Aisle = "produce" } } },
    // koshari (no nutrition on purpose: shows the "Estimate from ingredients" flow)
    new() { Id = "en_n10", Name = "Koshari", MealType = MealType.Vegetarian, Language = "en", Tags = new() { "rice" },
        PreparationMinutes = 45, ServingsYield = 4,
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "Rice, dry", "Brown lentils", "Pasta", "Onion" },
        IngredientLines = new() { new() { Id = "rice", Name = "Rice, dry", QtyPerServing = 60, Unit = "g", Aisle = "pantry" }, new() { Id = "brownLentils", Name = "Brown lentils", QtyPerServing = 40, Unit = "g", Aisle = "pantry" }, new() { Id = "pasta", Name = "Pasta", QtyPerServing = 40, Unit = "g", Aisle = "pantry" }, new() { Id = "onion", Name = "Onion", QtyPerServing = 1, Unit = "pc", Aisle = "produce" } } },
    // koshari (no nutrition on purpose: shows the "Estimate from ingredients" flow)
    new() { Id = "de_n10", Name = "Koshari", MealType = MealType.Vegetarian, Language = "de", Tags = new() { "rice" },
        PreparationMinutes = 45, ServingsYield = 4,
        SuitableSlots = new() { MealSlot.Meal1, MealSlot.Dinner },
        Ingredients = new() { "Reis, trocken", "Braune Linsen", "Nudeln", "Zwiebel" },
        IngredientLines = new() { new() { Id = "rice", Name = "Reis, trocken", QtyPerServing = 60, Unit = "g", Aisle = "pantry" }, new() { Id = "brownLentils", Name = "Braune Linsen", QtyPerServing = 40, Unit = "g", Aisle = "pantry" }, new() { Id = "pasta", Name = "Nudeln", QtyPerServing = 40, Unit = "g", Aisle = "pantry" }, new() { Id = "onion", Name = "Zwiebel", QtyPerServing = 1, Unit = "pc", Aisle = "produce" } } },
    };
}
