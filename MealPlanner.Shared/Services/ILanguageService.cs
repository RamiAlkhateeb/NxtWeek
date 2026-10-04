namespace MealPlanner.Shared.Services;
public interface ILanguageService { Task<string> GetAsync(); Task SetAsync(string language); }
