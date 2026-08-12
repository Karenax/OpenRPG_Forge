using Forge.Domain.Spatial;

namespace Forge.Domain.Creatures;

public enum CreatureAuthoringEncounterRole
{
    Minion,
    Skirmisher,
    Defender,
    Controller,
    Support,
    Boss,
    NonCombatant,
}
public sealed record CreatureSpeciesTraitSet(string Id, IReadOnlyList<string> Traits)
{
    internal CreatureSpeciesTraitSet Normalize() => this with
    {
        Id = CreaturePresentationProfile.NormalizeIdentifier(Id),
        Traits = (Traits ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
    };
}

public sealed record CreatureSpeciesProfile(
    string SpeciesId,
    IReadOnlyList<string> ParentSpeciesIds,
    IReadOnlyList<CreatureSpeciesTraitSet> TraitSets,
    IReadOnlyList<string> Variants,
    string RequiredSkeletonId)
{
    internal CreatureSpeciesProfile Normalize() => this with
    {
        SpeciesId = CreaturePresentationProfile.NormalizeIdentifier(SpeciesId),
        ParentSpeciesIds = (ParentSpeciesIds ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        TraitSets = (TraitSets ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        Variants = (Variants ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        RequiredSkeletonId = CreaturePresentationProfile.NormalizeIdentifier(RequiredSkeletonId),
    };
}

public sealed record CreatureAppearanceProfile(
    string VisualArchetypeId,
    string MeshAssetId,
    string RigId,
    IReadOnlyDictionary<string, double> Morphs,
    IReadOnlyDictionary<string, string> BodySlots,
    IReadOnlyList<string> MaterialIds,
    IReadOnlyList<string> PaletteIds,
    IReadOnlyList<string> AttachmentIds,
    string PresetId)
{
    internal CreatureAppearanceProfile Normalize() => this with
    {
        VisualArchetypeId = CreaturePresentationProfile.NormalizeIdentifier(VisualArchetypeId),
        MeshAssetId = CreaturePresentationProfile.NormalizeIdentifier(MeshAssetId),
        RigId = CreaturePresentationProfile.NormalizeIdentifier(RigId),
        Morphs = (Morphs ?? new Dictionary<string, double>()).OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => CreaturePresentationProfile.NormalizeIdentifier(item.Key), item => item.Value,
                StringComparer.Ordinal),
        BodySlots = (BodySlots ?? new Dictionary<string, string>()).OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => CreaturePresentationProfile.NormalizeIdentifier(item.Key),
                item => CreaturePresentationProfile.NormalizeIdentifier(item.Value), StringComparer.Ordinal),
        MaterialIds = NormalizeIdentifiers(MaterialIds),
        PaletteIds = NormalizeIdentifiers(PaletteIds),
        AttachmentIds = NormalizeIdentifiers(AttachmentIds),
        PresetId = CreaturePresentationProfile.NormalizeIdentifier(PresetId),
    };

    private static IReadOnlyList<string> NormalizeIdentifiers(IReadOnlyList<string>? values) =>
        (values ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}

public sealed record CreatureAnimationState(Guid Id, string StateId, string Category, string ClipAssetId)
{
    internal CreatureAnimationState Normalize() => this with
    {
        StateId = CreaturePresentationProfile.NormalizeIdentifier(StateId),
        Category = CreaturePresentationProfile.NormalizeIdentifier(Category),
        ClipAssetId = CreaturePresentationProfile.NormalizeIdentifier(ClipAssetId),
    };
}

public sealed record CreatureAnimationTransition(
    Guid Id,
    string FromStateId,
    string ToStateId,
    string ConditionId)
{
    internal CreatureAnimationTransition Normalize() => this with
    {
        FromStateId = CreaturePresentationProfile.NormalizeIdentifier(FromStateId),
        ToStateId = CreaturePresentationProfile.NormalizeIdentifier(ToStateId),
        ConditionId = CreaturePresentationProfile.NormalizeIdentifier(ConditionId),
    };
}

public sealed record CreatureAnimationGraphProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<CreatureAnimationState> States,
    IReadOnlyList<CreatureAnimationTransition> Transitions,
    string RetargetProfileId)
{
    public const int CurrentSchemaVersion = 1;

    internal CreatureAnimationGraphProfile Normalize() => this with
    {
        Provenance = Provenance?.Trim() ?? string.Empty,
        States = (States ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.StateId, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray(),
        Transitions = (Transitions ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.FromStateId, StringComparer.Ordinal)
            .ThenBy(item => item.ToStateId, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray(),
        RetargetProfileId = CreaturePresentationProfile.NormalizeIdentifier(RetargetProfileId),
    };
}

public sealed record CreatureAuthoringCompositionProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public string AssetId { get; }
    public TransformValue PreviewTransform { get; }
    public string ArchetypeId { get; }
    public string RulesetId { get; }
    public string RigId { get; }
    public string FactionId { get; }
    public CreatureAuthoringEncounterRole EncounterRole { get; }
    public double ThreatRating { get; }
    public string VisualKitId { get; }
    public IReadOnlyList<string> CombatTags { get; }
    public CreatureSpeciesProfile Species { get; }
    public CreatureAppearanceProfile Appearance { get; }
    public CreatureAnimationGraphProfile AnimationGraph { get; }
    public IReadOnlyDictionary<string, string> Overrides { get; }

    public CreatureAuthoringCompositionProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        string assetId,
        TransformValue previewTransform,
        string archetypeId,
        string rulesetId,
        string rigId,
        string factionId,
        CreatureAuthoringEncounterRole encounterRole,
        double threatRating,
        string visualKitId,
        IReadOnlyList<string>? combatTags,
        CreatureSpeciesProfile species,
        CreatureAppearanceProfile appearance,
        CreatureAnimationGraphProfile animationGraph,
        IReadOnlyDictionary<string, string>? overrides)
    {
        ArgumentNullException.ThrowIfNull(previewTransform);
        ArgumentNullException.ThrowIfNull(species);
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(animationGraph);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        AssetId = CreaturePresentationProfile.NormalizeIdentifier(assetId);
        PreviewTransform = previewTransform;
        ArchetypeId = CreaturePresentationProfile.NormalizeIdentifier(archetypeId);
        RulesetId = CreaturePresentationProfile.NormalizeIdentifier(rulesetId);
        RigId = CreaturePresentationProfile.NormalizeIdentifier(rigId);
        FactionId = CreaturePresentationProfile.NormalizeIdentifier(factionId);
        EncounterRole = encounterRole;
        ThreatRating = threatRating;
        VisualKitId = CreaturePresentationProfile.NormalizeIdentifier(visualKitId);
        CombatTags = (combatTags ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Species = species.Normalize();
        Appearance = appearance.Normalize();
        AnimationGraph = animationGraph.Normalize();
        Overrides = (overrides ?? new Dictionary<string, string>()).OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => CreaturePresentationProfile.NormalizeIdentifier(item.Key),
                item => item.Value?.Trim() ?? string.Empty, StringComparer.Ordinal);
    }

    public static CreatureAuthoringCompositionProfile CreateDefault(Guid id, string provenance) => new(
        id,
        CurrentSchemaVersion,
        provenance,
        "asset.creature.default",
        TransformValue.Identity,
        "archetype.guardian",
        "ruleset.core",
        "rig.humanoid",
        "faction.neutral",
        CreatureAuthoringEncounterRole.Defender,
        3,
        "visual.guardian",
        ["melee", "guardian"],
        new("species.human", [], [new("human.core", ["humanoid"])], ["standard"], "rig.humanoid"),
        new("visual.guardian", "mesh.guardian", "rig.humanoid", new Dictionary<string, double>(),
            new Dictionary<string, string>(), ["material.guardian"], ["palette.ember"], [], "preset.guardian"),
        new(Guid.NewGuid(), CreatureAnimationGraphProfile.CurrentSchemaVersion, provenance,
            [
                new(Guid.NewGuid(), "idle", "locomotion", "clip.guardian.idle"),
                new(Guid.NewGuid(), "walk", "locomotion", "clip.guardian.walk"),
                new(Guid.NewGuid(), "guard", "combat", "clip.guardian.guard"),
                new(Guid.NewGuid(), "salute", "emote", "clip.guardian.salute"),
                new(Guid.NewGuid(), "breathing", "additive", "clip.guardian.breathing"),
            ],
            [new(Guid.NewGuid(), "idle", "walk", "movement.requested")],
            "retarget.humanoid"),
        new Dictionary<string, string>());
}
