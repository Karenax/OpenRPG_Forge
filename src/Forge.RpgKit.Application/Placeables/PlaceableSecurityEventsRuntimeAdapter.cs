using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Items;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Application.Placeables;

public sealed record PlaceableSecurityEventsHandle(Guid PlaceableId, Guid ProfileId);

public sealed record PlaceableSecurityEventsResolution(
    bool Available,
    bool OptionalFallback,
    PlaceableSecurityEventsHandle? Handle,
    string? DiagnosticCode);

public sealed record PlaceableSecurityEventsRuntime(
    TrapComponent Trap,
    LootSpawner LootSpawner,
    LootTable LootTable,
    PlaceableContainerStateEventsProfile ContainerEvents,
    PlaceableLockStateEventsProfile LockEvents,
    PlaceableContentsChangedEventProfile ContentsChanged);

public sealed record PlaceableBoundEvent<T>(string ForgeScriptApiVersion, string Binding, T Payload);

public sealed record PlaceableLockEventDispatch(
    PlaceableBoundEvent<LockStateEvent> Changing,
    PlaceableBoundEvent<LockStateEvent>? Changed,
    LockComponent Lock);

public sealed class PlaceableSecurityEventsRuntimeAdapter
{
    public PlaceableSecurityEventsResolution Resolve(PlaceableWorldProfile placeable)
    {
        if (placeable.SecurityEvents is null)
        {
            return placeable.SecurityEventsRequiredAtRuntime
                ? new(false, false, null, "WORLD_PLACEABLE_SECURITY_EVENTS_REQUIRED_MISSING")
                : new(false, true, null, null);
        }

        return new(true, false, new(placeable.Id, placeable.SecurityEvents.Id), null);
    }

    public PlaceableSecurityEventsRuntime Build(
        PlaceableSecurityEventsHandle handle,
        PlaceableWorldProfile placeable)
    {
        var profile = Require(handle, placeable);
        var trap = profile.Trap;
        var loot = profile.Loot;
        return new(
            new TrapComponent(trap.Id, trap.TriggerId, trap.PayloadId, trap.DetectionDifficulty,
                trap.DisarmDifficulty, TimeSpan.FromSeconds(trap.ResetDelaySeconds), trap.OwnerId,
                trap.VisualEffectId, trap.FriendlyPolicy, trap.Armed, trap.OneShot),
            new LootSpawner(loot.LootTableId, MapTiming(loot.Timing), loot.Seed,
                loot.PersistGeneratedState,
                loot.SavedResults.Select(item => new LootResult(item.ItemDefinitionId, item.Quantity,
                    item.Quality, item.Group)).ToArray(), loot.Generation),
            new LootTable(loot.LootTable.Id, loot.LootTable.SchemaVersion, loot.LootTable.Provenance,
                loot.LootTable.Entries.Select(item => new LootEntry(item.ItemDefinitionId, item.Group,
                    item.Weight, item.MinimumQuantity, item.MaximumQuantity, item.MinimumQuality,
                    item.Condition, item.Unique)).ToArray()),
            profile.ContainerEvents,
            profile.LockEvents,
            profile.ContentsChanged);
    }

    public TrapResult Detect(PlaceableSecurityEventsHandle handle, PlaceableWorldProfile placeable,
        double perception, string actorFaction)
        => PlaceableSecurityService.Detect(Build(handle, placeable).Trap, perception, actorFaction);

    public TrapResult Disarm(PlaceableSecurityEventsHandle handle, PlaceableWorldProfile placeable,
        double skill)
        => PlaceableSecurityService.Disarm(Build(handle, placeable).Trap, skill);

    public TrapResult Trigger(PlaceableSecurityEventsHandle handle, PlaceableWorldProfile placeable,
        string actorFaction)
        => PlaceableSecurityService.Trigger(Build(handle, placeable).Trap, actorFaction);

    public TrapComponent Reset(PlaceableSecurityEventsHandle handle, PlaceableWorldProfile placeable,
        TrapComponent trap)
    {
        Require(handle, placeable);
        return PlaceableSecurityService.Reset(trap);
    }

    public LootSpawnResult Spawn(PlaceableSecurityEventsHandle handle, PlaceableWorldProfile placeable,
        LootSpawnTiming timing, IReadOnlySet<string> conditions, LootSpawner? current = null)
    {
        var runtime = Build(handle, placeable);
        return PlaceableSecurityService.Spawn(current ?? runtime.LootSpawner, runtime.LootTable, timing,
            conditions);
    }

    public PlaceableBoundEvent<ContainerStateEvent> ContainerStateChanged(
        PlaceableSecurityEventsHandle handle,
        PlaceableWorldProfile placeable,
        bool opened,
        string actorId,
        string result,
        string previousState,
        string authority,
        DateTimeOffset occurredAt)
    {
        var runtime = Build(handle, placeable);
        var policy = runtime.ContainerEvents;
        var payload = PlaceableSecurityService.ContainerEvent(opened, placeable.Id, actorId, result,
            previousState, authority, placeable.SecurityEvents!.Provenance, occurredAt);
        return new(policy.ForgeScriptApiVersion, opened ? policy.OpenedBinding : policy.ClosedBinding, payload);
    }

    public PlaceableLockEventDispatch ChangeLockState(
        PlaceableSecurityEventsHandle handle,
        PlaceableWorldProfile placeable,
        LockState requested,
        string method,
        string? credential,
        string actorId,
        Func<LockStateEvent, string?>? veto = null)
    {
        var runtime = Build(handle, placeable);
        var operations = placeable.Operations ??
            throw new InvalidOperationException("WORLD_PLACEABLE_OPERATIONS_REQUIRED_MISSING");
        var operationRuntime = new PlaceableOperationsRuntimeAdapter().Build(
            new(placeable.Id, operations.Id), placeable);
        var currentLock = operationRuntime.Lock ??
            throw new InvalidOperationException("WORLD_PLACEABLE_LOCK_MISSING");
        var result = PlaceableEventService.ChangeLockState(currentLock, placeable.Id, requested, method,
            credential, actorId, placeable.SecurityEvents!.Provenance,
            runtime.LockEvents.ControlledVeto ? veto : null);
        return new(
            new(runtime.LockEvents.ForgeScriptApiVersion, runtime.LockEvents.ChangingBinding, result.Changing),
            result.Changed is null ? null : new(runtime.LockEvents.ForgeScriptApiVersion,
                runtime.LockEvents.ChangedBinding, result.Changed),
            result.Lock);
    }

    public PlaceableBoundEvent<ContainerContentsChanged> ContentsChanged(
        PlaceableSecurityEventsHandle handle,
        PlaceableWorldProfile placeable,
        long previousRevision,
        IReadOnlyList<ContainerContentDelta> delta,
        string cause,
        string actorId)
    {
        var runtime = Build(handle, placeable);
        var payload = PlaceableEventService.ContentsChanged(placeable.Id, previousRevision, delta, cause,
            actorId, placeable.SecurityEvents!.Provenance);
        return new(runtime.ContentsChanged.ForgeScriptApiVersion, runtime.ContentsChanged.EventBinding,
            payload);
    }

    private static PlaceableSecurityEventsProfile Require(
        PlaceableSecurityEventsHandle handle,
        PlaceableWorldProfile placeable)
    {
        if (handle.PlaceableId != placeable.Id || placeable.SecurityEvents is null ||
            handle.ProfileId != placeable.SecurityEvents.Id)
            throw new InvalidOperationException("WORLD_PLACEABLE_SECURITY_EVENTS_HANDLE_STALE");
        return placeable.SecurityEvents;
    }

    private static LootSpawnTiming MapTiming(PlaceableAuthoredLootSpawnTiming timing) => timing switch
    {
        PlaceableAuthoredLootSpawnTiming.Initialize => LootSpawnTiming.Initialize,
        PlaceableAuthoredLootSpawnTiming.Open => LootSpawnTiming.Open,
        PlaceableAuthoredLootSpawnTiming.Reset => LootSpawnTiming.Reset,
        _ => throw new ArgumentOutOfRangeException(nameof(timing)),
    };
}
