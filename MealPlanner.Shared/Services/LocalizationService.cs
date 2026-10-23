using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>
/// Dictionary-backed UI translation. Every user-facing string in the app belongs in
/// <see cref="Translations"/> with an entry for each supported language — never write an
/// inline `Loc.IsArabic ? "..." : "..."` ternary in a component, it silently breaks every
/// language that is not one of those two.
/// </summary>
public class LocalizationService : ILocalizationService
{
    public const string DefaultLanguage = "ar";

    private static readonly LanguageOption[] Languages =
    [
        new("ar", "العربية", "🇸🇾", "rtl"),
        new("en", "English", "🇬🇧", "ltr"),
        new("de", "Deutsch", "🇩🇪", "ltr")
    ];

    /// <summary>Every language code the app ships, for callers that need the set
    /// without depending on an instance (catalog seeding, for example).</summary>
    public static IReadOnlyList<string> SupportedLanguageCodes { get; } = Languages.Select(l => l.Code).ToArray();

    /// <summary>Tried in order when a key has no entry for the active language.</summary>
    private static readonly string[] FallbackChain = ["en", "ar"];

    private readonly ILanguageService? _languageService;
    private string _currentLanguage = DefaultLanguage;
    private bool _initialized;

    public LocalizationService(ILanguageService? languageService = null)
    {
        _languageService = languageService;
    }

    public string CurrentLanguage => _currentLanguage;
    public IReadOnlyList<LanguageOption> AvailableLanguages => Languages;

    public bool IsArabic => _currentLanguage == "ar";
    public bool IsRtl => Direction == "rtl";
    public string Direction => Current.Direction;
    public CultureInfo Culture => CultureFor(_currentLanguage);

    public event Action? OnLanguageChanged;

    private LanguageOption Current =>
        Languages.FirstOrDefault(l => l.Code == _currentLanguage) ?? Languages[0];

    public static bool IsSupported(string? language) =>
        !string.IsNullOrWhiteSpace(language) && Languages.Any(l => l.Code == language);

    public string this[string key] => T(key);

    public string T(string key)
    {
        if (!Translations.TryGetValue(key, out var dict)) return key;
        if (dict.TryGetValue(_currentLanguage, out var val) && !string.IsNullOrEmpty(val)) return val;
        foreach (var fallback in FallbackChain)
        {
            if (dict.TryGetValue(fallback, out var alt) && !string.IsNullOrEmpty(alt)) return alt;
        }
        return key;
    }

    /// <summary>
    /// Formats a translation that carries positional placeholders. Word order differs
    /// between the supported languages, so translated sentences use {0}/{1} rather than
    /// string concatenation at the call site.
    /// </summary>
    public string T(string key, params object[] args)
    {
        var template = T(key);
        if (args is null || args.Length == 0) return template;
        try { return string.Format(Culture, template, args); }
        catch (FormatException) { return template; }
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        if (_languageService is not null)
        {
            try
            {
                var saved = await _languageService.GetAsync();
                if (IsSupported(saved)) _currentLanguage = saved;
            }
            catch
            {
                _currentLanguage = DefaultLanguage;
            }
        }
        _initialized = true;
    }

    public async Task SetLanguageAsync(string language)
    {
        if (!IsSupported(language)) language = DefaultLanguage;
        if (_currentLanguage == language && _initialized) return;

        _currentLanguage = language;
        _initialized = true;

        if (_languageService is not null)
        {
            try { await _languageService.SetAsync(language); }
            catch { /* ignore storage failures in tests/ssr */ }
        }

        OnLanguageChanged?.Invoke();
    }

    // ---------------------------------------------------------------- dates

    private static readonly string[] ArabicDays = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];
    private static readonly string[] EnglishDays = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
    private static readonly string[] GermanDays = ["Sonntag", "Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag"];

    private static readonly string[] ArabicShortDays = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];
    private static readonly string[] EnglishShortDays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
    private static readonly string[] GermanShortDays = ["So", "Mo", "Di", "Mi", "Do", "Fr", "Sa"];

    private static readonly string[] ArabicMonths = ["كانون الثاني", "شباط", "آذار", "نيسان", "أيار", "حزيران", "تموز", "آب", "أيلول", "تشرين الأول", "تشرين الثاني", "كانون الأول"];
    private static readonly string[] EnglishMonths = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    private static readonly string[] GermanMonths = ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"];

    private static readonly string[] EnglishShortMonths = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    private static readonly string[] GermanShortMonths = ["Jan.", "Feb.", "März", "Apr.", "Mai", "Juni", "Juli", "Aug.", "Sep.", "Okt.", "Nov.", "Dez."];

    private static CultureInfo CultureFor(string code) => code switch
    {
        "ar" => ArabicCulture,
        "de" => GermanCulture,
        _ => EnglishCulture
    };

    private static readonly CultureInfo ArabicCulture = CultureInfo.GetCultureInfo("ar-EG");
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

    private string[] Days => _currentLanguage switch { "ar" => ArabicDays, "de" => GermanDays, _ => EnglishDays };
    private string[] ShortDays => _currentLanguage switch { "ar" => ArabicShortDays, "de" => GermanShortDays, _ => EnglishShortDays };
    private string[] Months => _currentLanguage switch { "ar" => ArabicMonths, "de" => GermanMonths, _ => EnglishMonths };
    private string[] ShortMonths => _currentLanguage switch { "ar" => ArabicMonths, "de" => GermanShortMonths, _ => EnglishShortMonths };

    public string GetDayName(DayOfWeek day) => Days[(int)day];
    public string GetDayName(DateOnly date) => GetDayName(date.DayOfWeek);
    public string GetShortDayName(DayOfWeek day) => ShortDays[(int)day];

    public string GetDayAbbr(DayOfWeek day, int length = 3)
    {
        var name = _currentLanguage == "ar" ? ArabicDays[(int)day] : ShortDays[(int)day];
        // Arabic day names all start with the article "ال"; the letters after it are what tell days apart.
        if (_currentLanguage == "ar" && name.StartsWith("ال")) name = name[2..];
        return name.Length <= length ? name : name[..length];
    }
    public string GetMonthName(int month) => Months[Math.Clamp(month, 1, 12) - 1];

    public string FormatDayDate(DateOnly date) => _currentLanguage switch
    {
        "ar" => $"{ArabicDays[(int)date.DayOfWeek]} {date.Day} {ArabicMonths[date.Month - 1]}",
        "de" => $"{GermanDays[(int)date.DayOfWeek]}, {date.Day}. {GermanShortMonths[date.Month - 1]}",
        _ => $"{EnglishDays[(int)date.DayOfWeek]}, {EnglishShortMonths[date.Month - 1]} {date.Day}"
    };

    public string FormatMonthYear(DateOnly date) => $"{GetMonthName(date.Month)} {date.Year}";

    public string GetMealTypeName(MealType type) => type switch
    {
        MealType.Meat => this["meal_meat"],
        MealType.Chicken => this["meal_chicken"],
        MealType.Fish => this["meal_fish"],
        MealType.Vegetarian => this["meal_vegetarian"],
        _ => type.ToString()
    };

    // ---------------------------------------------------------- translations

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        // Brand & shared
        ["app_title"] = new() { ["ar"] = "مكدوس", ["en"] = "Makdous", ["de"] = "Makdous" },
        ["app_subtitle"] = new() { ["ar"] = "مخطط وجبات شخصي", ["en"] = "Personal Weekly Meal Planner", ["de"] = "Dein persönlicher Wochen-Essensplaner" },
        ["loading"] = new() { ["ar"] = "جارِ التحميل...", ["en"] = "Loading...", ["de"] = "Wird geladen …" },
        ["save"] = new() { ["ar"] = "حفظ", ["en"] = "Save", ["de"] = "Speichern" },
        ["cancel"] = new() { ["ar"] = "إلغاء", ["en"] = "Cancel", ["de"] = "Abbrechen" },
        ["close"] = new() { ["ar"] = "إغلاق", ["en"] = "Close", ["de"] = "Schließen" },
        ["delete"] = new() { ["ar"] = "حذف", ["en"] = "Delete", ["de"] = "Löschen" },
        ["edit"] = new() { ["ar"] = "تعديل", ["en"] = "Edit", ["de"] = "Bearbeiten" },
        ["copy"] = new() { ["ar"] = "نسخ", ["en"] = "Copy", ["de"] = "Kopieren" },
        ["add"] = new() { ["ar"] = "إضافة", ["en"] = "Add", ["de"] = "Hinzufügen" },
        ["back"] = new() { ["ar"] = "السابق", ["en"] = "Back", ["de"] = "Zurück" },
        ["next"] = new() { ["ar"] = "التالي", ["en"] = "Next", ["de"] = "Weiter" },
        ["confirm"] = new() { ["ar"] = "تأكيد", ["en"] = "Confirm", ["de"] = "Bestätigen" },
        ["done"] = new() { ["ar"] = "تم ✓", ["en"] = "Done ✓", ["de"] = "Fertig ✓" },
        ["search"] = new() { ["ar"] = "بحث", ["en"] = "Search", ["de"] = "Suchen" },
        ["badge_favorite"] = new() { ["ar"] = "مفضلة", ["en"] = "Favorite", ["de"] = "Favorit" },

        // Nav
        ["nav_week"] = new() { ["ar"] = "الأسبوع", ["en"] = "Week", ["de"] = "Woche" },
        ["nav_meals"] = new() { ["ar"] = "الوجبات", ["en"] = "Meals", ["de"] = "Gerichte" },
        ["nav_shopping"] = new() { ["ar"] = "المشتريات", ["en"] = "Shopping", ["de"] = "Einkauf" },
        ["nav_friends"] = new() { ["ar"] = "الأصدقاء", ["en"] = "Friends", ["de"] = "Freunde" },
        ["nav_assistant"] = new() { ["ar"] = "المساعد", ["en"] = "Assistant", ["de"] = "Assistent" },
        // {0} = number of pending friend requests
        ["nav_friends_requests"] = new() { ["ar"] = "{0} طلبات صداقة", ["en"] = "{0} friend requests", ["de"] = "{0} Freundschaftsanfragen" },
        ["nav_settings"] = new() { ["ar"] = "الإعدادات", ["en"] = "Settings", ["de"] = "Einstellungen" },
        ["ai_plan_nav_label"] = new() { ["ar"] = "المساعد الذكي", ["en"] = "AI Assistant", ["de"] = "KI-Assistent" },

        // Settings
        ["settings_title"] = new() { ["ar"] = "الإعدادات", ["en"] = "Settings", ["de"] = "Einstellungen" },
        ["settings_subtitle"] = new() { ["ar"] = "كل ما تحتاجه في مكان واحد", ["en"] = "Everything you need in one place", ["de"] = "Alles an einem Ort" },
        ["settings_theme_title"] = new() { ["ar"] = "المظهر", ["en"] = "Appearance", ["de"] = "Darstellung" },
        ["settings_theme_system"] = new() { ["ar"] = "تلقائي", ["en"] = "Auto", ["de"] = "Automatisch" },
        ["settings_theme_light"] = new() { ["ar"] = "فاتح", ["en"] = "Light", ["de"] = "Hell" },
        ["settings_theme_dark"] = new() { ["ar"] = "داكن", ["en"] = "Dark", ["de"] = "Dunkel" },
        ["settings_advanced"] = new() { ["ar"] = "متقدم", ["en"] = "Advanced", ["de"] = "Erweitert" },
        ["settings_advanced_hint"] = new() { ["ar"] = "مفتاح Gemini، النموذج، حول التطبيق والإصدار.", ["en"] = "Gemini key, model, About and version.", ["de"] = "Gemini-Schlüssel, Modell, Info und Version." },
        ["settings_lang_title"] = new() { ["ar"] = "لغة التطبيق", ["en"] = "App Language", ["de"] = "App-Sprache" },
        ["settings_lang_desc"] = new() { ["ar"] = "اختر لغة الواجهة والاتجاه المفضل لديك.", ["en"] = "Choose your preferred interface language and direction.", ["de"] = "Wähle deine bevorzugte Sprache und Schreibrichtung." },
        ["lang_ar"] = new() { ["ar"] = "العربية", ["en"] = "العربية", ["de"] = "العربية" },
        ["lang_en"] = new() { ["ar"] = "English", ["en"] = "English", ["de"] = "English" },
        ["lang_de"] = new() { ["ar"] = "Deutsch", ["en"] = "Deutsch", ["de"] = "Deutsch" },
        ["settings_username_title"] = new() { ["ar"] = "اسم المستخدم", ["en"] = "Username", ["de"] = "Benutzername" },
        ["settings_username_prompt"] = new() { ["ar"] = "اختر اسماً قبل إرسال أو قبول طلبات الصداقة.", ["en"] = "Choose a name before sending or accepting friend requests.", ["de"] = "Wähle einen Namen, bevor du Freundschaftsanfragen sendest oder annimmst." },
        ["settings_username_change"] = new() { ["ar"] = "تغيير اسم المستخدم", ["en"] = "Change username", ["de"] = "Benutzernamen ändern" },
        ["settings_username_choose"] = new() { ["ar"] = "اختر اسم مستخدم", ["en"] = "Choose username", ["de"] = "Benutzernamen wählen" },
        ["settings_username_placeholder"] = new() { ["ar"] = "مثال: rami_food", ["en"] = "e.g. rami_food", ["de"] = "z. B. rami_food" },
        ["settings_username_available"] = new() { ["ar"] = "اسم المستخدم متاح ✓", ["en"] = "Username is available ✓", ["de"] = "Benutzername ist frei ✓" },
        ["settings_username_taken"] = new() { ["ar"] = "اسم المستخدم مستخدم بالفعل.", ["en"] = "Username is already taken.", ["de"] = "Dieser Benutzername ist bereits vergeben." },
        ["settings_username_saved"] = new() { ["ar"] = "تم حفظ اسم المستخدم ✨", ["en"] = "Username saved successfully ✨", ["de"] = "Benutzername gespeichert ✨" },
        ["settings_username_copied"] = new() { ["ar"] = "تم نسخ اسم المستخدم.", ["en"] = "Username copied.", ["de"] = "Benutzername kopiert." },
        ["settings_recover_title"] = new() { ["ar"] = "استرجاع خطتي", ["en"] = "Recover My Plan", ["de"] = "Plan wiederherstellen" },
        ["settings_recover_desc"] = new() { ["ar"] = "هل مسحت بيانات المتصفح أو غيّرت جهازك؟ استعد خططك ووجباتك باسم المستخدم الذي اخترته سابقاً.", ["en"] = "Cleared browser data or changed devices? Recover your plan and meals with your username.", ["de"] = "Browserdaten gelöscht oder Gerät gewechselt? Hol dir deinen Plan mit deinem Benutzernamen zurück." },
        ["settings_recover_link"] = new() { ["ar"] = "استرجاع باسم المستخدم ←", ["en"] = "Recover by username →", ["de"] = "Mit Benutzernamen wiederherstellen →" },
        ["settings_install_title"] = new() { ["ar"] = "تثبيت على الشاشة الرئيسية", ["en"] = "Install on Home Screen", ["de"] = "Zum Startbildschirm hinzufügen" },
        ["settings_install_desc"] = new() { ["ar"] = "أضف مكدوس إلى شاشة هاتفك الرئيسية لفتحه كتطبيق سريع على أندرويد وiPhone.", ["en"] = "Add Makdous to your home screen for quick access on Android & iPhone.", ["de"] = "Füge Makdous zu deinem Startbildschirm hinzu — schneller Zugriff auf Android und iPhone." },
        ["settings_install_link"] = new() { ["ar"] = "كيف أثبّت مكدوس؟ ←", ["en"] = "How to install Makdous? →", ["de"] = "Wie installiere ich Makdous? →" },
        ["settings_about_title"] = new() { ["ar"] = "ℹ️ حول التطبيق", ["en"] = "ℹ️ About", ["de"] = "ℹ️ Über die App" },
        ["settings_about_desc"] = new() { ["ar"] = "مكدوس — مخطط وجبات شخصي", ["en"] = "Makdous — Personal Weekly Meal Planner", ["de"] = "Makdous — dein persönlicher Wochen-Essensplaner" },
        ["settings_about_version"] = new() { ["ar"] = "الإصدار 1.5", ["en"] = "Version 1.5", ["de"] = "Version 1.5" },
        ["settings_about_dev"] = new() { ["ar"] = "الموقع الشخصي للمطور ↗", ["en"] = "Developer's Website ↗", ["de"] = "Website des Entwicklers ↗" },

        // Week page
        ["week_title"] = new() { ["ar"] = "خطة الأسبوع", ["en"] = "Weekly Plan", ["de"] = "Wochenplan" },
        ["week_month_view"] = new() { ["ar"] = "عرض الشهر", ["en"] = "Month View", ["de"] = "Monatsansicht" },
        ["week_surprise"] = new() { ["ar"] = "فاجئني", ["en"] = "Surprise Me", ["de"] = "Überrasch mich" },
        ["week_surprise_week"] = new() { ["ar"] = "فاجئني بالأسبوع", ["en"] = "Surprise My Week", ["de"] = "Überrasch meine Woche" },
        ["week_surprise_week_eyebrow"] = new() { ["ar"] = "سنملأ الأيام الفارغة فقط", ["en"] = "We'll only fill empty days", ["de"] = "Wir füllen nur die leeren Tage" },
        ["week_surprise_week_note"] = new() { ["ar"] = "لن يتم تغيير أي وجبة مخططة مسبقاً.", ["en"] = "No planned meals will be replaced.", ["de"] = "Bereits geplante Gerichte bleiben unverändert." },
        ["week_share"] = new() { ["ar"] = "مشاركة", ["en"] = "Share", ["de"] = "Teilen" },
        ["week_share_week"] = new() { ["ar"] = "شارك أسبوعك", ["en"] = "Share Week", ["de"] = "Woche teilen" },
        ["week_share_failed"] = new() { ["ar"] = "تعذرت مشاركة الأسبوع. حاول مرة أخرى.", ["en"] = "Could not share week. Please try again.", ["de"] = "Die Woche konnte nicht geteilt werden. Bitte erneut versuchen." },
        ["week_empty_meal"] = new() { ["ar"] = "لا توجد وجبة محددة", ["en"] = "No meal planned", ["de"] = "Kein Gericht geplant" },
        ["week_choose_meal"] = new() { ["ar"] = "اختر وجبة", ["en"] = "Choose Meal", ["de"] = "Gericht wählen" },
        ["week_change_meal"] = new() { ["ar"] = "تغيير", ["en"] = "Change", ["de"] = "Ändern" },
        ["week_change_meal_action"] = new() { ["ar"] = "تغيير الوجبة", ["en"] = "Change Meal", ["de"] = "Gericht ändern" },
        ["week_edit_meal"] = new() { ["ar"] = "تعديل الوجبة", ["en"] = "Edit Meal", ["de"] = "Gericht bearbeiten" },
        ["week_suggest_meal"] = new() { ["ar"] = "اقتراح وجبة", ["en"] = "Suggest Meal", ["de"] = "Gericht vorschlagen" },
        ["week_move_meal"] = new() { ["ar"] = "نقل ليوم آخر", ["en"] = "Move to another day", ["de"] = "Auf einen anderen Tag verschieben" },
        ["week_remove_from_day"] = new() { ["ar"] = "حذف من اليوم", ["en"] = "Remove from day", ["de"] = "Vom Tag entfernen" },
        ["week_clear_meal"] = new() { ["ar"] = "مسح", ["en"] = "Clear", ["de"] = "Leeren" },
        ["week_today"] = new() { ["ar"] = "اليوم", ["en"] = "Today", ["de"] = "Heute" },
        ["week_current"] = new() { ["ar"] = "هذا الأسبوع", ["en"] = "This Week", ["de"] = "Diese Woche" },
        // {0} = weekday name
        ["week_today_named"] = new() { ["ar"] = "اليوم · {0}", ["en"] = "Today · {0}", ["de"] = "Heute · {0}" },
        ["week_more"] = new() { ["ar"] = "المزيد", ["en"] = "More", ["de"] = "Mehr" },
        ["week_add_meal"] = new() { ["ar"] = "أضف وجبة", ["en"] = "Add a meal", ["de"] = "Gericht hinzufügen" },
        // {0} = the past weekdays, already joined
        ["week_past_days"] = new() { ["ar"] = "أيام مضت · {0}", ["en"] = "Past days · {0}", ["de"] = "Vergangene Tage · {0}" },
        // joins a list of names: "Mon, Tue" / "الاثنين والثلاثاء"
        ["list_and"] = new() { ["ar"] = " و", ["en"] = ", ", ["de"] = ", " },
        ["meal_remove_favorite_short"] = new() { ["ar"] = "إزالة من المفضلة", ["en"] = "Remove from favorites", ["de"] = "Aus Favoriten entfernen" },
        ["week_prev"] = new() { ["ar"] = "الأسبوع السابق", ["en"] = "Previous Week", ["de"] = "Vorige Woche" },
        ["week_next"] = new() { ["ar"] = "الأسبوع القادم", ["en"] = "Next Week", ["de"] = "Nächste Woche" },
        ["week_nudge_prompt"] = new() { ["ar"] = "اختر اسم مستخدم لحفظ خطتك بشكل دائم ومشاركتها مع أصدقائك", ["en"] = "Choose a username to permanently save your plan and share with friends", ["de"] = "Wähle einen Benutzernamen, um deinen Plan dauerhaft zu speichern und zu teilen" },

        // Month page
        ["month_back_settings"] = new() { ["ar"] = "العودة إلى الإعدادات", ["en"] = "Back to Settings", ["de"] = "Zurück zu den Einstellungen" },
        ["month_prev"] = new() { ["ar"] = "الشهر السابق", ["en"] = "Previous Month", ["de"] = "Voriger Monat" },
        ["month_next"] = new() { ["ar"] = "الشهر القادم", ["en"] = "Next Month", ["de"] = "Nächster Monat" },
        ["month_seg_week"] = new() { ["ar"] = "أسبوع", ["en"] = "Week", ["de"] = "Woche" },
        ["month_seg_month"] = new() { ["ar"] = "شهر", ["en"] = "Month", ["de"] = "Monat" },
        ["month_today"] = new() { ["ar"] = "اليوم", ["en"] = "today", ["de"] = "heute" },
        ["month_open_details"] = new() { ["ar"] = "فتح التفاصيل", ["en"] = "Open details", ["de"] = "Details öffnen" },

        // Meals page
        ["meals_title"] = new() { ["ar"] = "الوجبات", ["en"] = "Meals", ["de"] = "Gerichte" },
        ["meals_subtitle"] = new() { ["ar"] = "تصفح الوجبات، احفظ المفضلة، واختر يوماً عند الاستخدام.", ["en"] = "Browse meals, save favorites, and pick a day to add.", ["de"] = "Gerichte durchstöbern, Favoriten speichern und einen Tag auswählen." },
        ["meals_search_placeholder"] = new() { ["ar"] = "ابحث عن وجبة أو مكون...", ["en"] = "Search meals or ingredients...", ["de"] = "Gericht oder Zutat suchen …" },
        ["meals_filter_all"] = new() { ["ar"] = "الكل", ["en"] = "All", ["de"] = "Alle" },
        ["meals_filter_favs"] = new() { ["ar"] = "المفضلة", ["en"] = "Favorites", ["de"] = "Favoriten" },
        ["meals_type_label"] = new() { ["ar"] = "النوع:", ["en"] = "Type:", ["de"] = "Art:" },
        ["meal_meat"] = new() { ["ar"] = "لحوم", ["en"] = "Meat", ["de"] = "Fleisch" },
        ["meal_chicken"] = new() { ["ar"] = "دواجن", ["en"] = "Chicken", ["de"] = "Geflügel" },
        ["meal_fish"] = new() { ["ar"] = "مأكولات بحرية", ["en"] = "Fish & Seafood", ["de"] = "Fisch & Meeresfrüchte" },
        ["meal_vegetarian"] = new() { ["ar"] = "نباتي", ["en"] = "Vegetarian", ["de"] = "Vegetarisch" },
        // {0} = number of meals shown
        ["meals_results_count"] = new() { ["ar"] = "{0} وجبة", ["en"] = "{0} meals", ["de"] = "{0} Gerichte" },
        ["meals_results_one"] = new() { ["ar"] = "وجبة واحدة", ["en"] = "1 meal", ["de"] = "1 Gericht" },
        ["meals_clear_filters"] = new() { ["ar"] = "مسح الفلاتر", ["en"] = "Clear filters", ["de"] = "Filter zurücksetzen" },
        ["meals_empty_title"] = new() { ["ar"] = "لم يتم العثور على وجبات", ["en"] = "No meals found", ["de"] = "Keine Gerichte gefunden" },
        ["meals_empty_desc"] = new() { ["ar"] = "جرّب تغيير كلمات البحث أو الفلاتر", ["en"] = "Try changing your search terms or filters", ["de"] = "Ändere deine Suchbegriffe oder Filter" },
        ["meals_add_custom"] = new() { ["ar"] = "إضافة وجبة جديدة", ["en"] = "Add New Meal", ["de"] = "Neues Gericht anlegen" },
        ["meals_new_meal"] = new() { ["ar"] = "وجبة جديدة", ["en"] = "New Meal", ["de"] = "Neues Gericht" },
        ["meals_new_meal_eyebrow"] = new() { ["ar"] = "أضفها مرة واحدة لتصبح متاحة للجميع", ["en"] = "Add it once to make it available to all", ["de"] = "Einmal anlegen — für alle verfügbar" },
        ["meals_lang_all"] = new() { ["ar"] = "جميع اللغات", ["en"] = "All Languages", ["de"] = "Alle Sprachen" },
        ["meals_filter"] = new() { ["ar"] = "تصفية", ["en"] = "Filter", ["de"] = "Filter" },
        ["meals_language_filter"] = new() { ["ar"] = "لغة الوجبات", ["en"] = "Meal language", ["de"] = "Sprache der Gerichte" },
        // {0} = day, e.g. "الجمعة 9"
        ["meals_pick_for_day"] = new() { ["ar"] = "اختر وجبة ليوم {0}", ["en"] = "Pick a meal for {0}", ["de"] = "Gericht für {0} wählen" },
        // {0} = day
        ["meals_add_to_named_day"] = new() { ["ar"] = "أضف إلى {0}", ["en"] = "Add to {0}", ["de"] = "Zu {0} hinzufügen" },
        ["meals_add_to_day_short"] = new() { ["ar"] = "أضف إلى يوم", ["en"] = "Add to a day", ["de"] = "Zu einem Tag hinzufügen" },
        ["done_action"] = new() { ["ar"] = "تم", ["en"] = "Done", ["de"] = "Fertig" },
        ["meals_lang_app"] = new() { ["ar"] = "وجبات باللغة الحالية", ["en"] = "Current Language Only", ["de"] = "Nur aktuelle Sprache" },
        ["meals_meal_name"] = new() { ["ar"] = "اسم الوجبة", ["en"] = "Meal name", ["de"] = "Name des Gerichts" },
        ["meals_meal_type"] = new() { ["ar"] = "نوع الوجبة", ["en"] = "Meal type", ["de"] = "Art des Gerichts" },
        ["meals_name_required"] = new() { ["ar"] = "أدخل اسم الوجبة.", ["en"] = "Enter meal name.", ["de"] = "Bitte einen Namen eingeben." },
        ["meals_save_and_select"] = new() { ["ar"] = "حفظ واختيار الوجبة", ["en"] = "Save and Select Meal", ["de"] = "Speichern und auswählen" },
        ["meals_edit_meal"] = new() { ["ar"] = "تعديل الوجبة", ["en"] = "Edit Meal", ["de"] = "Gericht bearbeiten" },
        ["meals_edit_eyebrow"] = new() { ["ar"] = "عدّل التفاصيل أو استخدمها في خطتك", ["en"] = "Edit details or use it in your plan", ["de"] = "Details ändern oder ins Wochenmenü übernehmen" },
        ["meals_save_changes"] = new() { ["ar"] = "حفظ التعديلات", ["en"] = "Save Changes", ["de"] = "Änderungen speichern" },
        ["meals_use_meal"] = new() { ["ar"] = "استخدم هذه الوجبة", ["en"] = "Use this meal", ["de"] = "Dieses Gericht verwenden" },
        ["meals_hide_meal"] = new() { ["ar"] = "إخفاء الوجبة من المكتبة", ["en"] = "Hide meal from library", ["de"] = "Gericht aus der Bibliothek ausblenden" },
        ["meals_meal_updated"] = new() { ["ar"] = "تم حفظ تعديلات الوجبة ✨", ["en"] = "Meal updated successfully ✨", ["de"] = "Gericht aktualisiert ✨" },
        ["meals_meal_hidden"] = new() { ["ar"] = "تم إخفاء الوجبة من المكتبة.", ["en"] = "Meal hidden from library.", ["de"] = "Gericht wurde ausgeblendet." },
        ["meals_add_to_day"] = new() { ["ar"] = "أضف هذه الوجبة لأي يوم؟", ["en"] = "Add this meal to which day?", ["de"] = "An welchem Tag soll es gekocht werden?" },
        // {0} = meal name, {1} = day name
        ["meals_added_toast"] = new() { ["ar"] = "تمت إضافة {0} إلى {1}", ["en"] = "Added {0} to {1}", ["de"] = "{0} für {1} eingeplant" },
        // {0} = error message
        ["meals_assign_error"] = new() { ["ar"] = "حدث خطأ أثناء الإضافة: {0}", ["en"] = "Error assigning meal: {0}", ["de"] = "Fehler beim Einplanen: {0}" },

        // Meal card / details
        ["meal_ingredients"] = new() { ["ar"] = "المكونات", ["en"] = "Ingredients", ["de"] = "Zutaten" },
        ["meal_view_ingredients"] = new() { ["ar"] = "عرض المكونات", ["en"] = "View ingredients", ["de"] = "Zutaten ansehen" },
        ["meal_side_dishes"] = new() { ["ar"] = "أطباق جانبية", ["en"] = "Side Dishes", ["de"] = "Beilagen" },
        ["meal_select_for_day"] = new() { ["ar"] = "اختيار لهذا اليوم", ["en"] = "Select for this day", ["de"] = "Für diesen Tag wählen" },
        ["meal_select_this"] = new() { ["ar"] = "✓ اختر هذه الوجبة", ["en"] = "✓ Select This Meal", ["de"] = "✓ Dieses Gericht wählen" },
        ["meal_use_this"] = new() { ["ar"] = "استخدام هذه الوجبة", ["en"] = "Use This Meal", ["de"] = "Dieses Gericht verwenden" },
        ["meal_add_to_plan"] = new() { ["ar"] = "إضافة إلى الخطة", ["en"] = "Add to Plan", ["de"] = "Zum Plan hinzufügen" },
        ["meal_favorite"] = new() { ["ar"] = "المفضلة", ["en"] = "Favorite", ["de"] = "Favorit" },
        ["meal_add_favorite"] = new() { ["ar"] = "☆ إضافة للمفضلة", ["en"] = "☆ Add to Favorites", ["de"] = "☆ Zu Favoriten hinzufügen" },
        ["meal_remove_favorite"] = new() { ["ar"] = "⭐ إزالة من المفضلة", ["en"] = "⭐ Remove from Favorites", ["de"] = "⭐ Aus Favoriten entfernen" },
        ["meal_no_ingredients"] = new() { ["ar"] = "لم يتم تحديد مكونات بعد لهذه الوجبة.", ["en"] = "No ingredients specified yet.", ["de"] = "Für dieses Gericht sind noch keine Zutaten hinterlegt." },
        ["meal_no_side_dishes"] = new() { ["ar"] = "لم يتم اختيار أطباق جانبية بعد.", ["en"] = "No side dishes specified yet.", ["de"] = "Noch keine Beilagen ausgewählt." },
        ["meal_ingredients_hint"] = new() { ["ar"] = "أضف للمشتريات بلمسة", ["en"] = "Add to shopping with a tap", ["de"] = "Mit einem Tipp zum Einkauf" },
        ["meal_on_list"] = new() { ["ar"] = "على قائمتك", ["en"] = "On your list", ["de"] = "Auf deiner Liste" },
        // {0} = ingredient
        ["meal_add_ingredient_to_list"] = new() { ["ar"] = "أضف {0} إلى المشتريات", ["en"] = "Add {0} to shopping", ["de"] = "{0} zum Einkauf hinzufügen" },
        // {0} = ingredient, {1} = shopping list name
        ["meal_ingredient_added"] = new() { ["ar"] = "أُضيف {0} إلى {1}", ["en"] = "Added {0} to {1}", ["de"] = "{0} zu {1} hinzugefügt" },
        ["meal_serves_with"] = new() { ["ar"] = "يُقدَّم مع", ["en"] = "Serves with", ["de"] = "Dazu passt" },
        ["meal_add_to_day_label"] = new() { ["ar"] = "أضف إلى يوم", ["en"] = "Add to a day", ["de"] = "Zu einem Tag hinzufügen" },
        // {0} = day, e.g. "الأربعاء 14"
        ["meal_add_to_named_day_cta"] = new() { ["ar"] = "أضف إلى {0}", ["en"] = "Add to {0}", ["de"] = "Zu {0} hinzufügen" },
        ["meal_add_photo"] = new() { ["ar"] = "إضافة صورة", ["en"] = "Add photo", ["de"] = "Foto hinzufügen" },
        ["meal_change_photo"] = new() { ["ar"] = "تغيير الصورة", ["en"] = "Change photo", ["de"] = "Foto ändern" },
        ["meal_remove_photo"] = new() { ["ar"] = "حذف الصورة", ["en"] = "Remove photo", ["de"] = "Foto entfernen" },
        ["meal_photo_failed"] = new() { ["ar"] = "تعذّر حفظ الصورة.", ["en"] = "Couldn't save the photo.", ["de"] = "Das Foto konnte nicht gespeichert werden." },
        ["meal_photo_private"] = new() { ["ar"] = "الصورة تظهر لك فقط", ["en"] = "Only you see this photo", ["de"] = "Nur du siehst dieses Foto" },
        ["meal_edit"] = new() { ["ar"] = "تعديل", ["en"] = "Edit", ["de"] = "Bearbeiten" },
        ["meal_close"] = new() { ["ar"] = "إغلاق", ["en"] = "Close", ["de"] = "Schließen" },
        ["meal_not_found"] = new() { ["ar"] = "لم يتم العثور على تفاصيل الوجبة.", ["en"] = "Meal details not found.", ["de"] = "Details zum Gericht nicht gefunden." },
        ["meal_back_home"] = new() { ["ar"] = "العودة للرئيسية", ["en"] = "Back to Home", ["de"] = "Zurück zur Startseite" },

        // Selected day header
        ["selectedday_add_to"] = new() { ["ar"] = "إضافة وجبة إلى", ["en"] = "Add meal to", ["de"] = "Gericht hinzufügen für" },

        // Shopping
        ["shopping_add_ingredients"] = new() { ["ar"] = "أضف المكونات إلى قائمة التسوق", ["en"] = "Add ingredients to shopping list", ["de"] = "Zutaten auf die Einkaufsliste" },
        // {0} = number of ingredients added, {1} = shopping list name
        ["shopping_ingredients_added"] = new() { ["ar"] = "تمت إضافة {0} مكوّنات إلى {1}", ["en"] = "Added {0} ingredients to {1}", ["de"] = "{0} Zutaten zu {1} hinzugefügt" },
        ["shopping_ingredients_present"] = new() { ["ar"] = "كل المكونات موجودة في قائمتك بالفعل", ["en"] = "All ingredients are already on your list", ["de"] = "Alle Zutaten stehen schon auf deiner Liste" },
        ["shopping_title"] = new() { ["ar"] = "المشتريات", ["en"] = "Shopping", ["de"] = "Einkauf" },
        ["shopping_subtitle"] = new() { ["ar"] = "رتّب مشترياتك حسب المتجر", ["en"] = "Organize your shopping by store", ["de"] = "Sortiere deinen Einkauf nach Geschäft" },
        ["shopping_add_store"] = new() { ["ar"] = "أضف متجرًا", ["en"] = "Add Store", ["de"] = "Geschäft hinzufügen" },
        ["shopping_loading"] = new() { ["ar"] = "جارٍ تحميل قائمة المشتريات…", ["en"] = "Loading shopping list…", ["de"] = "Einkaufsliste wird geladen …" },
        ["shopping_empty_title"] = new() { ["ar"] = "قائمة المشتريات جاهزة", ["en"] = "Shopping list is ready", ["de"] = "Deine Einkaufsliste ist bereit" },
        ["shopping_empty_desc"] = new() { ["ar"] = "أضف متجرًا ثم دوّن ما تحتاجه منه.", ["en"] = "Add a store and list what you need.", ["de"] = "Füge ein Geschäft hinzu und notiere, was du brauchst." },
        ["shopping_add_first_store"] = new() { ["ar"] = "أضف أول متجر", ["en"] = "Add First Store", ["de"] = "Erstes Geschäft hinzufügen" },
        ["shopping_items"] = new() { ["ar"] = "عنصر", ["en"] = "items", ["de"] = "Artikel" },
        ["shopping_delete_store"] = new() { ["ar"] = "حذف المتجر", ["en"] = "Delete Store", ["de"] = "Geschäft löschen" },
        // {0} = store name
        ["shopping_delete_store_named"] = new() { ["ar"] = "حذف {0}", ["en"] = "Delete {0}", ["de"] = "{0} löschen" },
        // {0} = store name
        ["shopping_add_item_to"] = new() { ["ar"] = "أضف عنصرًا إلى {0}", ["en"] = "Add item to {0}", ["de"] = "Artikel zu {0} hinzufügen" },
        ["shopping_add_item"] = new() { ["ar"] = "أضف عنصرًا", ["en"] = "Add item", ["de"] = "Artikel hinzufügen" },
        ["shopping_no_items"] = new() { ["ar"] = "لا توجد عناصر بعد — أضف أول عنصر.", ["en"] = "No items yet — add your first item.", ["de"] = "Noch keine Artikel — füge den ersten hinzu." },
        ["shopping_bought"] = new() { ["ar"] = "تم الشراء", ["en"] = "Bought", ["de"] = "Gekauft" },
        ["shopping_uncheck"] = new() { ["ar"] = "إلغاء شراء", ["en"] = "Uncheck", ["de"] = "Abwählen" },
        ["shopping_new_store"] = new() { ["ar"] = "متجر جديد", ["en"] = "New Store", ["de"] = "Neues Geschäft" },
        ["shopping_new_store_eyebrow"] = new() { ["ar"] = "رتّب مشترياتك بالطريقة التي تناسبك", ["en"] = "Organize your shopping the way you like", ["de"] = "Ordne deinen Einkauf so, wie es dir passt" },
        ["shopping_store_placeholder"] = new() { ["ar"] = "اسم المتجر، مثل كارفور أو بنده", ["en"] = "Store name, e.g. Walmart", ["de"] = "Name des Geschäfts, z. B. REWE" },
        ["shopping_delete_store_q"] = new() { ["ar"] = "حذف المتجر؟", ["en"] = "Delete Store?", ["de"] = "Geschäft löschen?" },
        ["shopping_delete_store_eyebrow"] = new() { ["ar"] = "سيُحذف المتجر وكل العناصر الموجودة فيه.", ["en"] = "The store and all its items will be deleted.", ["de"] = "Das Geschäft und alle Artikel darin werden gelöscht." },
        // {0} = items left to buy, {1} = number of stores
        ["shopping_summary"] = new() { ["ar"] = "{0} متبقية في {1} متاجر", ["en"] = "{0} left to buy in {1} stores", ["de"] = "{0} offen in {1} Geschäften" },
        ["shopping_summary_one"] = new() { ["ar"] = "{0} متبقية للشراء", ["en"] = "{0} left to buy", ["de"] = "{0} noch zu kaufen" },
        ["shopping_summary_empty"] = new() { ["ar"] = "لا شيء للشراء", ["en"] = "Nothing left to buy", ["de"] = "Nichts mehr zu kaufen" },
        ["shopping_add_week_ingredients"] = new() { ["ar"] = "أضف مكونات وجبات هذا الأسبوع", ["en"] = "Add this week's ingredients", ["de"] = "Zutaten dieser Woche hinzufügen" },
        ["shopping_by_store"] = new() { ["ar"] = "حسب المتجر", ["en"] = "By store", ["de"] = "Nach Geschäft" },
        ["shopping_by_aisle"] = new() { ["ar"] = "حسب القسم", ["en"] = "By aisle", ["de"] = "Nach Abteilung" },
        // {0} = bought count, {1} = total
        ["shopping_progress"] = new() { ["ar"] = "{0} من {1}", ["en"] = "{0} of {1}", ["de"] = "{0} von {1}" },
        // {0} = bought count
        ["shopping_bought_n"] = new() { ["ar"] = "تم شراؤها ({0})", ["en"] = "Bought ({0})", ["de"] = "Gekauft ({0})" },
        ["shopping_store_options"] = new() { ["ar"] = "خيارات المتجر", ["en"] = "Store options", ["de"] = "Geschäftsoptionen" },
        ["shopping_expand"] = new() { ["ar"] = "توسيع", ["en"] = "Expand", ["de"] = "Aufklappen" },
        ["shopping_item_placeholder"] = new() { ["ar"] = "اسم العنصر", ["en"] = "Item name", ["de"] = "Artikelname" },
        ["shopping_which_store"] = new() { ["ar"] = "أي متجر؟", ["en"] = "Which store?", ["de"] = "Welches Geschäft?" },
        ["shopping_item_name"] = new() { ["ar"] = "اسم العنصر", ["en"] = "Item name", ["de"] = "Artikelname" },
        ["shopping_quantity"] = new() { ["ar"] = "الكمية", ["en"] = "Quantity", ["de"] = "Menge" },
        ["shopping_unit"] = new() { ["ar"] = "الوحدة", ["en"] = "Unit", ["de"] = "Einheit" },
        ["shopping_unit_placeholder"] = new() { ["ar"] = "الوحدة (كغ، لتر، حزمة…)", ["en"] = "Unit (kg, l, bunch…)", ["de"] = "Einheit (kg, l, Bund …)" },
        ["shopping_increase"] = new() { ["ar"] = "زيادة", ["en"] = "Increase", ["de"] = "Mehr" },
        ["shopping_decrease"] = new() { ["ar"] = "إنقاص", ["en"] = "Decrease", ["de"] = "Weniger" },
        // {0} = meal name
        ["shopping_for_meal"] = new() { ["ar"] = "لـ{0}", ["en"] = "for {0}", ["de"] = "für {0}" },
        ["shopping_week_no_meals"] = new() { ["ar"] = "لا توجد وجبات مخططة لبقية هذا الأسبوع.", ["en"] = "No meals planned for the rest of this week.", ["de"] = "Für den Rest der Woche sind keine Gerichte geplant." },
        ["shopping_week_nothing_new"] = new() { ["ar"] = "كل المكونات موجودة في قوائمك بالفعل.", ["en"] = "Every ingredient is already on your lists.", ["de"] = "Alle Zutaten stehen schon auf deinen Listen." },
        // {0} = number added, {1} = store name
        ["shopping_week_added"] = new() { ["ar"] = "أُضيفت {0} مكونات إلى {1}", ["en"] = "Added {0} ingredients to {1}", ["de"] = "{0} Zutaten zu {1} hinzugefügt" },
        ["aisle_produce"] = new() { ["ar"] = "خضار وفواكه", ["en"] = "Produce", ["de"] = "Obst & Gemüse" },
        ["aisle_bakery"] = new() { ["ar"] = "مخبوزات", ["en"] = "Bakery", ["de"] = "Backwaren" },
        ["aisle_dairy"] = new() { ["ar"] = "ألبان وبيض", ["en"] = "Dairy & eggs", ["de"] = "Milch & Eier" },
        ["aisle_meat"] = new() { ["ar"] = "لحوم ودواجن وأسماك", ["en"] = "Meat & fish", ["de"] = "Fleisch & Fisch" },
        ["aisle_pantry"] = new() { ["ar"] = "مواد جافة", ["en"] = "Pantry", ["de"] = "Vorrat" },
        ["aisle_other"] = new() { ["ar"] = "أخرى", ["en"] = "Other", ["de"] = "Sonstiges" },

        // Friends
        ["friends_title"] = new() { ["ar"] = "الأصدقاء", ["en"] = "Friends", ["de"] = "Freunde" },
        ["friends_subtitle"] = new() { ["ar"] = "خطّطوا أسبوعكم معاً", ["en"] = "Plan your week together", ["de"] = "Plant eure Woche gemeinsam" },

        // Recover
        ["recover_subtitle"] = new() { ["ar"] = "أعد فتح خطتك باسم المستخدم بعد مسح بيانات المتصفح أو على جهاز جديد", ["en"] = "Restore your plan with your username after clearing browser data or on a new device", ["de"] = "Stelle deinen Plan mit deinem Benutzernamen wieder her — nach gelöschten Browserdaten oder auf einem neuen Gerät" },
        ["recover_intro"] = new() { ["ar"] = "إذا اخترت اسم مستخدم سابقاً ثم حُذفت بيانات المتصفح (أو أضفت التطبيق من جديد)، يمكنك استعادة كل خططك ووجباتك بكتابة اسم المستخدم نفسه الذي اخترته سابقاً.", ["en"] = "If you previously set a username and browser data was cleared, you can recover all your plans and meals by typing the same username.", ["de"] = "Wenn du früher einen Benutzernamen gewählt hast und die Browserdaten gelöscht wurden, kannst du alle Pläne und Gerichte mit demselben Benutzernamen zurückholen." },
        ["recover_prev_username"] = new() { ["ar"] = "اسم المستخدم السابق", ["en"] = "Previous Username", ["de"] = "Bisheriger Benutzername" },
        // {0} = username
        ["recover_warning"] = new() { ["ar"] = "هذا الجهاز سيتحول الآن إلى حساب «{0}». لا يلزم إعادة تسمية أي شيء، وستُعرض كل خططك المخزّنة في السحابة.", ["en"] = "This device will switch to user '{0}'. All your cloud-saved plans will be loaded.", ["de"] = "Dieses Gerät wechselt jetzt zum Konto „{0}“. Alle in der Cloud gespeicherten Pläne werden geladen." },
        ["recover_busy"] = new() { ["ar"] = "جارٍ الاسترجاع...", ["en"] = "Recovering...", ["de"] = "Wird wiederhergestellt …" },
        ["recover_now"] = new() { ["ar"] = "استرجاع خطتي الآن", ["en"] = "Recover My Plan Now", ["de"] = "Plan jetzt wiederherstellen" },
        ["recover_back"] = new() { ["ar"] = "➔ العودة إلى الإعدادات", ["en"] = "← Back to Settings", ["de"] = "← Zurück zu den Einstellungen" },
        ["recover_already_using"] = new() { ["ar"] = "أنت تستخدم اسم المستخدم هذا بالفعل على هذا الجهاز.", ["en"] = "You are already using this username on this device.", ["de"] = "Du verwendest diesen Benutzernamen auf diesem Gerät bereits." },
        ["recover_not_found"] = new() { ["ar"] = "لم نعثر على حساب بهذا الاسم. تأكد أنك كتبت اسم المستخدم الذي اخترته سابقاً، مع العلم أن البيانات المخزّنة كضيف (بدون اسم مستخدم) لا يمكن استرجاعها.", ["en"] = "No account found with this username. Ensure you entered your previously registered username.", ["de"] = "Kein Konto mit diesem Benutzernamen gefunden. Prüfe die Schreibweise — als Gast gespeicherte Daten lassen sich nicht wiederherstellen." },

        // Install help & add-to-home carousel
        ["install_title"] = new() { ["ar"] = "تثبيت مكدوس", ["en"] = "Install Makdous", ["de"] = "Makdous installieren" },
        ["install_subtitle"] = new() { ["ar"] = "أضف مكدوس إلى شاشتك الرئيسية لفتحه بسرعة مثل أي تطبيق", ["en"] = "Add Makdous to your home screen for quick app-like access", ["de"] = "Füge Makdous zum Startbildschirm hinzu — schnell wie eine echte App" },
        ["install_works_title"] = new() { ["ar"] = "يعمل على أندرويد و iPhone/iPad", ["en"] = "Works on Android and iPhone/iPad", ["de"] = "Funktioniert auf Android und iPhone/iPad" },
        ["install_works_desc"] = new() { ["ar"] = "استخدم متصفح Safari على iPhone/iPad، أو Chrome على أندرويد، واتبع نفس الخطوات من قائمة المشاركة أو القائمة ⋮.", ["en"] = "Use Safari on iPhone/iPad or Chrome on Android, then follow the same steps from the share menu or the ⋮ menu.", ["de"] = "Nutze Safari auf iPhone/iPad oder Chrome auf Android und folge den gleichen Schritten im Teilen-Menü oder im ⋮-Menü." },
        ["install_tip"] = new() { ["ar"] = "عند التثبيت على الشاشة الرئيسية يعمل مكدوس في وضع ملء الشاشة، ويُفتح بشكل أسرع، ويمكنك استخدامه من الشاشة الرئيسية مباشرة دون كتابة العنوان.", ["en"] = "Once installed, Makdous runs full screen, starts faster, and opens straight from your home screen without typing a URL.", ["de"] = "Einmal installiert, läuft Makdous im Vollbild, startet schneller und öffnet sich direkt vom Startbildschirm — ganz ohne Adresseingabe." },
        // {0} = current step, {1} = total steps
        ["carousel_step"] = new() { ["ar"] = "الخطوة {0} من {1}", ["en"] = "Step {0} of {1}", ["de"] = "Schritt {0} von {1}" },
        ["carousel_start"] = new() { ["ar"] = "ابدأ الآن 🚀", ["en"] = "Get started 🚀", ["de"] = "Los geht's 🚀" },
        // {0} = step number
        ["carousel_badge"] = new() { ["ar"] = "الخطوة {0}", ["en"] = "Step {0}", ["de"] = "Schritt {0}" },
        ["carousel_step1"] = new() { ["ar"] = "اضغط على زر المشاركة", ["en"] = "Tap the share button", ["de"] = "Tippe auf „Teilen“" },
        ["carousel_step2"] = new() { ["ar"] = "اختر «إضافة إلى الشاشة الرئيسية»", ["en"] = "Choose \"Add to Home Screen\"", ["de"] = "Wähle „Zum Home-Bildschirm“" },
        ["carousel_step3"] = new() { ["ar"] = "اضغط على «إضافة»", ["en"] = "Tap \"Add\"", ["de"] = "Tippe auf „Hinzufügen“" },
        ["carousel_step1_alt"] = new() { ["ar"] = "اضغط على زر المشاركة لإضافة مكدوس إلى الشاشة الرئيسية", ["en"] = "Tap the share button to add Makdous to your home screen", ["de"] = "Tippe auf „Teilen“, um Makdous zum Startbildschirm hinzuzufügen" },
        ["carousel_step2_alt"] = new() { ["ar"] = "اختر إضافة إلى الشاشة الرئيسية", ["en"] = "Choose Add to Home Screen", ["de"] = "Wähle „Zum Home-Bildschirm“" },
        ["carousel_step3_alt"] = new() { ["ar"] = "اضغط على زر الإضافة للتأكيد", ["en"] = "Tap Add to confirm", ["de"] = "Tippe zur Bestätigung auf „Hinzufügen“" },

        // First-launch wizard & iOS install guide (App.razor)
        ["wizard_title"] = new() { ["ar"] = "أضف مكدوس إلى شاشتك الرئيسية", ["en"] = "Add Makdous to your home screen", ["de"] = "Füge Makdous zu deinem Startbildschirm hinzu" },
        // {0} = current step of 3
        ["wizard_steps"] = new() { ["ar"] = "اتبع الخطوات {0} من 3 لفتح التطبيق كتطبيق سريع.", ["en"] = "Follow step {0} of 3 to open it like a native app.", ["de"] = "Folge Schritt {0} von 3, um die App wie eine native App zu öffnen." },
        ["wizard_close"] = new() { ["ar"] = "إغلاق وإكمال", ["en"] = "Close and continue", ["de"] = "Schließen und fortfahren" },
        ["wizard_skip"] = new() { ["ar"] = "تخطّي الإعداد لاحقاً", ["en"] = "Skip setup for now", ["de"] = "Einrichtung später erledigen" },
        ["ios_guide_title"] = new() { ["ar"] = "أضف مكدوس إلى الشاشة الرئيسية", ["en"] = "Add Makdous to your home screen", ["de"] = "Makdous zum Startbildschirm hinzufügen" },
        ["ios_guide_desc"] = new() { ["ar"] = "اضغط زر المشاركة في Safari، ثم اختر «إضافة إلى الشاشة الرئيسية».", ["en"] = "Tap the share button in Safari, then choose \"Add to Home Screen\".", ["de"] = "Tippe in Safari auf „Teilen“ und wähle „Zum Home-Bildschirm“." },
        ["ios_guide_alt"] = new() { ["ar"] = "شرح إضافة مكدوس إلى الشاشة الرئيسية على iPhone", ["en"] = "How to add Makdous to the home screen on iPhone", ["de"] = "So fügst du Makdous auf dem iPhone zum Startbildschirm hinzu" },
        ["ios_guide_replay"] = new() { ["ar"] = "إعادة العرض", ["en"] = "Replay", ["de"] = "Nochmal abspielen" },
        ["ios_guide_gotit"] = new() { ["ar"] = "فهمت", ["en"] = "Got it", ["de"] = "Verstanden" },
        ["email_confirm_title"] = new() { ["ar"] = "أكمل تسجيل الدخول", ["en"] = "Finish signing in", ["de"] = "Anmeldung abschließen" },
        ["email_confirm_desc"] = new() { ["ar"] = "اكتب البريد الإلكتروني الذي أرسلنا إليه الرابط للتأكد من هويتك.", ["en"] = "Enter the email address we sent the link to so we can confirm it is you.", ["de"] = "Gib die E-Mail-Adresse ein, an die wir den Link geschickt haben." },
        ["email_confirm_continue"] = new() { ["ar"] = "متابعة", ["en"] = "Continue", ["de"] = "Weiter" },
        ["email_link_invalid"] = new() { ["ar"] = "رابط تسجيل الدخول غير صالح.", ["en"] = "That sign-in link is not valid.", ["de"] = "Dieser Anmeldelink ist ungültig." },
        ["guest_name"] = new() { ["ar"] = "ضيف", ["en"] = "Guest", ["de"] = "Gast" },
        ["not_found"] = new() { ["ar"] = "عذراً، لا يوجد شيء على هذا العنوان.", ["en"] = "Sorry, there's nothing at this address.", ["de"] = "Unter dieser Adresse gibt es leider nichts." },
        ["friends_discoverable"] = new() { ["ar"] = "أشخاص على مكدوس", ["en"] = "People on Makdous", ["de"] = "Leute bei Makdous" },

        // Friends page
        ["friends_loading"] = new() { ["ar"] = "جارِ تحميل الأصدقاء...", ["en"] = "Loading friends...", ["de"] = "Freunde werden geladen …" },
        ["friends_need_username"] = new() { ["ar"] = "اختر اسم مستخدم من الإعدادات أولاً لتتمكن من إرسال أو قبول طلبات الصداقة.", ["en"] = "Choose a username in Settings first so you can send and accept friend requests.", ["de"] = "Wähle zuerst in den Einstellungen einen Benutzernamen, damit du Freundschaftsanfragen senden und annehmen kannst." },
        ["friends_your_username"] = new() { ["ar"] = "اسم المستخدم الخاص بك", ["en"] = "Your username", ["de"] = "Dein Benutzername" },
        ["friends_copy_username"] = new() { ["ar"] = "نسخ اسم المستخدم", ["en"] = "Copy username", ["de"] = "Benutzernamen kopieren" },
        ["friends_add_label"] = new() { ["ar"] = "أضف صديقاً", ["en"] = "Add a friend", ["de"] = "Freund hinzufügen" },
        ["friends_username_placeholder"] = new() { ["ar"] = "اسم المستخدم", ["en"] = "Username", ["de"] = "Benutzername" },
        ["friends_send_request"] = new() { ["ar"] = "إرسال طلب صداقة", ["en"] = "Send friend request", ["de"] = "Freundschaftsanfrage senden" },
        ["friends_requests_heading"] = new() { ["ar"] = "طلبات الصداقة", ["en"] = "Friend requests", ["de"] = "Freundschaftsanfragen" },
        ["friends_accept"] = new() { ["ar"] = "قبول طلب الصداقة", ["en"] = "Accept friend request", ["de"] = "Anfrage annehmen" },
        ["friends_decline"] = new() { ["ar"] = "رفض طلب الصداقة", ["en"] = "Decline friend request", ["de"] = "Anfrage ablehnen" },
        ["friends_list_heading"] = new() { ["ar"] = "أصدقاؤك", ["en"] = "Your friends", ["de"] = "Deine Freunde" },
        ["friends_empty"] = new() { ["ar"] = "شارك اسم المستخدم أعلاه وابدأ التخطيط مع أصدقائك.", ["en"] = "Share your username above and start planning with your friends.", ["de"] = "Teile deinen Benutzernamen von oben und plane gemeinsam mit deinen Freunden." },
        ["friends_week_of"] = new() { ["ar"] = "أسبوع {0}", ["en"] = "{0}'s week", ["de"] = "Woche von {0}" },
        ["friends_discover_empty"] = new() { ["ar"] = "لا يوجد مستخدمون جدد حالياً. ادعُ من حولك ليبدأوا التخطيط أيضاً!", ["en"] = "No new users right now. Invite people around you to start planning too!", ["de"] = "Gerade keine neuen Nutzer. Lade Leute aus deinem Umfeld ein, auch zu planen!" },
        ["friends_discover_hint"] = new() { ["ar"] = "أضفهم كأصدقاء وشاهد أسبوعهم مباشرة.", ["en"] = "Add them as friends and see their week right away.", ["de"] = "Füge sie als Freunde hinzu und sieh sofort ihre Woche." },
        ["friends_add_friend"] = new() { ["ar"] = "إضافة صديق", ["en"] = "Add friend", ["de"] = "Freund hinzufügen" },
        ["friends_add_user"] = new() { ["ar"] = "إضافة {0}", ["en"] = "Add {0}", ["de"] = "{0} hinzufügen" },
        ["friends_need_username_first"] = new() { ["ar"] = "اختر اسم مستخدم أولاً من الإعدادات.", ["en"] = "Choose a username in Settings first.", ["de"] = "Wähle zuerst in den Einstellungen einen Benutzernamen." },
        ["friends_request_sent"] = new() { ["ar"] = "تم إرسال طلب الصداقة ✨", ["en"] = "Friend request sent ✨", ["de"] = "Freundschaftsanfrage gesendet ✨" },
        ["friends_request_sent_to"] = new() { ["ar"] = "تم إرسال طلب الصداقة إلى @{0} ✨", ["en"] = "Friend request sent to @{0} ✨", ["de"] = "Freundschaftsanfrage an @{0} gesendet ✨" },
        ["friends_now_friends"] = new() { ["ar"] = "صرتما أصدقاء الآن 🎉", ["en"] = "You are friends now 🎉", ["de"] = "Ihr seid jetzt Freunde 🎉" },
        ["friends_copied"] = new() { ["ar"] = "تم نسخ اسم المستخدم. أرسله لصديقك!", ["en"] = "Username copied. Send it to your friend!", ["de"] = "Benutzername kopiert. Schick ihn deinem Freund!" },
        ["friends_copy_failed"] = new() { ["ar"] = "تعذر النسخ. يمكنك تحديد الاسم ونسخه.", ["en"] = "Couldn't copy. You can select the name and copy it by hand.", ["de"] = "Kopieren fehlgeschlagen. Markiere den Namen und kopiere ihn von Hand." },
        ["friends_meal_fallback"] = new() { ["ar"] = "وجبة", ["en"] = "Meal", ["de"] = "Gericht" },

        // Friend week sync
        ["friends_sync_button"] = new() { ["ar"] = "مزامنة أسبوعي", ["en"] = "Sync my week", ["de"] = "Meine Woche synchronisieren" },
        ["friends_sync_confirm_title"] = new() { ["ar"] = "مزامنة الأسبوع؟", ["en"] = "Sync this week?", ["de"] = "Diese Woche synchronisieren?" },
        // {0} = friend's display name, {1} = the week's date range (e.g. "5 Oct – 11 Oct")
        ["friends_sync_confirm_eyebrow"] = new() { ["ar"] = "سيتم استبدال خطتك لأسبوع {1} بخطة {0}", ["en"] = "This will replace your plan for the week of {1} with {0}'s plan", ["de"] = "Dein Plan für die Woche vom {1} wird durch den von {0} ersetzt" },
        ["friends_sync_confirm_note"] = new() { ["ar"] = "لا يمكن التراجع عن هذا تلقائياً، لكن يمكنك تعديل خطتك يدوياً بعد ذلك.", ["en"] = "This can't be undone automatically, but you can still edit your plan afterward.", ["de"] = "Das lässt sich nicht automatisch rückgängig machen, du kannst deinen Plan danach aber jederzeit bearbeiten." },
        // {0} = friend's display name
        ["friends_sync_confirm_action"] = new() { ["ar"] = "نعم، انسخ خطة {0}", ["en"] = "Yes, copy {0}'s plan", ["de"] = "Ja, Plan von {0} übernehmen" },
        ["friends_sync_syncing"] = new() { ["ar"] = "جارٍ المزامنة...", ["en"] = "Syncing...", ["de"] = "Wird synchronisiert …" },
        // {0} = friend's display name
        ["friends_sync_success"] = new() { ["ar"] = "تمت مزامنة أسبوعك مع خطة {0} ✨", ["en"] = "Your week now matches {0}'s plan ✨", ["de"] = "Deine Woche entspricht jetzt dem Plan von {0} ✨" },
        ["friends_add_placeholder"] = new() { ["ar"] = "أضف صديقاً باسم المستخدم", ["en"] = "Add a friend by username", ["de"] = "Freund per Benutzername hinzufügen" },
        ["friends_send_request"] = new() { ["ar"] = "إرسال طلب", ["en"] = "Send request", ["de"] = "Anfrage senden" },
        ["friends_your_username"] = new() { ["ar"] = "اسم المستخدم الخاص بك", ["en"] = "Your username", ["de"] = "Dein Benutzername" },
        ["friends_need_username"] = new() { ["ar"] = "اختر اسم مستخدم من الإعدادات أولاً لتتمكن من إضافة الأصدقاء.", ["en"] = "Pick a username in Settings first so you can add friends.", ["de"] = "Wähle zuerst in den Einstellungen einen Benutzernamen, um Freunde hinzuzufügen." },
        ["friends_need_username_short"] = new() { ["ar"] = "اختر اسم مستخدم أولاً من الإعدادات.", ["en"] = "Pick a username in Settings first.", ["de"] = "Wähle zuerst in den Einstellungen einen Benutzernamen." },
        ["friends_go_settings"] = new() { ["ar"] = "اذهب إلى الإعدادات", ["en"] = "Go to Settings", ["de"] = "Zu den Einstellungen" },
        ["friends_wants_to_add"] = new() { ["ar"] = "تريد إضافتك", ["en"] = "wants to add you", ["de"] = "möchte dich hinzufügen" },
        ["friends_accept"] = new() { ["ar"] = "قبول", ["en"] = "Accept", ["de"] = "Annehmen" },
        ["friends_decline"] = new() { ["ar"] = "رفض", ["en"] = "Decline", ["de"] = "Ablehnen" },
        ["friends_empty"] = new() { ["ar"] = "شارك اسم المستخدم أعلاه وابدأ التخطيط مع أصدقائك.", ["en"] = "Share your username above and start planning with friends.", ["de"] = "Teile deinen Benutzernamen und plant gemeinsam mit Freunden." },
        ["friends_planned_days"] = new() { ["ar"] = "{0} من 7 أيام مخططة", ["en"] = "{0} of 7 days planned", ["de"] = "{0} von 7 Tagen geplant" },
        ["friends_prev_week"] = new() { ["ar"] = "الأسبوع السابق", ["en"] = "Previous week", ["de"] = "Vorherige Woche" },
        ["friends_next_week"] = new() { ["ar"] = "الأسبوع القادم", ["en"] = "Next week", ["de"] = "Nächste Woche" },
        ["friends_replaces"] = new() { ["ar"] = "يحلّ محل: {0}", ["en"] = "Replaces: {0}", ["de"] = "Ersetzt: {0}" },
        ["friends_empty_day"] = new() { ["ar"] = "يوم فارغ", ["en"] = "Empty day", ["de"] = "Leerer Tag" },
        ["friends_replace_week"] = new() { ["ar"] = "استبدال أسبوعي", ["en"] = "Replace my week", ["de"] = "Meine Woche ersetzen" },
        ["friends_copy_empty"] = new() { ["ar"] = "نسخ الأيام الفارغة فقط", ["en"] = "Copy empty days only", ["de"] = "Nur leere Tage kopieren" },
        ["friends_discover_hint"] = new() { ["ar"] = "أضفهم وشاهد أسبوعهم", ["en"] = "Add them to see their week", ["de"] = "Füge sie hinzu und sieh ihre Woche" },
        ["friends_add_named"] = new() { ["ar"] = "إضافة {0}", ["en"] = "Add {0}", ["de"] = "{0} hinzufügen" },
        ["friends_request_sent"] = new() { ["ar"] = "تم إرسال طلب الصداقة ✨", ["en"] = "Friend request sent ✨", ["de"] = "Freundschaftsanfrage gesendet ✨" },
        ["friends_request_sent_to"] = new() { ["ar"] = "تم إرسال طلب الصداقة إلى @{0} ✨", ["en"] = "Friend request sent to @{0} ✨", ["de"] = "Freundschaftsanfrage an @{0} gesendet ✨" },
        ["friends_now_friends"] = new() { ["ar"] = "صرتما أصدقاء الآن 🎉", ["en"] = "You're friends now 🎉", ["de"] = "Ihr seid jetzt befreundet 🎉" },
        ["friends_username_copied"] = new() { ["ar"] = "تم نسخ اسم المستخدم. أرسله لصديقك!", ["en"] = "Username copied. Send it to a friend!", ["de"] = "Benutzername kopiert. Schick ihn einem Freund!" },
        ["friends_copy_failed"] = new() { ["ar"] = "تعذر النسخ. يمكنك تحديد الاسم ونسخه.", ["en"] = "Couldn't copy. You can select the name and copy it.", ["de"] = "Kopieren fehlgeschlagen. Markiere den Namen und kopiere ihn." },
        ["friends_nothing_to_copy"] = new() { ["ar"] = "لا توجد أيام فارغة لنسخها.", ["en"] = "No empty days to copy.", ["de"] = "Keine leeren Tage zum Kopieren." },
        ["friends_copy_empty_success"] = new() { ["ar"] = "تم نسخ الأيام الفارغة من خطة {0} ✨", ["en"] = "Filled your empty days from {0}'s plan ✨", ["de"] = "Leere Tage mit dem Plan von {0} gefüllt ✨" },
        ["welcome_eyebrow"] = new() { ["ar"] = "أهلاً بك", ["en"] = "Welcome", ["de"] = "Willkommen" },
        ["welcome_lead"] = new() { ["ar"] = "خطّة أسبوعك لوجبات العائلة، في مكان واحد. كيف تحب أن نبدأ؟", ["en"] = "Your family's weekly meal plan, in one place. How would you like to start?", ["de"] = "Der Wochenplan für euer Familienessen, an einem Ort. Wie möchtest du starten?" },
        ["welcome_empty"] = new() { ["ar"] = "ابدأ فارغاً", ["en"] = "Start empty", ["de"] = "Leer starten" },
        ["welcome_empty_desc"] = new() { ["ar"] = "أختار وجباتي بنفسي", ["en"] = "I'll pick my own meals", ["de"] = "Ich wähle meine Gerichte selbst" },
        ["welcome_assistant"] = new() { ["ar"] = "اسأل المساعد", ["en"] = "Ask the assistant", ["de"] = "Assistent fragen" },
        ["welcome_assistant_desc"] = new() { ["ar"] = "أخبره بما في مطبخك", ["en"] = "Tell it what's in your kitchen", ["de"] = "Sag ihm, was in deiner Küche ist" },
        ["welcome_note"] = new() { ["ar"] = "تستطيع تغيير أي شيء لاحقاً.", ["en"] = "You can change anything later.", ["de"] = "Du kannst später alles ändern." },
        ["signin_lead"] = new() { ["ar"] = "احفظ خطتك واستخدمها على كل أجهزتك", ["en"] = "Save your plan and use it on all your devices", ["de"] = "Speichere deinen Plan und nutze ihn auf allen Geräten" },
        ["signin_sub"] = new() { ["ar"] = "أدخل بريدك الإلكتروني للمتابعة، بدون كلمة مرور.", ["en"] = "Enter your email to continue — no password needed.", ["de"] = "Gib deine E-Mail ein, um fortzufahren – ganz ohne Passwort." },
        ["signin_email"] = new() { ["ar"] = "البريد الإلكتروني", ["en"] = "Email", ["de"] = "E-Mail" },
        ["signin_continue"] = new() { ["ar"] = "متابعة", ["en"] = "Continue", ["de"] = "Weiter" },
        ["signin_guest"] = new() { ["ar"] = "تابع كضيف", ["en"] = "Continue as guest", ["de"] = "Als Gast fortfahren" },
        ["signin_guest_note"] = new() { ["ar"] = "يمكنك ربط بريدك لاحقاً من الإعدادات.", ["en"] = "You can link your email later in Settings.", ["de"] = "Du kannst deine E-Mail später in den Einstellungen verknüpfen." },
        ["signin_invalid_email"] = new() { ["ar"] = "الرجاء إدخال بريد إلكتروني صحيح.", ["en"] = "Please enter a valid email address.", ["de"] = "Bitte gib eine gültige E-Mail-Adresse ein." },
        ["signin_failed"] = new() { ["ar"] = "حدث خطأ أثناء تسجيل الدخول: {0}", ["en"] = "Sign-in failed: {0}", ["de"] = "Anmeldung fehlgeschlagen: {0}" },

        // AI assistant (a chat that discusses first, then proposes changes and saves them once the user agrees)
        ["ai_plan_title"] = new() { ["ar"] = "المساعد الذكي", ["en"] = "AI Assistant", ["de"] = "KI-Assistent" },
        ["ai_plan_subtitle"] = new() { ["ar"] = "أخبرني بما في مطبخك أو بما تريد تخطيطه", ["en"] = "Tell me what's in your kitchen or what you'd like to plan", ["de"] = "Erzähl mir, was du in der Küche hast oder was du planen möchtest" },
        ["ai_plan_chat_empty"] = new() { ["ar"] = "أخبرني بالمكونات المتوفرة لديك وسأقترح عليك أطباقاً، أو بما تريد تخطيطه لهذا الأسبوع. لن أضيف شيئاً قبل أن نتفق.", ["en"] = "Tell me which ingredients you have and I'll suggest dishes, or what you'd like to plan this week. I won't add anything until we agree.", ["de"] = "Sag mir, welche Zutaten du hast, und ich schlage dir Gerichte vor – oder was du diese Woche planen möchtest. Ich füge nichts hinzu, bevor wir uns einig sind." },
        ["ai_plan_placeholder_short"] = new() { ["ar"] = "ماذا لديك في المطبخ؟", ["en"] = "What's in your kitchen?", ["de"] = "Was hast du in der Küche?" },
        ["ai_chat_chip_ingredients"] = new() { ["ar"] = "لدي بيض وبطاطا وبصل، ماذا أطبخ؟", ["en"] = "I have eggs, potatoes and onions — what can I cook?", ["de"] = "Ich habe Eier, Kartoffeln und Zwiebeln – was kann ich kochen?" },
        ["ai_chat_chip_plan"] = new() { ["ar"] = "ساعدني في تخطيط بقية الأسبوع", ["en"] = "Help me plan the rest of the week", ["de"] = "Hilf mir, den Rest der Woche zu planen" },
        ["ai_chat_chip_shopping"] = new() { ["ar"] = "أضف حليب وبيض وخبز إلى قائمتي", ["en"] = "Add milk, eggs and bread to my list", ["de"] = "Setz Milch, Eier und Brot auf meine Liste" },
        ["ai_chat_confirm_hint"] = new() { ["ar"] = "قل «نعم» لأنفّذ ذلك، أو أخبرني بما تريد تغييره.", ["en"] = "Say “yes” and I'll do it, or tell me what to change.", ["de"] = "Sag „ja“, dann erledige ich das – oder sag mir, was ich ändern soll." },
        ["ai_chat_propose_intro"] = new() { ["ar"] = "هذا ما يمكنني فعله:", ["en"] = "Here's what I can do:", ["de"] = "Das kann ich für dich tun:" },
        ["ai_plan_preview_meals_heading"] = new() { ["ar"] = "الوجبات", ["en"] = "Meals", ["de"] = "Gerichte" },
        ["ai_plan_proposal_add_heading"] = new() { ["ar"] = "إضافة إلى قائمة التسوق", ["en"] = "Add to shopping list", ["de"] = "Auf die Einkaufsliste" },
        ["ai_plan_proposal_remove_heading"] = new() { ["ar"] = "حذف من قائمة التسوق", ["en"] = "Remove from shopping list", ["de"] = "Von der Einkaufsliste entfernen" },
        ["ai_plan_default_shop_name"] = new() { ["ar"] = "قائمة التسوق", ["en"] = "Shopping List", ["de"] = "Einkaufsliste" },
        ["ai_new_chat"] = new() { ["ar"] = "محادثة جديدة", ["en"] = "New chat", ["de"] = "Neuer Chat" },
        ["ai_thinking_label"] = new() { ["ar"] = "جارٍ التفكير…", ["en"] = "Thinking…", ["de"] = "Denke nach …" },
        ["ai_proposal_badge"] = new() { ["ar"] = "اقتراح — لم يُحفظ بعد", ["en"] = "Proposal — not saved yet", ["de"] = "Vorschlag – noch nicht gespeichert" },
        // {0} = number of changes
        ["ai_proposal_changes"] = new() { ["ar"] = "{0} تغييرات", ["en"] = "{0} changes", ["de"] = "{0} Änderungen" },
        ["ai_proposal_add_heading"] = new() { ["ar"] = "أضف إلى المشتريات", ["en"] = "Add to shopping", ["de"] = "Zum Einkauf hinzufügen" },
        ["ai_proposal_remove_heading"] = new() { ["ar"] = "احذف من المشتريات", ["en"] = "Remove from shopping", ["de"] = "Vom Einkauf entfernen" },
        ["ai_proposal_apply"] = new() { ["ar"] = "تطبيق التغييرات", ["en"] = "Apply changes", ["de"] = "Änderungen übernehmen" },
        ["ai_proposal_cancelled"] = new() { ["ar"] = "أُلغي الاقتراح", ["en"] = "Proposal cancelled", ["de"] = "Vorschlag verworfen" },
        ["ai_proposal_replaced"] = new() { ["ar"] = "استُبدل باقتراح جديد", ["en"] = "Replaced by a newer proposal", ["de"] = "Durch einen neuen Vorschlag ersetzt" },
        // {0} = meal or item name
        ["ai_remove_named"] = new() { ["ar"] = "إزالة {0}", ["en"] = "Remove {0}", ["de"] = "{0} entfernen" },
        ["ai_applied_title"] = new() { ["ar"] = "تم التطبيق", ["en"] = "Applied", ["de"] = "Übernommen" },
        ["ai_applied_sep"] = new() { ["ar"] = "، ", ["en"] = ". ", ["de"] = ". " },
        // {0} = meals with their days
        ["ai_applied_meals"] = new() { ["ar"] = "أُضيف {0}", ["en"] = "Added {0}", ["de"] = "{0} hinzugefügt" },
        // {0} = meal, {1} = weekday
        ["ai_applied_meal_on"] = new() { ["ar"] = "{0} ليوم {1}", ["en"] = "{0} on {1}", ["de"] = "{0} am {1}" },
        // {0} = items
        ["ai_applied_items"] = new() { ["ar"] = "أُضيف {0} إلى المشتريات", ["en"] = "Added {0} to shopping", ["de"] = "{0} zum Einkauf hinzugefügt" },
        ["ai_applied_removed"] = new() { ["ar"] = "حُذف {0} من المشتريات", ["en"] = "Removed {0} from shopping", ["de"] = "{0} vom Einkauf entfernt" },
        ["ai_applied_not_found"] = new() { ["ar"] = "لم أجد {0} في قائمتك", ["en"] = "Couldn't find {0} on your list", ["de"] = "{0} nicht auf deiner Liste gefunden" },
        ["ai_plan_reply_done"] = new() { ["ar"] = "تم! هذا ما قمت به:", ["en"] = "Done! Here's what I did:", ["de"] = "Erledigt! Das habe ich gemacht:" },
        // {0} = shopping list name
        ["ai_plan_reply_removed_heading"] = new() { ["ar"] = "حُذف من {0}", ["en"] = "Removed from {0}", ["de"] = "Entfernt aus {0}" },
        ["ai_plan_reply_not_found_heading"] = new() { ["ar"] = "غير موجود في قائمتك", ["en"] = "Not on your list", ["de"] = "Nicht auf deiner Liste" },
        ["ai_plan_reply_nothing_found"] = new() { ["ar"] = "لم أجد ذلك في قائمة التسوق.", ["en"] = "I couldn't find that on your shopping list.", ["de"] = "Das habe ich auf deiner Einkaufsliste nicht gefunden." },
        // {0} = shopping list name
        ["ai_plan_reply_items_heading"] = new() { ["ar"] = "أضيف إلى {0}", ["en"] = "Added to {0}", ["de"] = "Hinzugefügt zu {0}" },

        // AI assistant settings card
        ["settings_ai_title"] = new() { ["ar"] = "المساعد الذكي", ["en"] = "AI Assistant", ["de"] = "KI-Assistent" },
        ["settings_ai_desc"] = new() { ["ar"] = "ألصق مفتاح Gemini المجاني الخاص بك لتفعيل المساعد الذكي.", ["en"] = "Paste your own free Gemini API key to enable the AI assistant.", ["de"] = "Füge deinen eigenen kostenlosen Gemini-API-Schlüssel ein, um den KI-Assistenten zu aktivieren." },
        ["settings_ai_key_label"] = new() { ["ar"] = "مفتاح Gemini API", ["en"] = "Gemini API key", ["de"] = "Gemini-API-Schlüssel" },
        ["settings_ai_key_placeholder"] = new() { ["ar"] = "ألصق مفتاح API الخاص بك", ["en"] = "Paste your API key", ["de"] = "API-Schlüssel einfügen" },
        ["settings_ai_key_hint_link"] = new() { ["ar"] = "احصل على مفتاح API مجاني من Google AI Studio", ["en"] = "Get a free API key from Google AI Studio", ["de"] = "Hol dir einen kostenlosen API-Schlüssel von Google AI Studio" },
        ["settings_ai_model_label"] = new() { ["ar"] = "النموذج", ["en"] = "Model", ["de"] = "Modell" },
        ["settings_ai_refresh_models"] = new() { ["ar"] = "تحديث قائمة النماذج", ["en"] = "Refresh model list", ["de"] = "Modellliste aktualisieren" },
        ["settings_ai_saved"] = new() { ["ar"] = "تم حفظ إعدادات الذكاء الاصطناعي.", ["en"] = "AI settings saved.", ["de"] = "KI-Einstellungen gespeichert." },

        // Nutrition on meals (3.0): values are per serving; estimates always show their badge
        ["nut_kcal"] = new() { ["ar"] = "سعرة", ["en"] = "kcal", ["de"] = "kcal" },
        ["nut_g"] = new() { ["ar"] = "غ", ["en"] = "g", ["de"] = "g" },
        ["nut_calories"] = new() { ["ar"] = "السعرات", ["en"] = "Calories", ["de"] = "Kalorien" },
        ["nut_protein"] = new() { ["ar"] = "بروتين", ["en"] = "Protein", ["de"] = "Eiweiß" },
        ["nut_carbs"] = new() { ["ar"] = "كربوهيدرات", ["en"] = "Carbs", ["de"] = "Kohlenhydrate" },
        ["nut_fat"] = new() { ["ar"] = "دهون", ["en"] = "Fat", ["de"] = "Fett" },
        ["nut_fibre"] = new() { ["ar"] = "ألياف", ["en"] = "Fibre", ["de"] = "Ballaststoffe" },
        ["nut_servings"] = new() { ["ar"] = "الحصص", ["en"] = "Servings", ["de"] = "Portionen" },
        ["nut_per_serving"] = new() { ["ar"] = "للحصة", ["en"] = "per serving", ["de"] = "pro Portion" },
        ["nut_none"] = new() { ["ar"] = "لا توجد قيم غذائية", ["en"] = "No nutrition yet", ["de"] = "Noch keine Nährwerte" },
        ["nut_none_body"] = new() { ["ar"] = "هذه الوجبة بلا قيم غذائية. قدّرها من المكوّنات أو أدخلها بنفسك.", ["en"] = "This meal has no nutrition values. Estimate them from the ingredients or enter them yourself.", ["de"] = "Dieses Gericht hat keine Nährwerte. Schätze sie aus den Zutaten oder trage sie selbst ein." },
        ["nut_estimate"] = new() { ["ar"] = "تقدير", ["en"] = "Estimate", ["de"] = "Schätzung" },
        ["nut_est_short"] = new() { ["ar"] = "تقديري", ["en"] = "est.", ["de"] = "gesch." },
        ["nut_manual"] = new() { ["ar"] = "أدخلتها بنفسك", ["en"] = "Entered by you", ["de"] = "Von dir eingegeben" },
        ["nut_edit"] = new() { ["ar"] = "تعديل", ["en"] = "Edit", ["de"] = "Bearbeiten" },
        ["nut_estimate_from"] = new() { ["ar"] = "قدّر من المكوّنات", ["en"] = "Estimate from ingredients", ["de"] = "Aus Zutaten schätzen" },
        ["nut_enter_manual"] = new() { ["ar"] = "أدخل يدوياً", ["en"] = "Enter manually", ["de"] = "Manuell eingeben" },
        ["nut_estimating"] = new() { ["ar"] = "جارٍ التقدير…", ["en"] = "Estimating…", ["de"] = "Schätze …" },
        // {0} = number of ingredients
        ["nut_estimate_note"] = new() { ["ar"] = "تقدير بالذكاء الاصطناعي من {0} مكوّنات. راجع القيم وعدّلها.", ["en"] = "AI estimate from {0} ingredients. Check the values and adjust them.", ["de"] = "KI-Schätzung aus {0} Zutaten. Prüfe die Werte und passe sie an." },
        // {0} = kcal from 4·protein + 4·carbs + 9·fat
        ["nut_macros_sum"] = new() { ["ar"] = "مجموع الماكروز {0} سعرة", ["en"] = "Macros add up to {0} kcal", ["de"] = "Makros ergeben {0} kcal" },
        ["nut_yield"] = new() { ["ar"] = "الوصفة تكفي", ["en"] = "Recipe makes", ["de"] = "Rezept ergibt" },
        // {0} = preparation minutes
        ["nut_minutes"] = new() { ["ar"] = "{0} د", ["en"] = "{0} min", ["de"] = "{0} Min." },
        ["nut_saved"] = new() { ["ar"] = "تم الحفظ", ["en"] = "Saved", ["de"] = "Gespeichert" },

        // Slots, targets panel, slot and quick-add sheets, profile and account (3.0)
        ["nut_slot_Meal1"] = new() { ["ar"] = "الوجبة 1", ["en"] = "Meal 1", ["de"] = "Mahlzeit 1" },
        ["nut_slot_PostWorkout"] = new() { ["ar"] = "بعد التمرين", ["en"] = "Post-workout", ["de"] = "Nach dem Training" },
        ["nut_slot_Dinner"] = new() { ["ar"] = "العشاء", ["en"] = "Dinner", ["de"] = "Abendessen" },
        ["nut_slot_Snack"] = new() { ["ar"] = "سناك المساء", ["en"] = "Evening snack", ["de"] = "Abendsnack" },
        ["nut_today"] = new() { ["ar"] = "اليوم", ["en"] = "Today", ["de"] = "Heute" },
        ["nut_day_training"] = new() { ["ar"] = "يوم تمرين", ["en"] = "Training day", ["de"] = "Trainingstag" },
        ["nut_day_rest"] = new() { ["ar"] = "يوم راحة", ["en"] = "Rest day", ["de"] = "Ruhetag" },
        ["nut_left"] = new() { ["ar"] = "متبقي", ["en"] = "left", ["de"] = "übrig" },
        ["nut_left_today"] = new() { ["ar"] = "المتبقي اليوم", ["en"] = "left today", ["de"] = "heute übrig" },
        ["nut_eaten"] = new() { ["ar"] = "تم تناوله", ["en"] = "eaten", ["de"] = "gegessen" },
        ["nut_planned"] = new() { ["ar"] = "مخطط", ["en"] = "planned", ["de"] = "geplant" },
        ["nut_open"] = new() { ["ar"] = "غير مخطط", ["en"] = "open", ["de"] = "offen" },
        ["nut_over"] = new() { ["ar"] = "فوق", ["en"] = "Over", ["de"] = "Darüber" },
        ["nut_add_meal"] = new() { ["ar"] = "أضف وجبة", ["en"] = "Add a meal", ["de"] = "Gericht hinzufügen" },
        ["nut_quick_add"] = new() { ["ar"] = "إضافة سريعة", ["en"] = "Quick add", ["de"] = "Schnell erfassen" },
        ["nut_mark_eaten"] = new() { ["ar"] = "تم تناولها", ["en"] = "Mark as eaten", ["de"] = "Als gegessen markieren" },
        ["nut_undo"] = new() { ["ar"] = "تراجع", ["en"] = "Undo", ["de"] = "Rückgängig" },
        ["nut_leftover"] = new() { ["ar"] = "بقايا", ["en"] = "Leftover", ["de"] = "Reste" },
        ["nut_quick_tag"] = new() { ["ar"] = "إضافة سريعة", ["en"] = "Quick add", ["de"] = "Schnell erfasst" },
        // {0} = kcal still open, {1} = grams of protein still open
        ["nut_open_line"] = new() { ["ar"] = "غير مخطط بعد: {0} سعرة و{1} غ بروتين", ["en"] = "Still open: {0} kcal and {1} g protein", ["de"] = "Noch offen: {0} kcal und {1} g Eiweiß" },
        ["nut_aim"] = new() { ["ar"] = "الهدف: 30–40 غ بروتين لكل وجبة", ["en"] = "Aim for 30–40 g protein per meal", ["de"] = "Ziel: 30–40 g Eiweiß pro Mahlzeit" },
        ["nut_no_post"] = new() { ["ar"] = "يوم راحة: لا توجد خانة بعد التمرين", ["en"] = "Rest day: no post-workout slot", ["de"] = "Ruhetag: kein Slot nach dem Training" },
        ["nut_empty_day"] = new() { ["ar"] = "لا شيء مخطط بعد", ["en"] = "Nothing planned yet", ["de"] = "Noch nichts geplant" },
        ["nut_week_empty"] = new() { ["ar"] = "ابدأ باقتراح غني بالبروتين أو أضف عنصراً سريعاً.", ["en"] = "Start with a high-protein suggestion or add a quick item.", ["de"] = "Starte mit einem eiweißreichen Vorschlag oder einem schnellen Eintrag." },
        ["nut_next_week"] = new() { ["ar"] = "الأسبوع القادم", ["en"] = "Next week", ["de"] = "Nächste Woche" },
        ["nut_carbs_short"] = new() { ["ar"] = "كربو", ["en"] = "Carbs", ["de"] = "KH" },
        ["nut_fat_short"] = new() { ["ar"] = "دهون", ["en"] = "Fat", ["de"] = "Fett" },
        ["nut_quick_title"] = new() { ["ar"] = "إضافة سريعة", ["en"] = "Quick add", ["de"] = "Schnell erfassen" },
        ["nut_what_is"] = new() { ["ar"] = "ما هو؟", ["en"] = "What is it?", ["de"] = "Was ist es?" },
        ["nut_amount"] = new() { ["ar"] = "الكمية", ["en"] = "Amount", ["de"] = "Menge" },
        ["nut_recent"] = new() { ["ar"] = "الأخيرة", ["en"] = "Recent", ["de"] = "Zuletzt" },
        ["nut_quick_hint"] = new() { ["ar"] = "بدون وصفة. أضف السعرات والبروتين إن عرفتها.", ["en"] = "No recipe needed. Add calories and protein if you know them.", ["de"] = "Kein Rezept nötig. Trage Kalorien und Eiweiß ein, wenn du sie kennst." },
        ["nut_also_eaten"] = new() { ["ar"] = "سجّلها كمأكولة الآن", ["en"] = "Mark as eaten now", ["de"] = "Gleich als gegessen markieren" },
        // {0} = slot name
        ["nut_add_to"] = new() { ["ar"] = "أضف إلى {0}", ["en"] = "Add to {0}", ["de"] = "Zu {0} hinzufügen" },
        ["nut_servings_label"] = new() { ["ar"] = "الحصص", ["en"] = "Servings", ["de"] = "Portionen" },
        ["nut_prep"] = new() { ["ar"] = "تحضير مسبق", ["en"] = "Meal prep", ["de"] = "Meal Prep" },
        ["nut_prep_hint"] = new() { ["ar"] = "اطبخ مرة وكُل 2–3 مرات", ["en"] = "Cook once, eat 2–3 times", ["de"] = "Einmal kochen, 2–3 Mal essen" },
        ["nut_off"] = new() { ["ar"] = "إيقاف", ["en"] = "Off", ["de"] = "Aus" },
        ["nut_replace"] = new() { ["ar"] = "غيّر الوجبة", ["en"] = "Change meal", ["de"] = "Gericht ändern" },
        ["nut_remove"] = new() { ["ar"] = "إزالة", ["en"] = "Remove", ["de"] = "Entfernen" },
        ["nut_eaten_toast"] = new() { ["ar"] = "تم تسجيلها كمأكولة", ["en"] = "Marked as eaten", ["de"] = "Als gegessen markiert" },
        // {0} = slot name
        ["nut_added_toast"] = new() { ["ar"] = "تمت الإضافة إلى {0}", ["en"] = "Added to {0}", ["de"] = "Zu {0} hinzugefügt" },
        ["nut_removed_toast"] = new() { ["ar"] = "تمت الإزالة", ["en"] = "Removed", ["de"] = "Entfernt" },
        ["nut_close"] = new() { ["ar"] = "إغلاق", ["en"] = "Close", ["de"] = "Schließen" },
        // {0} = slot name
        ["nut_time_for"] = new() { ["ar"] = "وقت {0}", ["en"] = "Time for {0}", ["de"] = "Zeit für {0}" },
        // {0} = slot name
        ["nut_pick_for"] = new() { ["ar"] = "اختر وجبة: {0}", ["en"] = "Choose for {0}", ["de"] = "Auswahl für {0}" },
        // {0} = kcal open, {1} = grams of protein open
        ["nut_budget"] = new() { ["ar"] = "غير مخطط: {0} سعرة و{1} غ بروتين", ["en"] = "{0} kcal and {1} g protein open", ["de"] = "Noch offen: {0} kcal und {1} g Eiweiß" },
        ["nut_profile"] = new() { ["ar"] = "الملف والأهداف", ["en"] = "Profile & targets", ["de"] = "Profil & Ziele" },
        ["nut_sex"] = new() { ["ar"] = "الجنس", ["en"] = "Sex", ["de"] = "Geschlecht" },
        ["nut_male"] = new() { ["ar"] = "ذكر", ["en"] = "Male", ["de"] = "Männlich" },
        ["nut_female"] = new() { ["ar"] = "أنثى", ["en"] = "Female", ["de"] = "Weiblich" },
        ["nut_age"] = new() { ["ar"] = "العمر", ["en"] = "Age", ["de"] = "Alter" },
        ["nut_height"] = new() { ["ar"] = "الطول", ["en"] = "Height", ["de"] = "Größe" },
        ["nut_weight"] = new() { ["ar"] = "الوزن", ["en"] = "Weight", ["de"] = "Gewicht" },
        ["nut_activity"] = new() { ["ar"] = "مستوى النشاط", ["en"] = "Activity level", ["de"] = "Aktivitätslevel" },
        ["nut_activity_0"] = new() { ["ar"] = "قليل الحركة", ["en"] = "Sedentary", ["de"] = "Kaum aktiv" },
        ["nut_activity_1"] = new() { ["ar"] = "خفيف: 1–3 تمارين أسبوعياً", ["en"] = "Light: 1–3 workouts a week", ["de"] = "Leicht: 1–3 Trainings pro Woche" },
        ["nut_activity_2"] = new() { ["ar"] = "متوسط: 3–5 تمارين", ["en"] = "Moderate: 3–5 workouts", ["de"] = "Mäßig: 3–5 Trainings" },
        ["nut_activity_3"] = new() { ["ar"] = "نشيط جداً: 6–7 تمارين", ["en"] = "Very active: 6–7 workouts", ["de"] = "Sehr aktiv: 6–7 Trainings" },
        ["nut_goal"] = new() { ["ar"] = "الهدف", ["en"] = "Goal", ["de"] = "Ziel" },
        ["nut_goal_Lose"] = new() { ["ar"] = "خسارة الدهون", ["en"] = "Lose fat", ["de"] = "Fett verlieren" },
        ["nut_goal_Keep"] = new() { ["ar"] = "الحفاظ", ["en"] = "Maintain", ["de"] = "Halten" },
        ["nut_goal_Gain"] = new() { ["ar"] = "زيادة", ["en"] = "Gain", ["de"] = "Zunehmen" },
        ["nut_calc"] = new() { ["ar"] = "الأهداف المحسوبة", ["en"] = "Calculated targets", ["de"] = "Berechnete Ziele" },
        ["nut_bmr"] = new() { ["ar"] = "معدل الأيض الأساسي", ["en"] = "Resting energy (BMR)", ["de"] = "Grundumsatz (BMR)" },
        ["nut_tdee"] = new() { ["ar"] = "إجمالي الحرق اليومي", ["en"] = "Daily energy use (TDEE)", ["de"] = "Tagesumsatz (TDEE)" },
        ["nut_adjust"] = new() { ["ar"] = "تعديل الهدف", ["en"] = "Goal adjustment", ["de"] = "Zielanpassung" },
        ["nut_adjust_check"] = new() { ["ar"] = "تعديل الفحص الأسبوعي", ["en"] = "Weekly check adjustment", ["de"] = "Anpassung aus dem Wochencheck" },
        ["nut_custom"] = new() { ["ar"] = "مخصص", ["en"] = "Custom", ["de"] = "Eigener Wert" },
        ["nut_calculated"] = new() { ["ar"] = "محسوب", ["en"] = "Calculated", ["de"] = "Berechnet" },
        ["nut_reset"] = new() { ["ar"] = "استرجاع المحسوب", ["en"] = "Reset to calculated", ["de"] = "Auf berechnet zurücksetzen" },
        ["nut_rest_note"] = new() { ["ar"] = "يوم الراحة: أقل بـ200 سعرة من الكربوهيدرات. البروتين والدهون كما هي.", ["en"] = "Rest day: 200 kcal less, taken from carbs. Protein and fat stay the same.", ["de"] = "Ruhetag: 200 kcal weniger, aus Kohlenhydraten. Eiweiß und Fett bleiben gleich." },
        // {0} = grams of protein per kg
        ["nut_per_kg"] = new() { ["ar"] = "{0} غ لكل كغ من وزن الجسم", ["en"] = "{0} g per kg body weight", ["de"] = "{0} g pro kg Körpergewicht" },
        ["nut_training_days"] = new() { ["ar"] = "أيام التمرين", ["en"] = "Training days", ["de"] = "Trainingstage" },
        ["nut_meal_times"] = new() { ["ar"] = "أوقات الوجبات", ["en"] = "Meal times", ["de"] = "Mahlzeiten-Zeiten" },
        ["nut_meal_times_hint"] = new() { ["ar"] = "أوقاتك المعتادة. تُرتَّب الوجبات حسب الوقت ويظهر الوقت على البطاقة.", ["en"] = "Your usual times. Slots are sorted by time and show it on the card.", ["de"] = "Deine üblichen Zeiten. Die Slots werden nach Zeit sortiert und zeigen sie auf der Karte." },
        ["nut_daily_targets"] = new() { ["ar"] = "الأهداف اليومية", ["en"] = "Daily targets", ["de"] = "Tagesziele" },
        // {0} = age, {1} = height in cm, {2} = weight in kg
        ["nut_profile_meta"] = new() { ["ar"] = "{0} سنة · {1} سم · {2} كغ", ["en"] = "{0} y · {1} cm · {2} kg", ["de"] = "{0} J. · {1} cm · {2} kg" },
        ["nut_kg"] = new() { ["ar"] = "كغ", ["en"] = "kg", ["de"] = "kg" },
        ["nut_cm"] = new() { ["ar"] = "سم", ["en"] = "cm", ["de"] = "cm" },
        ["nut_account"] = new() { ["ar"] = "الحساب", ["en"] = "Account", ["de"] = "Konto" },
        // {0} = email
        ["nut_signed_as"] = new() { ["ar"] = "مسجّل باسم {0}", ["en"] = "Signed in as {0}", ["de"] = "Angemeldet als {0}" },
        ["nut_signed_out"] = new() { ["ar"] = "غير مسجّل", ["en"] = "Not signed in", ["de"] = "Nicht angemeldet" },
        ["nut_sign_out"] = new() { ["ar"] = "تسجيل الخروج", ["en"] = "Sign out", ["de"] = "Abmelden" },
        ["nut_sign_in"] = new() { ["ar"] = "تسجيل الدخول", ["en"] = "Sign in", ["de"] = "Anmelden" },
        ["nut_privacy"] = new() { ["ar"] = "تبقى بياناتك الصحية في حسابك.", ["en"] = "Health data stays in your account.", ["de"] = "Gesundheitsdaten bleiben in deinem Konto." },
        ["nut_friends"] = new() { ["ar"] = "الأصدقاء", ["en"] = "Friends", ["de"] = "Freunde" },
        ["nut_friends_sub"] = new() { ["ar"] = "خططوا أسبوعكم معاً", ["en"] = "Plan your week together", ["de"] = "Plant eure Woche zusammen" },
        // {0} = number of friend requests
        ["nut_request_count"] = new() { ["ar"] = "{0} طلب", ["en"] = "{0} request", ["de"] = "{0} Anfrage" },
        ["nut_sign_title"] = new() { ["ar"] = "البيانات الصحية تحتاج إلى حساب", ["en"] = "Health data needs an account", ["de"] = "Gesundheitsdaten brauchen ein Konto" },
        ["nut_sign_body"] = new() { ["ar"] = "أنشئ حساباً مجانياً لحفظ وزنك وخصرك. تبقى قياساتك خاصة وتتبعك على كل أجهزتك.", ["en"] = "Create a free account to save your weight and waist. Your measurements stay private and follow you to every device.", ["de"] = "Erstelle ein kostenloses Konto, um Gewicht und Taille zu speichern. Deine Messwerte bleiben privat und sind auf jedem Gerät verfügbar." },
        ["nut_sign_point_1"] = new() { ["ar"] = "خاصة بك فقط", ["en"] = "Private to you", ["de"] = "Nur für dich sichtbar" },
        ["nut_sign_point_2"] = new() { ["ar"] = "متزامنة بين أجهزتك", ["en"] = "Synced across your devices", ["de"] = "Auf allen Geräten synchron" },
        ["nut_sign_point_3"] = new() { ["ar"] = "يبقى تخطيط الوجبات مجانياً بدون حساب", ["en"] = "Meal planning stays free without an account", ["de"] = "Planen bleibt auch ohne Konto kostenlos" },
        ["nut_not_now"] = new() { ["ar"] = "ليس الآن", ["en"] = "Not now", ["de"] = "Jetzt nicht" },
        ["nut_assistant"] = new() { ["ar"] = "المساعد", ["en"] = "Assistant", ["de"] = "Assistent" },
        ["nut_shopping"] = new() { ["ar"] = "المشتريات", ["en"] = "Shopping", ["de"] = "Einkauf" },
        ["nut_eat_leftover"] = new() { ["ar"] = "تُؤكل كبقايا", ["en"] = "Eat as leftover", ["de"] = "Als Reste essen" },
        ["nut_eat_leftover_hint"] = new() { ["ar"] = "من طبخة سابقة، فلا تُضاف إلى قائمة التسوق.", ["en"] = "From an earlier cook, so it stays off the shopping list.", ["de"] = "Von einem früheren Kochen, kommt also nicht auf die Einkaufsliste." },
        ["nut_retry"] = new() { ["ar"] = "إعادة المحاولة", ["en"] = "Try again", ["de"] = "Erneut versuchen" },
        ["nut_load_error"] = new() { ["ar"] = "تعذّر تحميل خطتك.", ["en"] = "Couldn't load your plan.", ["de"] = "Dein Plan konnte nicht geladen werden." },
        ["nut_servings_less"] = new() { ["ar"] = "حصص أقل", ["en"] = "Fewer servings", ["de"] = "Weniger Portionen" },
        ["nut_servings_more"] = new() { ["ar"] = "حصص أكثر", ["en"] = "More servings", ["de"] = "Mehr Portionen" },
        // {0} = slot name, {1} = meal name
        ["nut_slot_entry"] = new() { ["ar"] = "{0}: {1}", ["en"] = "{0}: {1}", ["de"] = "{0}: {1}" },
        // {0} = day type ("Training day"), {1} = that day's calorie target
        ["nut_day_type_button"] = new() { ["ar"] = "{0} · {1}", ["en"] = "{0} · {1}", ["de"] = "{0} · {1}" },
        ["nut_day_type_switch"] = new() { ["ar"] = "بدّل بين يوم تمرين ويوم راحة", ["en"] = "Switch between training and rest day", ["de"] = "Zwischen Trainings- und Ruhetag wechseln" },
        ["nut_profile_saved"] = new() { ["ar"] = "تم حفظ الملف والأهداف", ["en"] = "Profile and targets saved", ["de"] = "Profil und Ziele gespeichert" },
        ["nut_profile_guest"] = new() { ["ar"] = "تعمل الأهداف بالقيم الافتراضية حتى تسجّل الدخول وتحفظ ملفك.", ["en"] = "Targets use these values until you sign in and save your profile.", ["de"] = "Bis du dich anmeldest und dein Profil speicherst, gelten diese Werte." },
        ["nut_years"] = new() { ["ar"] = "سنة", ["en"] = "years", ["de"] = "Jahre" },
        ["nut_gauge_legend_low"] = new() { ["ar"] = "أقل من 30 غ", ["en"] = "< 30 g", ["de"] = "< 30 g" },
        ["nut_gauge_legend_aim"] = new() { ["ar"] = "30–40 غ", ["en"] = "30–40 g", ["de"] = "30–40 g" },
        ["nut_gauge_legend_high"] = new() { ["ar"] = "أكثر من 40 غ", ["en"] = "> 40 g", ["de"] = "> 40 g" },
        // {0} = grams of protein in this meal
        ["nut_gauge_value"] = new() { ["ar"] = "{0} غ بروتين. الهدف 30–40 غ لكل وجبة", ["en"] = "{0} g protein. Aim for 30–40 g per meal", ["de"] = "{0} g Eiweiß. Ziel: 30–40 g pro Mahlzeit" },
        ["nut_surprise_meal1"] = new() { ["ar"] = "يملأ الوجبة 1 الفارغة في الأيام القادمة من هذا الأسبوع بأطباق عشوائية.", ["en"] = "Fills each empty Meal 1 in the rest of this week with a random dish.", ["de"] = "Füllt jede leere Mahlzeit 1 im Rest der Woche mit einem zufälligen Gericht." },

        // Quick-add recent items and the Week header date (3.0)
        ["nut_quick_nuts"] = new() { ["ar"] = "زبيب ومكسرات", ["en"] = "Raisins and nuts", ["de"] = "Rosinen und Nüsse" },
        ["nut_quick_bar"] = new() { ["ar"] = "لوح بروتين", ["en"] = "Protein bar", ["de"] = "Proteinriegel" },
        ["nut_quick_apple"] = new() { ["ar"] = "تفاحة", ["en"] = "Apple", ["de"] = "Apfel" },
        ["nut_quick_yogurt"] = new() { ["ar"] = "زبادي يوناني", ["en"] = "Greek yogurt", ["de"] = "Griechischer Joghurt" },
        // {0} = day of the month, {1} = month name
        ["nut_day_month"] = new() { ["ar"] = "{0} {1}", ["en"] = "{0} {1}", ["de"] = "{0}. {1}" },

        // Meal details: every slot of the chosen day is filled
        ["nut_nothing_open"] = new() { ["ar"] = "كل الخانات ممتلئة، لا يوجد ما يُخطَّط.", ["en"] = "Every slot is already filled, so there is nothing to plan.", ["de"] = "Alle Slots sind schon belegt, es gibt nichts zu planen." },

        // Progress: weight, waist, weekly check and weekly summary (3.0)
        ["nut_progress"] = new() { ["ar"] = "التقدم", ["en"] = "Progress", ["de"] = "Fortschritt" },
        ["nut_weight_tab"] = new() { ["ar"] = "الوزن", ["en"] = "Weight", ["de"] = "Gewicht" },
        ["nut_summary_tab"] = new() { ["ar"] = "ملخص الأسبوع", ["en"] = "Weekly summary", ["de"] = "Wochenübersicht" },
        ["nut_log_today"] = new() { ["ar"] = "سجّل اليوم", ["en"] = "Log today", ["de"] = "Heute eintragen" },
        ["nut_waist"] = new() { ["ar"] = "الخصر", ["en"] = "Waist", ["de"] = "Taille" },
        ["nut_avg7"] = new() { ["ar"] = "متوسط 7 أيام", ["en"] = "7-day average", ["de"] = "7-Tage-Schnitt" },
        ["nut_raw"] = new() { ["ar"] = "القياسات اليومية", ["en"] = "Daily entries", ["de"] = "Tageswerte" },
        ["nut_plan_line"] = new() { ["ar"] = "المخطط", ["en"] = "Plan", ["de"] = "Plan" },
        ["nut_range_2"] = new() { ["ar"] = "أسبوعان", ["en"] = "2 weeks", ["de"] = "2 Wochen" },
        ["nut_range_4"] = new() { ["ar"] = "4 أسابيع", ["en"] = "4 weeks", ["de"] = "4 Wochen" },
        ["nut_check_on_title"] = new() { ["ar"] = "على المسار الصحيح", ["en"] = "On track", ["de"] = "Im Plan" },
        // {0} = kg lost by the 7-day average this week
        ["nut_check_on_body"] = new() { ["ar"] = "انخفض متوسط 7 أيام بمقدار {0} كغ هذا الأسبوع. المخطط 0.2 إلى 0.5 كغ.", ["en"] = "Your 7-day average fell {0} kg this week. The plan is 0.2 to 0.5 kg.", ["de"] = "Dein 7-Tage-Schnitt ist diese Woche um {0} kg gesunken. Geplant sind 0,2 bis 0,5 kg." },
        ["nut_check_slow_title"] = new() { ["ar"] = "أبطأ من المخطط", ["en"] = "Slower than planned", ["de"] = "Langsamer als geplant" },
        // {0} = kg lost this week
        ["nut_check_slow_body"] = new() { ["ar"] = "النزول {0} كغ فقط مقابل 0.2 إلى 0.5 كغ المخطط. قلّل 150 سعرة أو زد الخطوات.", ["en"] = "Down {0} kg against a plan of 0.2 to 0.5 kg. Reduce by 150 kcal or add steps.", ["de"] = "{0} kg weniger, geplant sind 0,2 bis 0,5 kg. Reduziere um 150 kcal oder geh mehr." },
        ["nut_check_fast_title"] = new() { ["ar"] = "أسرع من المخطط", ["en"] = "Faster than planned", ["de"] = "Schneller als geplant" },
        // {0} = kg lost this week
        ["nut_check_fast_body"] = new() { ["ar"] = "النزول {0} كغ مقابل 0.2 إلى 0.5 كغ المخطط. أضف 150 سعرة.", ["en"] = "Down {0} kg against a plan of 0.2 to 0.5 kg. Add 150 kcal.", ["de"] = "{0} kg weniger, geplant sind 0,2 bis 0,5 kg. Füge 150 kcal hinzu." },
        ["nut_reduce_150"] = new() { ["ar"] = "قلّل 150 سعرة", ["en"] = "Reduce by 150 kcal", ["de"] = "150 kcal reduzieren" },
        ["nut_add_150"] = new() { ["ar"] = "أضف 150 سعرة", ["en"] = "Add 150 kcal", ["de"] = "150 kcal hinzufügen" },
        // {0} = new daily kcal target
        ["nut_adjust_done"] = new() { ["ar"] = "الهدف اليومي الآن {0} سعرة", ["en"] = "Daily target is now {0} kcal", ["de"] = "Tagesziel jetzt {0} kcal" },
        ["nut_not_enough_title"] = new() { ["ar"] = "لا توجد بيانات كافية بعد", ["en"] = "Not enough data yet", ["de"] = "Noch zu wenig Daten" },
        // {0} = entries so far, {1} = entries still needed
        ["nut_not_enough_body"] = new() { ["ar"] = "{0} من 7 قياسات. سجّل {1} أيام أخرى لعرض اتجاه 7 أيام.", ["en"] = "{0} of 7 entries. Log {1} more days to see your 7-day trend.", ["de"] = "{0} von 7 Einträgen. Trage noch {1} Tage ein, dann siehst du deinen 7-Tage-Trend." },
        ["nut_check_starts"] = new() { ["ar"] = "يبدأ الفحص الأسبوعي بعد 14 يوماً من القياسات.", ["en"] = "The weekly check starts after 14 days of entries.", ["de"] = "Der Wochencheck startet nach 14 Tagen mit Einträgen." },
        ["nut_empty_title"] = new() { ["ar"] = "لا توجد قياسات بعد", ["en"] = "No measurements yet", ["de"] = "Noch keine Messwerte" },
        ["nut_empty_body"] = new() { ["ar"] = "زِن نفسك كل صباح في الوقت نفسه. الاتجاه يحتاج إلى أسبوع تقريباً.", ["en"] = "Weigh yourself at the same time each morning. A trend needs about a week.", ["de"] = "Wiege dich jeden Morgen zur selben Zeit. Für einen Trend brauchst du etwa eine Woche." },
        ["nut_first_entry"] = new() { ["ar"] = "سجّل أول وزن", ["en"] = "Log your first weight", ["de"] = "Erstes Gewicht eintragen" },
        ["nut_log_title"] = new() { ["ar"] = "تسجيل القياسات", ["en"] = "Log measurements", ["de"] = "Messwerte eintragen" },
        // {0} = last weight in kg
        ["nut_last"] = new() { ["ar"] = "الأخير: {0} كغ", ["en"] = "Last: {0} kg", ["de"] = "Zuletzt: {0} kg" },
        ["nut_avg_kcal"] = new() { ["ar"] = "متوسط السعرات", ["en"] = "Average calories", ["de"] = "Ø Kalorien" },
        ["nut_avg_protein"] = new() { ["ar"] = "متوسط البروتين", ["en"] = "Average protein", ["de"] = "Ø Eiweiß" },
        ["nut_days_on"] = new() { ["ar"] = "أيام ضمن 10% من الهدف", ["en"] = "Days within 10% of target", ["de"] = "Tage innerhalb von 10 % des Ziels" },
        ["nut_weight_trend"] = new() { ["ar"] = "اتجاه الوزن", ["en"] = "Weight trend", ["de"] = "Gewichtstrend" },
        // {0} = target value
        ["nut_target"] = new() { ["ar"] = "الهدف {0}", ["en"] = "Target {0}", ["de"] = "Ziel {0}" },
        ["nut_last_week"] = new() { ["ar"] = "الأسبوع الماضي", ["en"] = "Last week", ["de"] = "Letzte Woche" },
        ["nut_this_week"] = new() { ["ar"] = "هذا الأسبوع حتى الآن", ["en"] = "This week so far", ["de"] = "Diese Woche bisher" },
        ["nut_on_target"] = new() { ["ar"] = "ضمن الهدف", ["en"] = "On target", ["de"] = "Im Ziel" },
        ["nut_under"] = new() { ["ar"] = "تحت", ["en"] = "Under", ["de"] = "Darunter" },
        ["nut_change_week"] = new() { ["ar"] = "متوسط 7 أيام، التغيّر خلال الأسبوع", ["en"] = "7-day average, change over the week", ["de"] = "7-Tage-Schnitt, Änderung in der Woche" },
        ["nut_trend_title"] = new() { ["ar"] = "التغيّر هذا الأسبوع", ["en"] = "Change this week", ["de"] = "Änderung diese Woche" },
        // {0} = signed change in kg, e.g. "−0.3"
        ["nut_trend_body"] = new() { ["ar"] = "تغيّر متوسط 7 أيام بمقدار {0} كغ هذا الأسبوع.", ["en"] = "Your 7-day average changed by {0} kg this week.", ["de"] = "Dein 7-Tage-Schnitt hat sich diese Woche um {0} kg verändert." },
        // {0} = what is changed ("Weight", "Waist")
        ["nut_decrease"] = new() { ["ar"] = "إنقاص {0}", ["en"] = "Decrease {0}", ["de"] = "{0} verringern" },
        ["nut_increase"] = new() { ["ar"] = "زيادة {0}", ["en"] = "Increase {0}", ["de"] = "{0} erhöhen" },
        // {0} = number of logged days, {1} = days in the period
        ["nut_days_on_count"] = new() { ["ar"] = "{0} / {1}", ["en"] = "{0} / {1}", ["de"] = "{0} / {1}" },
        ["nut_no_data"] = new() { ["ar"] = "لم يُسجَّل", ["en"] = "Not logged", ["de"] = "Nicht erfasst" },
        // {0} = previous average, {1} = current average (kg)
        ["nut_trend_from_to"] = new() { ["ar"] = "{0} ← {1} كغ", ["en"] = "{0} → {1} kg", ["de"] = "{0} → {1} kg" }
    };
}
