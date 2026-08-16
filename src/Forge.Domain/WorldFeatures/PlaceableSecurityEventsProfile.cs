using Forge.Domain.Items;

namespace Forge.Domain.WorldFeatures;

public enum PlaceableAuthoredLootSpawnTiming
{
    Initialize,
    Open,
    Reset,
}

public sealed record PlaceableSecurityEventsProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    PlaceableTrapProfile? Trap = null,
    PlaceableLootSpawnerProfile? Loot = null,
    PlaceableContainerStateEventsProfile? ContainerEvents = null,
    PlaceableLockStateEventsProfile? LockEvents = null,
    PlaceableContentsChangedEventProfile? ContentsChanged = null);

public sealed record PlaceableTrapProfile(
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
    bool OneShot);

public sealed record PlaceableSavedLootResultProfile(
    Guid ItemDefinitionId,
    int Quantity,
    int Quality,
    string Group);

public sealed record PlaceableLootSpawnerProfile(
    Guid Id,
    Guid LootTableId,
    PlaceableAuthoredLootSpawnTiming Timing,
    int Seed,
    bool PersistGeneratedState,
    IReadOnlyList<PlaceableSavedLootResultProfile> SavedResults,
    int Generation,
    ItemLootTablePolicy LootTable);

public sealed record PlaceableContainerStateEventsProfile(
    Guid Id,
    string ForgeScriptApiVersion,
    string OpenedBinding,
    string ClosedBinding,
    bool IncludeActor,
    bool IncludeResult,
    bool IncludePreviousState,
    bool IncludeAuthority);

public sealed record PlaceableLockStateEventsProfile(
    Guid Id,
    string ForgeScriptApiVersion,
    string ChangingBinding,
    string ChangedBinding,
    bool IncludeMethod,
    bool IncludeCredential,
    bool IncludeActor,
    bool ControlledVeto);

public sealed record PlaceableContentsChangedEventProfile(
    Guid Id,
    string ForgeScriptApiVersion,
    string EventBinding,
    bool IncludeTransactionalDelta,
    bool IncludeCause,
    bool IncludeRevision,
    bool IncludeActor);
