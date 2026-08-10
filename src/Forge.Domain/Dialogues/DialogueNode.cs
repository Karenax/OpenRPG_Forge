using Forge.Domain.Blueprints;
using System.Text.Json.Serialization;

namespace Forge.Domain.Dialogues;

public sealed record DialogueNode
{
    public DialogueNodeId Id { get; }

    public BlueprintId? SpeakerBlueprintId { get; }

    public LocalizedText Text { get; }

    public IReadOnlyList<DialogueAction> OnEnterActions { get; }

    public IReadOnlyList<DialogueChoice> Choices { get; }

    public DialogueNodeRole Role { get; }

    public Guid ParticipantId { get; }

    public string? SubgraphKey { get; }

    [JsonConstructor]
    public DialogueNode(
        DialogueNodeId id,
        BlueprintId? speakerBlueprintId,
        LocalizedText text,
        IReadOnlyList<DialogueAction>? onEnterActions,
        IReadOnlyList<DialogueChoice>? choices,
        DialogueNodeRole role,
        Guid participantId,
        string? subgraphKey)
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
        Role = role == DialogueNodeRole.Unspecified
            ? speakerBlueprintId is null ? DialogueNodeRole.Narration : DialogueNodeRole.SpeakerLine
            : role;
        ParticipantId = participantId == Guid.Empty
            ? DialogueParticipant.FromSpeaker(speakerBlueprintId).Id
            : participantId;
        SubgraphKey = string.IsNullOrWhiteSpace(subgraphKey) ? null : subgraphKey.Trim();
    }

    public DialogueNode(
        DialogueNodeId id,
        BlueprintId? speakerBlueprintId,
        LocalizedText text,
        IReadOnlyList<DialogueAction>? onEnterActions = null,
        IReadOnlyList<DialogueChoice>? choices = null,
        DialogueNodeRole role = DialogueNodeRole.Unspecified,
        string? subgraphKey = null)
        : this(id, speakerBlueprintId, text, onEnterActions, choices, role, Guid.Empty, subgraphKey)
    {
    }
}
