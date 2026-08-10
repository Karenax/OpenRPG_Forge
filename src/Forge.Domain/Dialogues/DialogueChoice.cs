namespace Forge.Domain.Dialogues;

public sealed record DialogueChoice
{
    public DialogueChoiceId Id { get; }

    public LocalizedText Text { get; }

    public IReadOnlyList<DialogueCondition> Conditions { get; }

    public IReadOnlyList<DialogueAction> Actions { get; }

    public DialogueNodeId? TargetNodeId { get; }

    public DialogueEdgeKind EdgeKind { get; }

    public int Priority { get; }

    public DialogueConditionCombination ConditionCombination { get; }

    public DialogueChoice(
        DialogueChoiceId id,
        LocalizedText text,
        IReadOnlyList<DialogueCondition>? conditions = null,
        IReadOnlyList<DialogueAction>? actions = null,
        DialogueNodeId? targetNodeId = null,
        DialogueEdgeKind edgeKind = DialogueEdgeKind.Flow,
        int priority = 0,
        DialogueConditionCombination conditionCombination = DialogueConditionCombination.All)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(text);
        Id = id;
        Text = text;
        Conditions = (conditions ?? []).ToArray();
        Actions = (actions ?? []).ToArray();
        TargetNodeId = targetNodeId;
        EdgeKind = edgeKind;
        Priority = priority;
        ConditionCombination = conditionCombination;
    }
}
