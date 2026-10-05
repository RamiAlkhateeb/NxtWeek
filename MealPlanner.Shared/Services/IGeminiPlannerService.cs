using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public sealed record ChatTurn(bool IsUser, string Text);
public sealed record ParsedMealEntry(DateOnly Date, string Name, MealType MealType, List<string> Ingredients);
public sealed record AssistantProposal(List<ParsedMealEntry> Meals, List<string> ShoppingItems);
public sealed record AssistantReply(string Message, AssistantProposal? Proposal, string ModelUsed);

public interface IGeminiPlannerService
{
    Task<List<string>> ListAvailableModelsAsync();

    /// <summary>
    /// One conversational turn. <paramref name="planContext"/> is a plain-text summary of
    /// the user's upcoming plan so the assistant can avoid taken days. A proposal is only
    /// ever a suggestion — callers must not write it until the user confirms.
    /// </summary>
    Task<AssistantReply> ChatAsync(IReadOnlyList<ChatTurn> history, string planContext);
}
