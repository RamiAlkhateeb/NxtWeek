using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public sealed record ChatTurn(bool IsUser, string Text);
public sealed record ParsedMealEntry(DateOnly Date, string Name, MealType MealType, List<string> Ingredients);

/// <summary>A set of plan/shopping changes the assistant proposed or the user agreed to.</summary>
public sealed record PlanChanges(List<ParsedMealEntry> Meals, List<string> ShoppingItems, List<string> RemoveShoppingItems)
{
    public bool IsEmpty => Meals.Count == 0 && ShoppingItems.Count == 0 && RemoveShoppingItems.Count == 0;
}

public enum AssistantAction
{
    /// <summary>Still talking: suggesting dishes, asking a question. Nothing to save.</summary>
    Chat,
    /// <summary>Concrete changes are on the table and the user is asked to confirm them.</summary>
    Propose,
    /// <summary>The user agreed to the pending proposal — save it now.</summary>
    Apply,
    /// <summary>The user turned the pending proposal down.</summary>
    Cancel
}

public sealed record AssistantReply(string Message, AssistantAction Action, PlanChanges Changes);

public interface IGeminiPlannerService
{
    /// <summary>
    /// One conversational turn. The assistant discusses first and only returns
    /// <see cref="AssistantAction.Apply"/> after the user has agreed to a pending proposal.
    /// </summary>
    /// <param name="history">The conversation so far, ending with the user's latest message.</param>
    /// <param name="pending">The proposal currently waiting for the user's answer, if any.</param>
    /// <param name="planContext">The coming days of the plan (date: meal or FREE).</param>
    /// <param name="currentShoppingItems">Names already on the shopping list, so removals use exact names.</param>
    Task<AssistantReply> ChatAsync(IReadOnlyList<ChatTurn> history, PlanChanges? pending, string planContext,
        IReadOnlyList<string> currentShoppingItems);
}
