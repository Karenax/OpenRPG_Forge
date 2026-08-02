using Forge.Domain.Blueprints;

namespace Forge.Domain.Dialogues;

public sealed record DialogueNode
{
    public DialogueNodeId Id { get; }

    public BlueprintId? SpeakerBlueprintId { get; }

    public LocalizedText Text { get; }

    public IReadOnlyList<DialogueAction> OnEnterActions { get; }

    public IReadOnlyList<DialogueChoice> Choices { get; }

    public DialogueNode(
        DialogueNodeId id,
        BlueprintId? speakerBlueprintId,
        LocalizedText text,
        IReadOnlyList<DialogueAction>? onEnterActions = null,
        IReadOnlyList<DialogueChoice>? choices = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(text);
        var materializedChoices = (choices ?? []).ToArray();
        if (materializedChoices.Select(choice => choice.Id).Distinct().Count() != materializedChoices.Length)
        {
            throw new ArgumentException("A dialogue node cannot contain duplicate choice IDs.", nameof(choices));
        }

        Id = id;
        SpeakerBlueprintId = speakerBlueprintId;
        Text = text;
        OnEnterActions = (onEnterActions ?? []).ToArray();
        Choices = materializedChoices;
    }
}
