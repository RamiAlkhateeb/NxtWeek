namespace MealPlanner.Shared.Services;
public interface IThemeService { Task<string> GetAsync(); Task SetAsync(string theme); }
public interface ILanguageService { Task<string> GetAsync(); Task SetAsync(string language); }
