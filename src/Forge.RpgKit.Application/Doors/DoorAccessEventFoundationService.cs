using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Doors;
using Forge.RpgKit.Domain.Placeables;
using RpgVector3 = Forge.RpgKit.Domain.Placeables.Vector3Value;

namespace Forge.RpgKit.Application.Doors;

public static class DoorAccessEventFoundationService
{
    public static DoorAccessEventResolution Resolve(DoorWorldProfile profile) =>
        DoorAccessEventProfileValidator.Resolve(profile);

    public static DoorLockEventResolution ResolveLockEvents(DoorWorldProfile profile) =>
        DoorAccessEventProfileValidator.ResolveLockEvents(profile);

    public static LockComponent CreateLock(DoorWorldProfile profile)
    {
        var policy = Required(profile).Lock;
        return new(Map(policy.State), policy.CredentialIds.ToHashSet(StringComparer.Ordinal), policy.SkillId,
            policy.Difficulty, policy.CodeHash, policy.Hackable, policy.Relockable, policy.CanJam,
            policy.Permissions.ToHashSet(StringComparer.Ordinal));
    }

    public static DoorUnlockResult Unlock(DoorWorldProfile profile, LockAttempt attempt,
        IReadOnlySet<string> credentials) =>
        DoorRuntimeService.Unlock(CreateLock(profile), attempt, credentials, Required(profile).Lock.ConsumeCredential);

    public static LockResult Relock(DoorWorldProfile profile, bool jam, string actorId) =>
        PlaceableSecurityService.Relock(CreateLock(profile) with { State = LockState.Unlocked }, jam, actorId);

    public static DoorTrapBinding CreateTrap(DoorWorldProfile profile)
    {
        var policy = Required(profile).Trap;
        var trap = new TrapComponent(policy.Id, policy.TriggerId, policy.PayloadId, policy.DetectionDifficulty,
            policy.DisarmDifficulty, TimeSpan.FromSeconds(policy.ResetDelaySeconds), policy.OwnerId,
            policy.VisualEffectId, policy.FriendlyPolicy, policy.Armed, policy.OneShot);
        return new(trap, policy.Sides.ToHashSet(StringComparer.Ordinal), policy.Events.ToHashSet(StringComparer.Ordinal));
    }

    public static TrapResult TriggerTrap(DoorWorldProfile profile, string side, string eventName,
        string actorFaction) =>
        DoorRuntimeService.TriggerTrap(CreateTrap(profile), side, eventName, actorFaction);

    public static DoorOpenRejected Reject(DoorWorldProfile profile, string actorId, string reasonCode)
    {
        var access = Required(profile);
        var policy = access.Rejection;
        return DoorRuntimeService.Reject(profile.Id, actorId, reasonCode,
            policy.Texts.Select(item => new LocalizedDoorFailure(item.Locale, item.Text)).ToArray(),
            policy.SoundAssetId, policy.AnimationId, policy.ScriptId, access.Provenance);
    }

    public static (PortalUseRequested Request, PortalUseCompleted? Completed) RequestPortalUse(
        DoorWorldProfile profile,
        string actorId,
        string cause,
        IReadOnlySet<string> conditions,
        Func<PortalUseRequested, string?>? veto = null)
    {
        var access = Required(profile);
        return DoorEventService.RequestPortalUse(ToDefinition(profile), actorId, cause, conditions,
            access.Provenance, veto);
    }

    public static DoorStateEvent CompleteStateTransition(DoorWorldProfile profile, string actorId,
        string previousState, string currentState, string trigger, DateTimeOffset occurredAt) =>
        DoorEventService.CompleteStateTransition(profile.Id, actorId, previousState, currentState, trigger,
            Required(profile).Provenance, occurredAt);

    public static DoorLockEvent CompleteLockTransition(DoorWorldProfile profile, string actorId, string method,
        string? credential, DoorAccessLockState previousState, DoorAccessLockState currentState,
        DateTimeOffset occurredAt)
    {
        _ = RequiredLockEvents(profile);
        return DoorEventService.CompleteLockTransition(profile.Id, actorId, method, credential,
            Map(previousState), Map(currentState), Required(profile).Provenance, occurredAt);
    }

    private static DoorAccessEventProfile Required(DoorWorldProfile profile)
    {
        var resolution = Resolve(profile);
        if (!resolution.Available || resolution.Profile is null)
            throw new InvalidOperationException(resolution.Diagnostics.FirstOrDefault()?.Code ??
                                                "WORLD_DOOR_ACCESS_EVENTS_MISSING");
        return resolution.Profile;
    }

    private static DoorAccessEventProfile.DoorLockEventPolicy RequiredLockEvents(DoorWorldProfile profile)
    {
        var resolution = ResolveLockEvents(profile);
        if (!resolution.Available || resolution.Profile is null)
            throw new InvalidOperationException(resolution.Diagnostics.FirstOrDefault()?.Code ??
                                                "WORLD_DOOR_LOCK_EVENTS_MISSING");
        return resolution.Profile;
    }

    private static DoorDefinition ToDefinition(DoorWorldProfile profile)
    {
        var access = Required(profile);
        var foundation = profile.Foundation ?? throw new InvalidOperationException("WORLD_DOOR_FOUNDATION_MISSING");
        var transform = access.Transform;
        var adapter = foundation.Adapter is null
            ? null
            : new DoorKitAdapter(foundation.Adapter.KitId, foundation.Adapter.SocketKind,
                foundation.Adapter.MaterialBindings,
                new(foundation.Adapter.WidthOffset, foundation.Adapter.HeightOffset, foundation.Adapter.DepthOffset));
        var states = foundation.States.Select(item => new DoorStateDefinition(item.Id, (DoorState)item.Kind,
            item.AnimationId, item.CollisionEnabled, item.InteractionEnabled)).ToArray();
        var transitions = foundation.Transitions.Select(item => new DoorTransition(item.FromStateId, item.ToStateId,
            item.Trigger, TimeSpan.FromSeconds(item.DurationSeconds), item.Condition ?? string.Empty)).ToArray();
        var interaction = foundation.Interaction;
        var portal = foundation.Portal is null
            ? null
            : new PortalComponent((PortalDestinationKind)foundation.Portal.DestinationKind,
                foundation.Portal.DestinationId, foundation.Portal.SpawnAnchorId,
                new(0, foundation.Portal.ExitYawDegrees, 0), foundation.Portal.Preload,
                foundation.Portal.Permissions.ToHashSet(StringComparer.Ordinal));
        return new(profile.Id, profile.SchemaVersion, profile.Provenance, access.AssetId, access.AssetId,
            (DoorPlacementMode)foundation.Placement, foundation.ArchitectureSocketId, adapter,
            new(new(transform.Position.X, transform.Position.Y, transform.Position.Z), new RpgVector3(0, 0, 0),
                new(transform.Scale.X, transform.Scale.Y, transform.Scale.Z), new RpgVector3(0, 0, 0), 0, null,
                false, null),
            new(foundation.InitialStateId, states, transitions),
            new(interaction.Range, interaction.AllowedSide,
                new(interaction.ClearanceWidth, interaction.ClearanceHeight, interaction.ClearanceDepth),
                interaction.Authority, interaction.QueueCapacity, interaction.FailureFeedbackKey), portal);
    }

    private static LockState Map(DoorAccessLockState value) => value switch
    {
        DoorAccessLockState.Unlocked => LockState.Unlocked,
        DoorAccessLockState.Locked => LockState.Locked,
        DoorAccessLockState.Jammed => LockState.Jammed,
        DoorAccessLockState.Broken => LockState.Broken,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
