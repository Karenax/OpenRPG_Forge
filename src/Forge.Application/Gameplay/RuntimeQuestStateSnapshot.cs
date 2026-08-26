using Forge.Domain.Quests;

namespace Forge.Application.Gameplay;

public sealed record RuntimeQuestStateSnapshot(
    QuestId QuestId,
    QuestStateId StateId,
    bool IsAccepted);
