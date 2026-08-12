using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Domain.Creatures;

public enum CreatureRulesInitialLifeState
{
    Alive,
    Unconscious,
    Dead,
    Sleeping,
    Seated,
    Frozen,
}

public enum CreatureRulesAttributeFormulaOperation
{
    Sum,
    Product,
    Ratio,
}

public sealed record CreatureRulesInitialState(
    Guid Id,
    CreatureRulesInitialLifeState LifeState,
    string? AnimationLoopAssetId,
    string? PoseId,
    bool Invisible)
{
    internal CreatureRulesInitialState Normalize() => this with
    {
        AnimationLoopAssetId = NormalizeOptional(AnimationLoopAssetId),
        PoseId = NormalizeOptional(PoseId),
    };

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : CreaturePresentationProfile.NormalizeIdentifier(value);
}

public sealed record CreatureRulesAttributeModifier(
    Guid Id,
    string AttributeId,
    string SourceId,
    double Value)
{
    internal CreatureRulesAttributeModifier Normalize() => this with
    {
        AttributeId = CreaturePresentationProfile.NormalizeIdentifier(AttributeId),
        SourceId = CreaturePresentationProfile.NormalizeIdentifier(SourceId),
    };
}

public sealed record CreatureRulesDerivedAttribute(
    Guid Id,
    string AttributeId,
    CreatureRulesAttributeFormulaOperation Operation,
    IReadOnlyList<string> Dependencies,
    double Constant)
{
    internal CreatureRulesDerivedAttribute Normalize() => this with
    {
        AttributeId = CreaturePresentationProfile.NormalizeIdentifier(AttributeId),
        Dependencies = (Dependencies ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
            .Distinct(StringComparer.Ordinal).ToArray(),
    };
}

public sealed record CreatureRulesAttributeSet(
    Guid Id,
    string RulesetId,
    IReadOnlyDictionary<string, double> BaseValues,
    IReadOnlyList<CreatureRulesAttributeModifier> Modifiers,
    IReadOnlyList<CreatureRulesDerivedAttribute> Derived)
{
    internal CreatureRulesAttributeSet Normalize() => this with
    {
        RulesetId = CreaturePresentationProfile.NormalizeIdentifier(RulesetId),
        BaseValues = (BaseValues ?? new Dictionary<string, double>()).OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => CreaturePresentationProfile.NormalizeIdentifier(item.Key), item => item.Value,
                StringComparer.Ordinal),
        Modifiers = (Modifiers ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.AttributeId, StringComparer.Ordinal).ThenBy(item => item.SourceId, StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray(),
        Derived = (Derived ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.AttributeId, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray(),
    };
}

public sealed record CreatureRulesDefenseProfile(
    Guid Id,
    double Avoidance,
    double Armor,
    IReadOnlyDictionary<string, double> Resistances,
    IReadOnlyDictionary<string, double> Saves,
    IReadOnlyList<string> Immunities,
    double FlatMitigation)
{
    internal CreatureRulesDefenseProfile Normalize() => this with
    {
        Resistances = NormalizeMap(Resistances),
        Saves = NormalizeMap(Saves),
        Immunities = (Immunities ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
    };

    private static IReadOnlyDictionary<string, double> NormalizeMap(IReadOnlyDictionary<string, double>? values) =>
        (values ?? new Dictionary<string, double>()).OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => CreaturePresentationProfile.NormalizeIdentifier(item.Key), item => item.Value,
                StringComparer.Ordinal);
}

public sealed record CreatureRulesAbilityInstance(
    Guid Id,
    string AbilityId,
    int Level,
    int Charges,
    double CooldownSeconds,
    IReadOnlyList<string> Conditions,
    IReadOnlyList<string> AiTags,
    string ScriptId)
{
    internal CreatureRulesAbilityInstance Normalize() => this with
    {
        AbilityId = CreaturePresentationProfile.NormalizeIdentifier(AbilityId),
        Conditions = NormalizeIds(Conditions),
        AiTags = NormalizeIds(AiTags),
        ScriptId = CreaturePresentationProfile.NormalizeIdentifier(ScriptId),
    };

    private static IReadOnlyList<string> NormalizeIds(IReadOnlyList<string>? values) =>
        (values ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}

public sealed record CreatureRulesInventoryEntry(
    Guid Id,
    BlueprintId ItemBlueprintId,
    int Quantity,
    double UnitWeight,
    double UnitVolume);

public sealed record CreatureRulesInventoryContainer(
    Guid Id,
    string ContainerId,
    int SlotCount,
    double MaximumWeight,
    double MaximumVolume,
    IReadOnlyList<CreatureRulesInventoryEntry> Items)
{
    internal CreatureRulesInventoryContainer Normalize() => this with
    {
        ContainerId = CreaturePresentationProfile.NormalizeIdentifier(ContainerId),
        Items = (Items ?? []).OrderBy(item => item.ItemBlueprintId.ToString(), StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray(),
    };
}

public sealed record CreatureRulesInventoryProfile(
    Guid Id,
    IReadOnlyList<CreatureRulesInventoryContainer> Containers,
    IReadOnlyDictionary<string, Guid> EquipmentSlots,
    string OwnerId,
    bool Persistent)
{
    internal CreatureRulesInventoryProfile Normalize() => this with
    {
        Containers = (Containers ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.ContainerId, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray(),
        EquipmentSlots = (EquipmentSlots ?? new Dictionary<string, Guid>())
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => CreaturePresentationProfile.NormalizeIdentifier(item.Key), item => item.Value,
                StringComparer.Ordinal),
        OwnerId = CreaturePresentationProfile.NormalizeIdentifier(OwnerId),
    };
}

public sealed record CreatureRulesCompositionProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public string AssetId { get; }
    public TransformValue PreviewTransform { get; }
    public CreatureRulesInitialState InitialState { get; }
    public CreatureRulesAttributeSet Attributes { get; }
    public CreatureRulesDefenseProfile Defenses { get; }
    public IReadOnlyList<CreatureRulesAbilityInstance> Abilities { get; }
    public CreatureRulesInventoryProfile Inventory { get; }
    public IReadOnlyDictionary<string, string> Overrides { get; }

    public CreatureRulesCompositionProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        string assetId,
        TransformValue previewTransform,
        CreatureRulesInitialState initialState,
        CreatureRulesAttributeSet attributes,
        CreatureRulesDefenseProfile defenses,
        IReadOnlyList<CreatureRulesAbilityInstance>? abilities,
        CreatureRulesInventoryProfile inventory,
        IReadOnlyDictionary<string, string>? overrides)
    {
        ArgumentNullException.ThrowIfNull(previewTransform);
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(defenses);
        ArgumentNullException.ThrowIfNull(inventory);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        AssetId = CreaturePresentationProfile.NormalizeIdentifier(assetId);
        PreviewTransform = previewTransform;
        InitialState = initialState.Normalize();
        Attributes = attributes.Normalize();
        Defenses = defenses.Normalize();
        Abilities = (abilities ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.AbilityId, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray();
        Inventory = inventory.Normalize();
        Overrides = (overrides ?? new Dictionary<string, string>()).OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => CreaturePresentationProfile.NormalizeIdentifier(item.Key),
                item => item.Value?.Trim() ?? string.Empty, StringComparer.Ordinal);
    }

    public static CreatureRulesCompositionProfile CreateDefault(Guid id, string provenance) => new(
        id, CurrentSchemaVersion, provenance, "asset.creature-rules.default", TransformValue.Identity,
        new(Guid.NewGuid(), CreatureRulesInitialLifeState.Alive, "clip.default.idle", "pose.default.idle", false),
        new(Guid.NewGuid(), "ruleset.core", new Dictionary<string, double>
        {
            ["agility"] = 5,
            ["vitality"] = 5,
        }, [], []),
        new(Guid.NewGuid(), 0, 0, new Dictionary<string, double>(), new Dictionary<string, double>(), [], 0),
        [], new(Guid.NewGuid(), [new(Guid.NewGuid(), "container.backpack", 16, 100, 100, [])],
            new Dictionary<string, Guid>(), "creature.default", true),
        new Dictionary<string, string>());
}
