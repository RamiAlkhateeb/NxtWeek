namespace MealPlanner.Shared.Services;

public interface IDataExportService { Task<string> ExportCurrentWeekAsync(); }
public interface IDataImportService { Task ClearLocalDataAsync(); }
