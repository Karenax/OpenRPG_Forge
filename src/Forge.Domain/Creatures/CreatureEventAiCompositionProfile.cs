using Forge.Domain.Lifecycle;
using Forge.Domain.Spatial;

namespace Forge.Domain.Creatures;

public enum CreatureEventAiControllerKind
{
    StateMachine,
    BehaviorTree,
    Utility,
    Script,
}

public enum CreatureEventAiBlackboardValueKind
{
    Boolean,
    Integer,
    Number,
    Text,
    Entity,
    Vector3,
}

public enum CreatureEventAiRecoveryStrategy
{
    Wait,
    Repath,
    Avoid,
    Teleport,
    Abort,
}

public sealed record CreaturePerceptionEventProfile(
    Guid Id,
    EventBinding Detected,
    EventBinding Lost)
{
    internal CreaturePerceptionEventProfile Normalize() => this with
    {
        Detected = CreatureEventAiCompositionProfile.NormalizeBinding(Detected),
        Lost = CreatureEventAiCompositionProfile.NormalizeBinding(Lost),
    };
}

public sealed record CreaturePhysicalAttackEventProfile(
    Guid Id,
    EventBinding Declared,
    EventBinding Resolved)
{
    internal CreaturePhysicalAttackEventProfile Normalize() => this with
    {
        Declared = CreatureEventAiCompositionProfile.NormalizeBinding(Declared),
        Resolved = CreatureEventAiCompositionProfile.NormalizeBinding(Resolved),
    };
}

public sealed record CreatureAbilityEventProfile(
    Guid Id,
    EventBinding Targeted,
    EventBinding Impacted)
{
    internal CreatureAbilityEventProfile Normalize() => this with
    {
        Targeted = CreatureEventAiCompositionProfile.NormalizeBinding(Targeted),
        Impacted = CreatureEventAiCompositionProfile.NormalizeBinding(Impacted),
    };
}

public sealed record CreatureBlockedEventProfile(
    Guid Id,
    EventBinding Blocked,
    IReadOnlyList<CreatureEventAiRecoveryStrategy> RecoveryOrder)
{
    internal CreatureBlockedEventProfile Normalize() => this with
    {
        Blocked = CreatureEventAiCompositionProfile.NormalizeBinding(Blocked),
        RecoveryOrder = (RecoveryOrder ?? []).Distinct().ToArray(),
    };
}

public sealed record CreatureEventAiBlackboardEntry(
    Guid Id,
    string Key,
    CreatureEventAiBlackboardValueKind Kind,
    string Value,
    bool Persistent)
{
    internal CreatureEventAiBlackboardEntry Normalize() => this with
    {
        Key = CreaturePresentationProfile.NormalizeIdentifier(Key),
        Value = Value?.Trim() ?? string.Empty,
    };
}

public sealed record CreatureEventAiControllerProfile(
    Guid Id,
    CreatureEventAiControllerKind Kind,
    string DefinitionId,
    IReadOnlyList<CreatureEventAiBlackboardEntry> Blackboard,
    IReadOnlyList<string> AllowedActions)
{
    internal CreatureEventAiControllerProfile Normalize() => this with
    {
        DefinitionId = CreaturePresentationProfile.NormalizeIdentifier(DefinitionId),
        Blackboard = (Blackboard ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.Key, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray(),
        AllowedActions = (AllowedActions ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
    };
}

public sealed record CreatureEventAiCompositionProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public string AssetId { get; }
    public TransformValue PreviewTransform { get; }
    public CreaturePerceptionEventProfile Perception { get; }
    public CreaturePhysicalAttackEventProfile PhysicalAttack { get; }
    public CreatureAbilityEventProfile Ability { get; }
    public CreatureBlockedEventProfile Blocked { get; }
    public CreatureEventAiControllerProfile Controller { get; }
    public IReadOnlyDictionary<string, string> Overrides { get; }

    public CreatureEventAiCompositionProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        string assetId,
        TransformValue previewTransform,
        CreaturePerceptionEventProfile perception,
        CreaturePhysicalAttackEventProfile physicalAttack,
        CreatureAbilityEventProfile ability,
        CreatureBlockedEventProfile blocked,
        CreatureEventAiControllerProfile controller,
        IReadOnlyDictionary<string, string>? overrides)
    {
        ArgumentNullException.ThrowIfNull(previewTransform);
        ArgumentNullException.ThrowIfNull(perception);
        ArgumentNullException.ThrowIfNull(physicalAttack);
        ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(blocked);
        ArgumentNullException.ThrowIfNull(controller);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        AssetId = CreaturePresentationProfile.NormalizeIdentifier(assetId);
        PreviewTransform = previewTransform;
        Perception = perception.Normalize();
        PhysicalAttack = physicalAttack.Normalize();
        Ability = ability.Normalize();
        Blocked = blocked.Normalize();
        Controller = controller.Normalize();
        Overrides = (overrides ?? new Dictionary<string, string>()).OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => CreaturePresentationProfile.NormalizeIdentifier(item.Key),
                item => item.Value?.Trim() ?? string.Empty, StringComparer.Ordinal);
    }

    internal static EventBinding NormalizeBinding(EventBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return binding with
        {
            Provenance = binding.Provenance?.Trim() ?? string.Empty,
            ForgeScriptApiVersion = binding.ForgeScriptApiVersion?.Trim() ?? string.Empty,
            EventName = binding.EventName?.Trim().ToLowerInvariant() ?? string.Empty,
            Handler = binding.Handler?.Trim() ?? string.Empty,
        };
    }

    public static CreatureEventAiCompositionProfile CreateDefault(Guid id, string provenance) => new(
        id, CurrentSchemaVersion, provenance, "asset.creature-event-ai.default", TransformValue.Identity,
        new(Guid.NewGuid(),
            NewBinding("creature.stimulus_detected", "OnPerceptionDetected", provenance),
            NewBinding("creature.stimulus_lost", "OnPerceptionLost", provenance)),
        new(Guid.NewGuid(),
            NewBinding("creature.attack_declared", "BeforePhysicalAttacked", provenance),
            NewBinding("creature.attack_resolved", "OnPhysicalAttacked", provenance)),
        new(Guid.NewGuid(),
            NewBinding("creature.ability_targeted", "BeforeAbilityTargeted", provenance),
            NewBinding("creature.ability_impacted", "OnSpellCastAt", provenance)),
        new(Guid.NewGuid(), NewBinding("creature.movement_blocked", "OnBlocked", provenance),
            [CreatureEventAiRecoveryStrategy.Repath, CreatureEventAiRecoveryStrategy.Avoid,
                CreatureEventAiRecoveryStrategy.Teleport, CreatureEventAiRecoveryStrategy.Abort]),
        new(Guid.NewGuid(), CreatureEventAiControllerKind.StateMachine, "ai.default",
            [new(Guid.NewGuid(), "state", CreatureEventAiBlackboardValueKind.Text, "idle", true),
             new(Guid.NewGuid(), "target", CreatureEventAiBlackboardValueKind.Entity, "entity.none", false)],
            ["idle", "patrol"]), new Dictionary<string, string>());

    private static EventBinding NewBinding(string eventName, string handler, string provenance) =>
        new(Guid.NewGuid(), 1, provenance, "1.0", eventName, handler, EventAuthorityScope.Server);
}

public sealed record CreatureEventAiDecision(
    string Action,
    double Score,
    IReadOnlyDictionary<string, CreatureEventAiBlackboardEntry> Blackboard);
