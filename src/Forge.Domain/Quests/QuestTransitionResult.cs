namespace Forge.Domain.Quests;

public sealed record QuestTransitionResult(
    QuestStateDefinition State,
    IReadOnlyList<QuestAction> Actions,
    bool Completed);
