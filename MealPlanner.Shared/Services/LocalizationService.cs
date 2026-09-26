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
        MealType.Vegan => this["meal_vegan"],
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
        ["nav_settings"] = new() { ["ar"] = "الإعدادات", ["en"] = "Settings", ["de"] = "Einstellungen" },

        // Settings
        ["settings_title"] = new() { ["ar"] = "الإعدادات", ["en"] = "Settings", ["de"] = "Einstellungen" },
        ["settings_subtitle"] = new() { ["ar"] = "كل ما تحتاجه في مكان واحد", ["en"] = "Everything you need in one place", ["de"] = "Alles an einem Ort" },
        ["settings_lang_title"] = new() { ["ar"] = "🌐 لغة التطبيق", ["en"] = "🌐 App Language", ["de"] = "🌐 App-Sprache" },
        ["settings_lang_desc"] = new() { ["ar"] = "اختر لغة الواجهة والاتجاه المفضل لديك.", ["en"] = "Choose your preferred interface language and direction.", ["de"] = "Wähle deine bevorzugte Sprache und Schreibrichtung." },
        ["lang_ar"] = new() { ["ar"] = "العربية", ["en"] = "العربية", ["de"] = "العربية" },
        ["lang_en"] = new() { ["ar"] = "English", ["en"] = "English", ["de"] = "English" },
        ["lang_de"] = new() { ["ar"] = "Deutsch", ["en"] = "Deutsch", ["de"] = "Deutsch" },
        ["settings_username_title"] = new() { ["ar"] = "👤 اسم المستخدم", ["en"] = "👤 Username", ["de"] = "👤 Benutzername" },
        ["settings_username_prompt"] = new() { ["ar"] = "اختر اسماً قبل إرسال أو قبول طلبات الصداقة.", ["en"] = "Choose a name before sending or accepting friend requests.", ["de"] = "Wähle einen Namen, bevor du Freundschaftsanfragen sendest oder annimmst." },
        ["settings_username_change"] = new() { ["ar"] = "تغيير اسم المستخدم", ["en"] = "Change username", ["de"] = "Benutzernamen ändern" },
        ["settings_username_choose"] = new() { ["ar"] = "اختر اسم مستخدم", ["en"] = "Choose username", ["de"] = "Benutzernamen wählen" },
        ["settings_username_placeholder"] = new() { ["ar"] = "مثال: rami_food", ["en"] = "e.g. rami_food", ["de"] = "z. B. rami_food" },
        ["settings_username_available"] = new() { ["ar"] = "اسم المستخدم متاح ✓", ["en"] = "Username is available ✓", ["de"] = "Benutzername ist frei ✓" },
        ["settings_username_taken"] = new() { ["ar"] = "اسم المستخدم مستخدم بالفعل.", ["en"] = "Username is already taken.", ["de"] = "Dieser Benutzername ist bereits vergeben." },
        ["settings_username_saved"] = new() { ["ar"] = "تم حفظ اسم المستخدم ✨", ["en"] = "Username saved successfully ✨", ["de"] = "Benutzername gespeichert ✨" },
        ["settings_username_copied"] = new() { ["ar"] = "تم نسخ اسم المستخدم.", ["en"] = "Username copied.", ["de"] = "Benutzername kopiert." },
        ["settings_recover_title"] = new() { ["ar"] = "🔁 استرجاع خطتي", ["en"] = "🔁 Recover My Plan", ["de"] = "🔁 Plan wiederherstellen" },
        ["settings_recover_desc"] = new() { ["ar"] = "هل مسحت بيانات المتصفح أو غيّرت جهازك؟ استعد خططك ووجباتك باسم المستخدم الذي اخترته سابقاً.", ["en"] = "Cleared browser data or changed devices? Recover your plan and meals with your username.", ["de"] = "Browserdaten gelöscht oder Gerät gewechselt? Hol dir deinen Plan mit deinem Benutzernamen zurück." },
        ["settings_recover_link"] = new() { ["ar"] = "استرجاع باسم المستخدم ←", ["en"] = "Recover by username →", ["de"] = "Mit Benutzernamen wiederherstellen →" },
        ["settings_install_title"] = new() { ["ar"] = "📲 تثبيت على الشاشة الرئيسية", ["en"] = "📲 Install on Home Screen", ["de"] = "📲 Zum Startbildschirm hinzufügen" },
        ["settings_install_desc"] = new() { ["ar"] = "أضف مكدوس إلى شاشة هاتفك الرئيسية لفتحه كتطبيق سريع على أندرويد وiPhone.", ["en"] = "Add Makdous to your home screen for quick access on Android & iPhone.", ["de"] = "Füge Makdous zu deinem Startbildschirm hinzu — schneller Zugriff auf Android und iPhone." },
        ["settings_install_link"] = new() { ["ar"] = "كيف أثبّت مكدوس؟ ←", ["en"] = "How to install Makdous? →", ["de"] = "Wie installiere ich Makdous? →" },
        ["settings_about_title"] = new() { ["ar"] = "ℹ️ حول التطبيق", ["en"] = "ℹ️ About", ["de"] = "ℹ️ Über die App" },
        ["settings_about_desc"] = new() { ["ar"] = "مكدوس — مخطط وجبات شخصي", ["en"] = "Makdous — Personal Weekly Meal Planner", ["de"] = "Makdous — dein persönlicher Wochen-Essensplaner" },
        ["settings_about_version"] = new() { ["ar"] = "الإصدار 1.4", ["en"] = "Version 1.4", ["de"] = "Version 1.4" },
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
        ["week_prev"] = new() { ["ar"] = "الأسبوع السابق", ["en"] = "Previous Week", ["de"] = "Vorige Woche" },
        ["week_next"] = new() { ["ar"] = "الأسبوع القادم", ["en"] = "Next Week", ["de"] = "Nächste Woche" },
        ["week_nudge_prompt"] = new() { ["ar"] = "اختر اسم مستخدم لحفظ خطتك بشكل دائم ومشاركتها مع أصدقائك", ["en"] = "Choose a username to permanently save your plan and share with friends", ["de"] = "Wähle einen Benutzernamen, um deinen Plan dauerhaft zu speichern und zu teilen" },

        // Month page
        ["month_back_settings"] = new() { ["ar"] = "العودة إلى الإعدادات", ["en"] = "Back to Settings", ["de"] = "Zurück zu den Einstellungen" },
        ["month_prev"] = new() { ["ar"] = "الشهر السابق", ["en"] = "Previous Month", ["de"] = "Voriger Monat" },
        ["month_next"] = new() { ["ar"] = "الشهر القادم", ["en"] = "Next Month", ["de"] = "Nächster Monat" },

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
        ["meal_vegan"] = new() { ["ar"] = "نباتي صرف", ["en"] = "Vegan", ["de"] = "Vegan" },
        ["meals_empty_title"] = new() { ["ar"] = "لم يتم العثور على وجبات", ["en"] = "No meals found", ["de"] = "Keine Gerichte gefunden" },
        ["meals_empty_desc"] = new() { ["ar"] = "جرّب تغيير كلمات البحث أو الفلاتر", ["en"] = "Try changing your search terms or filters", ["de"] = "Ändere deine Suchbegriffe oder Filter" },
        ["meals_add_custom"] = new() { ["ar"] = "إضافة وجبة جديدة", ["en"] = "Add New Meal", ["de"] = "Neues Gericht anlegen" },
        ["meals_new_meal"] = new() { ["ar"] = "وجبة جديدة", ["en"] = "New Meal", ["de"] = "Neues Gericht" },
        ["meals_new_meal_eyebrow"] = new() { ["ar"] = "أضفها مرة واحدة لتصبح متاحة للجميع", ["en"] = "Add it once to make it available to all", ["de"] = "Einmal anlegen — für alle verfügbar" },
        ["meals_lang_all"] = new() { ["ar"] = "جميع اللغات", ["en"] = "All Languages", ["de"] = "Alle Sprachen" },
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
        ["meal_not_found"] = new() { ["ar"] = "لم يتم العثور على تفاصيل الوجبة.", ["en"] = "Meal details not found.", ["de"] = "Details zum Gericht nicht gefunden." },
        ["meal_back_home"] = new() { ["ar"] = "العودة للرئيسية", ["en"] = "Back to Home", ["de"] = "Zurück zur Startseite" },

        // Selected day header
        ["selectedday_add_to"] = new() { ["ar"] = "إضافة وجبة إلى", ["en"] = "Add meal to", ["de"] = "Gericht hinzufügen für" },

        // Shopping
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
        ["friends_discoverable"] = new() { ["ar"] = "أشخاص على مكدوس", ["en"] = "People on Makdous", ["de"] = "Leute bei Makdous" }
    };
}
