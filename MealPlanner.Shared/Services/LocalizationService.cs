using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public class LocalizationService : ILocalizationService
{
    private readonly ILanguageService? _languageService;
    private string _currentLanguage = "ar";
    private bool _initialized;

    public LocalizationService(ILanguageService? languageService = null)
    {
        _languageService = languageService;
    }

    public string CurrentLanguage => _currentLanguage;
    public bool IsArabic => _currentLanguage == "ar";
    public bool IsEnglish => _currentLanguage == "en";
    public string Direction => IsArabic ? "rtl" : "ltr";

    public event Action? OnLanguageChanged;

    public string this[string key] => T(key);

    public string T(string key)
    {
        if (Translations.TryGetValue(key, out var dict))
        {
            if (dict.TryGetValue(_currentLanguage, out var val))
            {
                return val;
            }
            if (dict.TryGetValue("ar", out var fallback))
            {
                return fallback;
            }
        }
        return key;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        if (_languageService != null)
        {
            try
            {
                var saved = await _languageService.GetAsync();
                if (!string.IsNullOrWhiteSpace(saved) && (saved == "ar" || saved == "en"))
                {
                    _currentLanguage = saved;
                }
            }
            catch
            {
                _currentLanguage = "ar";
            }
        }
        _initialized = true;
    }

    public async Task SetLanguageAsync(string language)
    {
        if (language != "ar" && language != "en") language = "ar";
        if (_currentLanguage == language && _initialized) return;

        _currentLanguage = language;
        _initialized = true;

        if (_languageService != null)
        {
            try
            {
                await _languageService.SetAsync(language);
            }
            catch
            {
                // ignore storage failures in tests/ssr
            }
        }

        OnLanguageChanged?.Invoke();
    }

    public string GetDayName(DayOfWeek day)
    {
        return IsArabic ? ArabicDays[(int)day] : EnglishDays[(int)day];
    }

    public string GetDayName(DateOnly date)
    {
        return GetDayName(date.DayOfWeek);
    }

    public string GetMealTypeName(MealType type)
    {
        return type switch
        {
            MealType.Meat => this["meal_meat"],
            MealType.Chicken => this["meal_chicken"],
            MealType.Fish => this["meal_fish"],
            MealType.Vegetarian => this["meal_vegetarian"],
            MealType.Vegan => this["meal_vegan"],
            _ => type.ToString()
        };
    }

    private static readonly string[] ArabicDays = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];
    private static readonly string[] EnglishDays = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["app_title"] = new() { ["ar"] = "مكدوس", ["en"] = "NxtWeek" },
        ["app_subtitle"] = new() { ["ar"] = "مخطط وجبات شخصي", ["en"] = "Personal Weekly Meal Planner" },
        ["loading"] = new() { ["ar"] = "جاري التحميل...", ["en"] = "Loading..." },
        ["save"] = new() { ["ar"] = "حفظ", ["en"] = "Save" },
        ["cancel"] = new() { ["ar"] = "إلغاء", ["en"] = "Cancel" },
        ["close"] = new() { ["ar"] = "إغلاق", ["en"] = "Close" },
        ["delete"] = new() { ["ar"] = "حذف", ["en"] = "Delete" },
        ["edit"] = new() { ["ar"] = "تعديل", ["en"] = "Edit" },
        ["copy"] = new() { ["ar"] = "نسخ", ["en"] = "Copy" },

        // Nav
        ["nav_week"] = new() { ["ar"] = "الأسبوع", ["en"] = "Week" },
        ["nav_meals"] = new() { ["ar"] = "الوجبات", ["en"] = "Meals" },
        ["nav_settings"] = new() { ["ar"] = "الإعدادات", ["en"] = "Settings" },

        // Settings Page
        ["settings_title"] = new() { ["ar"] = "الإعدادات", ["en"] = "Settings" },
        ["settings_subtitle"] = new() { ["ar"] = "كل ما تحتاجه في مكان واحد", ["en"] = "Everything you need in one place" },
        ["settings_lang_title"] = new() { ["ar"] = "🌐 لغة التطبيق", ["en"] = "🌐 App Language" },
        ["settings_lang_desc"] = new() { ["ar"] = "اختر لغة الواجهة والاتجاه المفضل لديك.", ["en"] = "Choose your preferred interface language and direction." },
        ["lang_ar"] = new() { ["ar"] = "العربية", ["en"] = "العربية" },
        ["lang_en"] = new() { ["ar"] = "English", ["en"] = "English" },
        ["settings_username_title"] = new() { ["ar"] = "👤 اسم المستخدم", ["en"] = "👤 Username" },
        ["settings_username_prompt"] = new() { ["ar"] = "اختر اسماً قبل إرسال أو قبول طلبات الصداقة.", ["en"] = "Choose a name before sending or accepting friend requests." },
        ["settings_username_change"] = new() { ["ar"] = "تغيير اسم المستخدم", ["en"] = "Change username" },
        ["settings_username_choose"] = new() { ["ar"] = "اختر اسم مستخدم", ["en"] = "Choose username" },
        ["settings_username_placeholder"] = new() { ["ar"] = "مثال: rami_food", ["en"] = "e.g. rami_food" },
        ["settings_username_available"] = new() { ["ar"] = "اسم المستخدم متاح ✓", ["en"] = "Username is available ✓" },
        ["settings_username_taken"] = new() { ["ar"] = "اسم المستخدم مستخدم بالفعل.", ["en"] = "Username is already taken." },
        ["settings_username_saved"] = new() { ["ar"] = "تم حفظ اسم المستخدم ✨", ["en"] = "Username saved successfully ✨" },
        ["settings_username_copied"] = new() { ["ar"] = "تم نسخ اسم المستخدم.", ["en"] = "Username copied." },
        ["settings_recover_title"] = new() { ["ar"] = "🔁 استرجاع خطتي", ["en"] = "🔁 Recover My Plan" },
        ["settings_recover_desc"] = new() { ["ar"] = "هل مسحت بيانات المتصفح أو غيّرت جهازك؟ استعد خططك ووجباتك باسم المستخدم الذي اخترته سابقاً.", ["en"] = "Cleared browser data or changed devices? Recover your plan and meals with your username." },
        ["settings_recover_link"] = new() { ["ar"] = "استرجاع باسم المستخدم ←", ["en"] = "Recover by username →" },
        ["settings_install_title"] = new() { ["ar"] = "📲 تثبيت على الشاشة الرئيسية", ["en"] = "📲 Install on Home Screen" },
        ["settings_install_desc"] = new() { ["ar"] = "أضف مكدوس إلى شاشة هاتفك الرئيسية لفتحه كتطبيق سريع على أندرويد وiPhone.", ["en"] = "Add NxtWeek to your home screen for quick access on Android & iPhone." },
        ["settings_install_link"] = new() { ["ar"] = "كيف أثبّت مكدوس؟ ←", ["en"] = "How to install NxtWeek? →" },
        ["settings_about_title"] = new() { ["ar"] = "ℹ️ حول التطبيق", ["en"] = "ℹ️ About" },
        ["settings_about_desc"] = new() { ["ar"] = "المكدوس — مخطط وجبات شخصي", ["en"] = "NxtWeek — Personal Weekly Meal Planner" },
        ["settings_about_version"] = new() { ["ar"] = "الإصدار 1.0", ["en"] = "Version 1.0" },
        ["settings_about_dev"] = new() { ["ar"] = "الموقع الشخصي للمطور ↗", ["en"] = "Developer's Website ↗" },

        // Week Page
        ["week_title"] = new() { ["ar"] = "خطة الأسبوع", ["en"] = "Weekly Plan" },
        ["week_month_view"] = new() { ["ar"] = "عرض الشهر", ["en"] = "Month View" },
        ["week_surprise"] = new() { ["ar"] = "فاجئني", ["en"] = "Surprise Me" },
        ["week_share"] = new() { ["ar"] = "مشاركة", ["en"] = "Share" },
        ["week_empty_meal"] = new() { ["ar"] = "لا توجد وجبة محددة", ["en"] = "No meal planned" },
        ["week_choose_meal"] = new() { ["ar"] = "اختيار وجبة", ["en"] = "Choose Meal" },
        ["week_change_meal"] = new() { ["ar"] = "تغيير", ["en"] = "Change" },
        ["week_suggest_meal"] = new() { ["ar"] = "اقتراح وجبة", ["en"] = "Suggest Meal" },
        ["week_move_meal"] = new() { ["ar"] = "نقل ليوم آخر", ["en"] = "Move to another day" },
        ["week_clear_meal"] = new() { ["ar"] = "مسح", ["en"] = "Clear" },
        ["week_today"] = new() { ["ar"] = "اليوم", ["en"] = "Today" },
        ["week_current"] = new() { ["ar"] = "هذا الأسبوع", ["en"] = "This Week" },
        ["week_prev"] = new() { ["ar"] = "الأسبوع السابق", ["en"] = "Previous Week" },
        ["week_next"] = new() { ["ar"] = "الأسبوع القادم", ["en"] = "Next Week" },
        ["week_nudge_prompt"] = new() { ["ar"] = "اختر اسم مستخدم لحفظ خطتك بشكل دائم ومشاركتها مع أصدقائك", ["en"] = "Choose a username to permanently save your plan and share with friends" },

        // Meals Page
        ["meals_title"] = new() { ["ar"] = "الوجبات", ["en"] = "Meals" },
        ["meals_search_placeholder"] = new() { ["ar"] = "ابحث عن وجبة أو مكون...", ["en"] = "Search meals or ingredients..." },
        ["meals_filter_all"] = new() { ["ar"] = "الكل", ["en"] = "All" },
        ["meals_filter_favs"] = new() { ["ar"] = "المفضلة", ["en"] = "Favorites" },
        ["meal_meat"] = new() { ["ar"] = "لحوم", ["en"] = "Meat" },
        ["meal_chicken"] = new() { ["ar"] = "دواجن", ["en"] = "Chicken" },
        ["meal_fish"] = new() { ["ar"] = "مأكولات بحرية", ["en"] = "Fish & Seafood" },
        ["meal_vegetarian"] = new() { ["ar"] = "نباتي", ["en"] = "Vegetarian" },
        ["meal_vegan"] = new() { ["ar"] = "نباتي صرف", ["en"] = "Vegan" },
        ["meals_empty_title"] = new() { ["ar"] = "لم يتم العثور على وجبات", ["en"] = "No meals found" },
        ["meals_empty_desc"] = new() { ["ar"] = "جرّب تغيير كلمات البحث أو الفلاتر", ["en"] = "Try changing your search terms or filters" },
        ["meals_add_custom"] = new() { ["ar"] = "إضافة وجبة جديدة", ["en"] = "Add New Meal" },
        ["meals_lang_all"] = new() { ["ar"] = "جميع اللغات", ["en"] = "All Languages" },
        ["meals_lang_app"] = new() { ["ar"] = "وجبات باللغة الحالية", ["en"] = "Current Language Only" },

        // Meal Details & Bottom Sheet
        ["meal_ingredients"] = new() { ["ar"] = "المكونات", ["en"] = "Ingredients" },
        ["meal_side_dishes"] = new() { ["ar"] = "أطباق جانبية", ["en"] = "Side Dishes" },
        ["meal_select_for_day"] = new() { ["ar"] = "اختيار لهذا اليوم", ["en"] = "Select for this day" },
        ["meal_add_to_plan"] = new() { ["ar"] = "إضافة إلى الخطة", ["en"] = "Add to Plan" },
        ["meal_favorite"] = new() { ["ar"] = "المفضلة", ["en"] = "Favorite" },
        ["meal_no_ingredients"] = new() { ["ar"] = "لا توجد مكونات مسجلة", ["en"] = "No ingredients listed" },
        ["meal_no_side_dishes"] = new() { ["ar"] = "لا توجد أطباق جانبية", ["en"] = "No side dishes" }
    };
}
