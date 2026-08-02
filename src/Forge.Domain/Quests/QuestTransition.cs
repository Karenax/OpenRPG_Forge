namespace Forge.Domain.Quests;

public sealed record QuestTransition
{
    public QuestTransitionId Id { get; }

    public QuestStateId FromStateId { get; }

    public QuestStateId ToStateId { get; }

    public IReadOnlyList<QuestCondition> Conditions { get; }

    public IReadOnlyList<QuestAction> Actions { get; }

    public QuestTransition(
        QuestTransitionId id,
        QuestStateId fromStateId,
        QuestStateId toStateId,
        IReadOnlyList<QuestCondition>? conditions = null,
        IReadOnlyList<QuestAction>? actions = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(fromStateId);
        ArgumentNullException.ThrowIfNull(toStateId);
        Id = id;
        FromStateId = fromStateId;
        ToStateId = toStateId;
        Conditions = (conditions ?? []).ToArray();
        Actions = (actions ?? []).ToArray();
    }
}
