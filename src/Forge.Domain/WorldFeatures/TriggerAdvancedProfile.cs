namespace Forge.Domain.WorldFeatures;

public sealed record TriggerAdvancedProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ForgeScriptApiVersion,
    TriggerTrapProfile Trap,
    TriggerEntityQueryProfile TargetFilter,
    IReadOnlyList<TriggerGameplayVolumeProfile> GameplayVolumes,
    TriggerOccupancyDebugProfile OccupancyDebug);

public sealed record TriggerTrapProfile(
    Guid Id,
    string TriggerId,
    string PayloadId,
    double DetectionDifficulty,
    double DisarmDifficulty,
    double ResetDelaySeconds,
    string OwnerId,
    string VisualEffectId,
    string FriendlyPolicy,
    bool Armed,
    bool OneShot,
    bool GameMasterVisualization,
    string VisualizationColor,
    string DetectedBinding,
    string DisarmedBinding,
    string TriggeredBinding,
    string ResetBinding);

public sealed record TriggerEntityQueryProfile(
    Guid Id,
    bool PlayersOnly,
    IReadOnlyList<string> RequiredTags,
    IReadOnlyList<string> ExcludedTags,
    IReadOnlyList<Guid> FactionIds,
    IReadOnlyList<string> RequiredComponents,
    string? OwnerId,
    string? TeamId,
    string? SandboxPredicate);

public enum TriggerGameplayVolumeKind
{
    Damage,
    Heal,
    Gravity,
    Water,
    Climb,
    NoBuild,
    Camera,
    Audio,
    Weather,
    Checkpoint,
}

public sealed record TriggerGameplayVolumeProfile(
    Guid Id,
    TriggerGameplayVolumeKind Kind,
    IReadOnlyDictionary<string, double> NumericParameters,
    IReadOnlyDictionary<string, string> TextParameters,
    string EnterBinding,
    string ExitBinding,
    bool Enabled);

public sealed record TriggerOccupancyDebugProfile(
    Guid Id,
    bool Enabled,
    bool ShowOccupants,
    bool ShowLastFires,
    bool ShowCooldowns,
    bool ShowFilters,
    bool ShowEventTrace,
    int MaximumTraceEntries);
