using Forge.Domain.Quests;

namespace Forge.Application.Gameplay;

public sealed record RuntimeQuestAdvance(
    QuestDefinition Quest,
    QuestTransition Transition,
    QuestTransitionResult Result);
