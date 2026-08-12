using Forge.Domain.Lifecycle;
using Forge.Domain.Spatial;

namespace Forge.Domain.Creatures;

public enum CreatureFoundationMovementMode
{
    Grounded,
    Flying,
    Swimming,
    Climbing,
    Vehicle,
    ZeroG,
}

public sealed record CreatureFoundationIdentity(
    string Slug,
    IReadOnlyDictionary<string, string> Names,
    IReadOnlyList<string> Aliases,
    string Pronouns,
    IReadOnlyDictionary<string, string> Biographies,
    string AuthorNotes,
    IReadOnlyList<string> NarrativeTags)
{
    internal CreatureFoundationIdentity Normalize() => this with
    {
        Slug = CreaturePresentationProfile.NormalizeIdentifier(Slug),
        Names = NormalizeLocalized(Names),
        Aliases = NormalizeText(Aliases),
        Pronouns = Pronouns?.Trim() ?? string.Empty,
        Biographies = NormalizeLocalized(Biographies),
        AuthorNotes = AuthorNotes?.Trim() ?? string.Empty,
        NarrativeTags = (NarrativeTags ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
    };

    private static IReadOnlyDictionary<string, string> NormalizeLocalized(IReadOnlyDictionary<string, string>? values) =>
        (values ?? new Dictionary<string, string>()).OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key.Trim().ToLowerInvariant(), item => item.Value?.Trim() ?? string.Empty,
                StringComparer.Ordinal);

    private static IReadOnlyList<string> NormalizeText(IReadOnlyList<string>? values) =>
        (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}

public sealed record CreatureFoundationResourcePool(
    Guid Id,
    string ResourceId,
    double Maximum,
    double Current,
    double RegenerationPerSecond,
    bool Persistent)
{
    internal CreatureFoundationResourcePool Normalize() => this with
    {
        ResourceId = CreaturePresentationProfile.NormalizeIdentifier(ResourceId),
    };
}

public sealed record CreatureFoundationMovement(
    CreatureFoundationMovementMode Mode,
    double MaximumSpeed,
    double Acceleration,
    double Deceleration,
    double MinimumAltitude,
    double MaximumAltitude,
    double MaximumSlopeDegrees);

public sealed record CreatureFoundationReferences(
    string AppearanceId,
    string RigId,
    string PerceptionProfileId,
    string CombatProfileId,
    string? DialogueId,
    string InventoryProfileId,
    string FactionId)
{
    internal CreatureFoundationReferences Normalize() => this with
    {
        AppearanceId = CreaturePresentationProfile.NormalizeIdentifier(AppearanceId),
        RigId = CreaturePresentationProfile.NormalizeIdentifier(RigId),
        PerceptionProfileId = CreaturePresentationProfile.NormalizeIdentifier(PerceptionProfileId),
        CombatProfileId = CreaturePresentationProfile.NormalizeIdentifier(CombatProfileId),
        DialogueId = string.IsNullOrWhiteSpace(DialogueId) ? null : CreaturePresentationProfile.NormalizeIdentifier(DialogueId),
        InventoryProfileId = CreaturePresentationProfile.NormalizeIdentifier(InventoryProfileId),
        FactionId = CreaturePresentationProfile.NormalizeIdentifier(FactionId),
    };
}

public sealed record CreatureFoundationProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool RequiredAtRuntime { get; }
    public CreatureFoundationIdentity Identity { get; }
    public TransformValue DefaultTransform { get; }
    public IReadOnlyList<CreatureFoundationResourcePool> Resources { get; }
    public CreatureFoundationMovement Movement { get; }
    public CreatureFoundationReferences References { get; }
    public EventBinding Spawned { get; }
    public IReadOnlyDictionary<string, string> Overrides { get; }
    public CreatureAuthoringCompositionProfile? AuthoringComposition { get; }
    public bool AuthoringCompositionRequiredAtRuntime { get; }
    public CreatureRulesCompositionProfile? RulesComposition { get; }
    public bool RulesCompositionRequiredAtRuntime { get; }
    public CreatureInteractionCompositionProfile? InteractionComposition { get; }
    public bool InteractionCompositionRequiredAtRuntime { get; }
    public CreatureEventAiCompositionProfile? EventAiComposition { get; }
    public bool EventAiCompositionRequiredAtRuntime { get; }

    public CreatureFoundationProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        bool requiredAtRuntime,
        CreatureFoundationIdentity identity,
        TransformValue defaultTransform,
        IReadOnlyList<CreatureFoundationResourcePool>? resources,
        CreatureFoundationMovement movement,
        CreatureFoundationReferences references,
        EventBinding spawned,
        IReadOnlyDictionary<string, string>? overrides,
        CreatureAuthoringCompositionProfile? authoringComposition = null,
        bool authoringCompositionRequiredAtRuntime = false,
        CreatureRulesCompositionProfile? rulesComposition = null,
        bool rulesCompositionRequiredAtRuntime = false,
        CreatureInteractionCompositionProfile? interactionComposition = null,
        bool interactionCompositionRequiredAtRuntime = false,
        CreatureEventAiCompositionProfile? eventAiComposition = null,
        bool eventAiCompositionRequiredAtRuntime = false)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(defaultTransform);
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(spawned);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        RequiredAtRuntime = requiredAtRuntime;
        Identity = identity.Normalize();
        DefaultTransform = defaultTransform;
        Resources = (resources ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.ResourceId, StringComparer.Ordinal).ToArray();
        Movement = movement;
        References = references.Normalize();
        Spawned = spawned with
        {
            Provenance = spawned.Provenance?.Trim() ?? string.Empty,
            ForgeScriptApiVersion = spawned.ForgeScriptApiVersion?.Trim() ?? string.Empty,
            EventName = spawned.EventName?.Trim().ToLowerInvariant() ?? string.Empty,
            Handler = spawned.Handler?.Trim() ?? string.Empty,
        };
        Overrides = (overrides ?? new Dictionary<string, string>()).OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => CreaturePresentationProfile.NormalizeIdentifier(item.Key),
                item => item.Value?.Trim() ?? string.Empty, StringComparer.Ordinal);
        AuthoringComposition = authoringComposition;
        AuthoringCompositionRequiredAtRuntime = authoringCompositionRequiredAtRuntime;
        RulesComposition = rulesComposition;
        RulesCompositionRequiredAtRuntime = rulesCompositionRequiredAtRuntime;
        InteractionComposition = interactionComposition;
        InteractionCompositionRequiredAtRuntime = interactionCompositionRequiredAtRuntime;
        EventAiComposition = eventAiComposition;
        EventAiCompositionRequiredAtRuntime = eventAiCompositionRequiredAtRuntime;
    }

    public static CreatureFoundationProfile CreateDefault(Guid id, string slug, string displayName, string provenance) => new(
        id, CurrentSchemaVersion, provenance, false,
        new(slug, new Dictionary<string, string> { ["fr"] = displayName }, [], "iel",
            new Dictionary<string, string> { ["fr"] = string.Empty }, string.Empty, []),
        TransformValue.Identity,
        [new(Guid.NewGuid(), "health", 100, 100, 0, true), new(Guid.NewGuid(), "stamina", 100, 100, 2, false)],
        new(CreatureFoundationMovementMode.Grounded, 5, 10, 8, 0, 0, 45),
        new("appearance.default", "rig.humanoid", "perception.standard", "combat.standard", null,
            "inventory.standard", "faction.neutral"),
        new(Guid.NewGuid(), 1, provenance, "1.0", "creature.spawned", "OnSpawned", EventAuthorityScope.Server),
        new Dictionary<string, string>());
}

public sealed record CreatureFoundationDiagnostic(string Code, string Path, string Message);

public sealed record CreatureSpawnContextValue(
    Guid SpawnId,
    string AreaId,
    TransformValue Transform,
    int Seed,
    EventAuthorityScope Authority,
    string Cause);

public sealed record CreatureSpawnedValue(
    Guid EventId,
    Guid ProfileId,
    string BlueprintId,
    CreatureSpawnContextValue Context,
    IReadOnlyDictionary<string, double> ResourceState);
