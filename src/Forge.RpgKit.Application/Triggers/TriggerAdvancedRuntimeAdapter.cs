using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Doors;
using Forge.RpgKit.Domain.Placeables;
using Forge.RpgKit.Domain.Triggers;

namespace Forge.RpgKit.Application.Triggers;

public sealed record TriggerAdvancedHandle(Guid TriggerId, Guid ProfileId);

public sealed record TriggerAdvancedResolution(
    bool Available,
    bool OptionalFallback,
    TriggerAdvancedHandle? Handle,
    string? DiagnosticCode);

public sealed record TriggerAdvancedRuntime(
    TriggerAdvancedProfile Profile,
    TriggerVolume Trigger,
    TrapVolume Trap,
    EntityQuery TargetFilter,
    IReadOnlyList<GameplayVolumeDefinition> GameplayVolumes);

public sealed class TriggerAdvancedRuntimeAdapter
{
    public TriggerAdvancedResolution Resolve(TriggerWorldProfile trigger)
    {
        if (trigger.Advanced is null)
        {
            return trigger.AdvancedRequiredAtRuntime
                ? new(false, false, null, "WORLD_TRIGGER_ADVANCED_REQUIRED_MISSING")
                : new(false, true, null, null);
        }

        return new(true, false, new(trigger.Id, trigger.Advanced.Id), null);
    }

    public TriggerAdvancedRuntime Build(TriggerAdvancedHandle handle, TriggerWorldProfile trigger)
    {
        var profile = Require(handle, trigger);
        var foundation = trigger.Foundation ??
            throw new InvalidOperationException("WORLD_TRIGGER_FOUNDATION_REQUIRED_FOR_ADVANCED");
        var trap = profile.Trap;
        return new(profile, Trigger(foundation), new(
                new(trap.Id, trap.TriggerId, trap.PayloadId, trap.DetectionDifficulty, trap.DisarmDifficulty,
                    TimeSpan.FromSeconds(trap.ResetDelaySeconds), trap.OwnerId, trap.VisualEffectId,
                    trap.FriendlyPolicy, trap.Armed, trap.OneShot), Shape(foundation.Shape),
                trap.GameMasterVisualization, trap.VisualizationColor),
            new(profile.TargetFilter.PlayersOnly,
                profile.TargetFilter.RequiredTags.ToHashSet(StringComparer.Ordinal),
                profile.TargetFilter.ExcludedTags.ToHashSet(StringComparer.Ordinal),
                profile.TargetFilter.FactionIds.ToHashSet(),
                profile.TargetFilter.RequiredComponents.ToHashSet(StringComparer.Ordinal),
                profile.TargetFilter.OwnerId, profile.TargetFilter.TeamId,
                profile.TargetFilter.SandboxPredicate),
            profile.GameplayVolumes.Where(item => item.Enabled).Select(Volume).ToArray());
    }

    public TrapResult Detect(TriggerAdvancedHandle handle, TriggerWorldProfile trigger,
        double perception, string actorFaction)
        => TriggerAdvancedService.DetectTrap(Build(handle, trigger).Trap, perception, actorFaction);

    public TrapResult Disarm(TriggerAdvancedHandle handle, TriggerWorldProfile trigger, double skill)
        => TriggerAdvancedService.DisarmTrap(Build(handle, trigger).Trap, skill);

    public TrapVolume Reset(TriggerAdvancedHandle handle, TriggerWorldProfile trigger, TrapComponent trap)
        => TriggerAdvancedService.ResetTrap(Build(handle, trigger).Trap with { Trap = trap });

    public bool Matches(TriggerAdvancedHandle handle, TriggerWorldProfile trigger, QueryableEntity entity)
        => TriggerAdvancedService.Matches(Build(handle, trigger).TargetFilter, entity);

    public GameplayVolumeEffect Apply(TriggerAdvancedHandle handle, TriggerWorldProfile trigger,
        TriggerGameplayVolumeKind kind, string entityId)
    {
        var definition = Build(handle, trigger).GameplayVolumes.Single(item =>
            item.Kind == (GameplayVolumeKind)kind);
        return TriggerAdvancedService.Apply(definition, entityId);
    }

    public TriggerDebugOverlay Debug(TriggerAdvancedHandle handle, TriggerWorldProfile trigger,
        TriggerRuntimeState state, DateTimeOffset now, IReadOnlyList<TriggerEventTrace> trace)
    {
        var runtime = Build(handle, trigger);
        var maximum = runtime.Profile.OccupancyDebug.MaximumTraceEntries;
        return TriggerAdvancedService.Debug(runtime.Trigger, state, now, trace.TakeLast(maximum).ToArray());
    }

    private static TriggerAdvancedProfile Require(TriggerAdvancedHandle handle, TriggerWorldProfile trigger)
    {
        if (handle.TriggerId != trigger.Id || trigger.Advanced is null || handle.ProfileId != trigger.Advanced.Id)
            throw new InvalidOperationException("WORLD_TRIGGER_ADVANCED_HANDLE_STALE");
        return trigger.Advanced;
    }

    private static TriggerVolume Trigger(TriggerWorldProfile.FoundationPolicy foundation)
        => new(foundation.Id, foundation.SchemaVersion, foundation.Provenance, foundation.Slug,
            Shape(foundation.Shape), new(
                foundation.Filter.EntityKinds.ToHashSet(StringComparer.Ordinal),
                foundation.Filter.RequiredTags.ToHashSet(StringComparer.Ordinal),
                foundation.Filter.ExcludedTags.ToHashSet(StringComparer.Ordinal),
                foundation.Filter.Layers.ToHashSet(StringComparer.Ordinal)),
            foundation.Conditions.ToHashSet(StringComparer.Ordinal), TimeSpan.FromSeconds(foundation.CooldownSeconds),
            (TriggerFirePolicy)foundation.Fire,
            foundation.Actions.Select(item => new TriggerAction(item.Kind, item.TargetId, item.Parameters)).ToArray(),
            foundation.Portal is null ? null : new PortalComponent((PortalDestinationKind)foundation.Portal.Destination,
                foundation.Portal.DestinationId, foundation.Portal.SpawnAnchorId,
                Vector(foundation.Portal.ExitOrientationDegrees), foundation.Portal.Preload,
                foundation.Portal.Permissions.ToHashSet(StringComparer.Ordinal)));

    private static TriggerShape Shape(TriggerWorldProfile.ShapePolicy shape)
        => new((TriggerShapeKind)shape.Kind, shape.Vertices.Select(Vector).ToArray(),
            shape.Holes.Select(hole => (IReadOnlyList<Forge.RpgKit.Domain.Placeables.Vector3Value>)hole
                .Select(Vector).ToArray()).ToArray(), Vector(shape.Size), shape.Height, shape.SnapIncrement);

    private static Forge.RpgKit.Domain.Placeables.Vector3Value Vector(Forge.Domain.Spatial.Vector3Value value)
        => new(value.X, value.Y, value.Z);

    private static GameplayVolumeDefinition Volume(TriggerGameplayVolumeProfile profile)
        => new((GameplayVolumeKind)profile.Kind, profile.NumericParameters, profile.TextParameters,
            profile.EnterBinding, profile.ExitBinding);
}
