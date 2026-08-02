namespace Forge.Domain.Dialogues;

public sealed record DialogueChoice
{
    public DialogueChoiceId Id { get; }

    public LocalizedText Text { get; }

    public IReadOnlyList<DialogueCondition> Conditions { get; }

    public IReadOnlyList<DialogueAction> Actions { get; }

    public DialogueNodeId? TargetNodeId { get; }

    public DialogueChoice(
        DialogueChoiceId id,
        LocalizedText text,
        IReadOnlyList<DialogueCondition>? conditions = null,
        IReadOnlyList<DialogueAction>? actions = null,
        DialogueNodeId? targetNodeId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(text);
        Id = id;
        Text = text;
        Conditions = (conditions ?? []).ToArray();
        Actions = (actions ?? []).ToArray();
        TargetNodeId = targetNodeId;
    }
}
