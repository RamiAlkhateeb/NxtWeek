using System;
using System.Collections.Generic;

namespace MealPlanner.Shared.Models;

public class Meal
{
    public string Id { get; set; } = string.Empty;   // ISO date, e.g. "2026-02-02"
    public DateOnly Date { get; set; }
    public string DayName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }

    // New catalog/user properties
    public string MealId { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public List<string> Ingredients { get; set; } = new();
    public List<string> SideDishes { get; set; } = new();
    public Cuisine? Cuisine { get; set; }
    public MealType? MealType { get; set; }
}