using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Dialogues;

public enum DialogueTextTokenType
{
    Text,
    Integer,
    Decimal,
    Boolean,
    EntityName,
    LocalizedText,
}

public enum DialogueParticipantBindingKind
{
    Role,
    Query,
    Tag,
    Entity,
}

public enum DialogueChoiceEvaluationMode
{
    FirstValid,
    AllValid,
    Weighted,
    RandomDeterministic,
    Manual,
}

public enum DialogueNodeEditorialStatus
{
    Draft,
    Review,
    Approved,
    Deprecated,
}

public enum DialogueWizardEntryKind
{
    Condition,
    Action,
}

public sealed record DialogueTextToken(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Name,
    DialogueTextTokenType Type,
    bool Required,
    string? DefaultValue)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueParticipantBinding(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Role,
    DialogueParticipantBindingKind Kind,
    string Value,
    string? FallbackRole)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueChoiceEvaluation(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DialogueChoiceId ChoiceId,
    double Weight)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueWizardEntry(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DialogueWizardEntryKind Kind,
    Guid TargetId,
    string SourceDescription)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueNodeEditorialNotes(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Intent,
    string Emotion,
    string Source,
    DialogueNodeEditorialStatus Status,
    IReadOnlyList<string> Todos,
    string AuthorMention)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueNodeAuthoring(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DialogueNodeId NodeId,
    string SpeakerRole,
    DialogueChoiceEvaluationMode EvaluationMode,
    IReadOnlyList<DialogueTextToken> Tokens,
    IReadOnlyList<DialogueChoiceEvaluation> ChoiceEvaluations,
    IReadOnlyList<DialogueWizardEntry> WizardEntries,
    DialogueNodeEditorialNotes Notes)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueAuthoringProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    IReadOnlyList<DialogueParticipantBinding> Participants,
    IReadOnlyList<DialogueNodeAuthoring> Nodes)
{
    public const int CurrentSchemaVersion = 1;

    public static DialogueAuthoringProfile CreateDefault(DialogueDefinition dialogue, string provenance)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var participantRoles = dialogue.Participants.Select(item => item.Role).ToHashSet(StringComparer.Ordinal);
        var participants = dialogue.Participants.Select(item => new DialogueParticipantBinding(
            StableId($"{dialogue.Id}:participant:{item.Role}"),
            DialogueParticipantBinding.CurrentSchemaVersion,
            provenance,
            item.Role,
            item.BlueprintId is null ? DialogueParticipantBindingKind.Role : DialogueParticipantBindingKind.Entity,
            item.BlueprintId?.ToString() ?? item.Role,
            item.Role != "narrator" && participantRoles.Contains("narrator") ? "narrator" : null)).ToArray();
        var nodes = dialogue.Nodes.Select(node => new DialogueNodeAuthoring(
            StableId($"{dialogue.Id}:node:{node.Id}"),
            DialogueNodeAuthoring.CurrentSchemaVersion,
            provenance,
            node.Id,
            dialogue.Participants.Single(item => item.Id == node.ParticipantId).Role,
            DialogueChoiceEvaluationMode.FirstValid,
            [],
            node.Choices.Select(choice => new DialogueChoiceEvaluation(
                StableId($"{dialogue.Id}:choice-evaluation:{choice.Id}"),
                DialogueChoiceEvaluation.CurrentSchemaVersion,
                provenance,
                choice.Id,
                1)).ToArray(),
            [],
            new DialogueNodeEditorialNotes(
                StableId($"{dialogue.Id}:notes:{node.Id}"),
                DialogueNodeEditorialNotes.CurrentSchemaVersion,
                provenance,
                "Clarifier l’intention de cette réplique.",
                "neutral",
                "authoring:migration",
                DialogueNodeEditorialStatus.Draft,
                [],
                "@author"))).ToArray();
        return new(StableId($"{dialogue.Id}:authoring"), CurrentSchemaVersion, provenance, true, false,
            participants, nodes);
    }

    internal static Guid StableId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:dialogue-authoring:{value}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}

public static class DialogueScriptWizard
{
    public static (DialogueCondition Value, DialogueWizardEntry Source) BuildCondition(
        DialogueConditionKind kind,
        string key,
        string expectedValue,
        bool negated,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var condition = new DialogueCondition(Guid.NewGuid(), DialogueCondition.CurrentSchemaVersion, provenance,
            kind, key.Trim(), expectedValue.Trim(), negated, "on-choice-visible", 1);
        var source = new DialogueWizardEntry(Guid.NewGuid(), DialogueWizardEntry.CurrentSchemaVersion, provenance,
            DialogueWizardEntryKind.Condition, condition.Id,
            $"builder:condition:{kind}:{key.Trim()}:{expectedValue.Trim()}:{negated.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}");
        return (condition, source);
    }

    public static (DialogueAction Value, DialogueWizardEntry Source) BuildAction(
        DialogueActionKind kind,
        string key,
        string value,
        bool required,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var action = new DialogueAction(Guid.NewGuid(), DialogueAction.CurrentSchemaVersion, provenance,
            kind, key.Trim(), value.Trim(), required);
        var source = new DialogueWizardEntry(Guid.NewGuid(), DialogueWizardEntry.CurrentSchemaVersion, provenance,
            DialogueWizardEntryKind.Action, action.Id,
            $"builder:action:{kind}:{key.Trim()}:{value.Trim()}:{required.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}");
        return (action, source);
    }
}
