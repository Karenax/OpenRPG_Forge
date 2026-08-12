using Forge.Domain.Dialogues;
using Forge.Domain.Lifecycle;
using Forge.Domain.Spatial;

namespace Forge.Domain.Creatures;

public enum CreatureInteractionSenseKind
{
    Vision,
    Hearing,
    Smell,
    Magic,
}

public sealed record CreatureInteractionFactionMembership(
    Guid Id,
    Guid FactionId,
    double Weight,
    string Rank,
    double PersonalReputation,
    bool Inherited)
{
    internal CreatureInteractionFactionMembership Normalize() => this with
    {
        Rank = CreaturePresentationProfile.NormalizeIdentifier(Rank),
    };
}

public sealed record CreatureInteractionFactionProfile(
    Guid Id,
    Guid PrimaryFactionId,
    IReadOnlyList<CreatureInteractionFactionMembership> Memberships)
{
    internal CreatureInteractionFactionProfile Normalize() => this with
    {
        Memberships = (Memberships ?? []).Select(item => item.Normalize())
            .OrderByDescending(item => item.Weight).ThenBy(item => item.FactionId).ThenBy(item => item.Id).ToArray(),
    };
}

public sealed record CreatureInteractionSenseProfile(
    Guid Id,
    CreatureInteractionSenseKind Kind,
    double Range,
    double ConeDegrees,
    bool UsesOcclusion,
    IReadOnlyList<string> StimulusTags)
{
    internal CreatureInteractionSenseProfile Normalize() => this with
    {
        StimulusTags = (StimulusTags ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
    };
}

public sealed record CreatureInteractionPerceptionProfile(
    Guid Id,
    IReadOnlyList<CreatureInteractionSenseProfile> Senses,
    double MemorySeconds)
{
    internal CreatureInteractionPerceptionProfile Normalize() => this with
    {
        Senses = (Senses ?? []).Select(item => item.Normalize()).OrderBy(item => item.Kind).ThenBy(item => item.Id).ToArray(),
    };
}

public sealed record CreatureInteractionDialogueBinding(
    Guid Id,
    string Context,
    int Priority,
    DialogueId DialogueId,
    string? InterlocutorId,
    string? RequiredState,
    bool Fallback)
{
    internal CreatureInteractionDialogueBinding Normalize() => this with
    {
        Context = CreaturePresentationProfile.NormalizeIdentifier(Context),
        InterlocutorId = NormalizeOptional(InterlocutorId),
        RequiredState = NormalizeOptional(RequiredState),
    };

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : CreaturePresentationProfile.NormalizeIdentifier(value);
}

public sealed record CreatureInteractionProtectionPolicy(
    Guid Id,
    bool Invulnerable,
    bool UsesNarrativeDeath,
    bool DialogueInterruptible,
    IReadOnlyList<string> GameMasterPermissions)
{
    internal CreatureInteractionProtectionPolicy Normalize() => this with
    {
        GameMasterPermissions = (GameMasterPermissions ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
    };
}

public sealed record CreatureInteractionCompositionProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public string AssetId { get; }
    public TransformValue PreviewTransform { get; }
    public CreatureInteractionFactionProfile Factions { get; }
    public CreatureInteractionPerceptionProfile Perception { get; }
    public IReadOnlyList<CreatureInteractionDialogueBinding> DialogueBindings { get; }
    public CreatureInteractionProtectionPolicy Protection { get; }
    public EventBinding OnConversation { get; }
    public IReadOnlyDictionary<string, string> Overrides { get; }

    public CreatureInteractionCompositionProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        string assetId,
        TransformValue previewTransform,
        CreatureInteractionFactionProfile factions,
        CreatureInteractionPerceptionProfile perception,
        IReadOnlyList<CreatureInteractionDialogueBinding>? dialogueBindings,
        CreatureInteractionProtectionPolicy protection,
        EventBinding onConversation,
        IReadOnlyDictionary<string, string>? overrides)
    {
        ArgumentNullException.ThrowIfNull(previewTransform);
        ArgumentNullException.ThrowIfNull(factions);
        ArgumentNullException.ThrowIfNull(perception);
        ArgumentNullException.ThrowIfNull(protection);
        ArgumentNullException.ThrowIfNull(onConversation);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        AssetId = CreaturePresentationProfile.NormalizeIdentifier(assetId);
        PreviewTransform = previewTransform;
        Factions = factions.Normalize();
        Perception = perception.Normalize();
        DialogueBindings = (dialogueBindings ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.Context, StringComparer.Ordinal).ThenByDescending(item => item.Priority)
            .ThenBy(item => item.DialogueId.ToString(), StringComparer.Ordinal).ThenBy(item => item.Id).ToArray();
        Protection = protection.Normalize();
        OnConversation = onConversation with
        {
            Provenance = onConversation.Provenance?.Trim() ?? string.Empty,
            ForgeScriptApiVersion = onConversation.ForgeScriptApiVersion?.Trim() ?? string.Empty,
            EventName = onConversation.EventName?.Trim().ToLowerInvariant() ?? string.Empty,
            Handler = onConversation.Handler?.Trim() ?? string.Empty,
        };
        Overrides = (overrides ?? new Dictionary<string, string>()).OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => CreaturePresentationProfile.NormalizeIdentifier(item.Key),
                item => item.Value?.Trim() ?? string.Empty, StringComparer.Ordinal);
    }
}

public sealed record CreatureInteractionDialogueDecision(
    bool Accepted,
    CreatureInteractionDialogueBinding? Binding,
    string? RefusalCode);
