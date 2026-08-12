using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Doors;

public sealed record PortalUseRequested(Guid EventId, int SchemaVersion, string Provenance, Guid DoorId, string ActorId, PortalDestinationKind DestinationKind, Guid DestinationId, string Cause, IReadOnlySet<string> Conditions, bool Vetoed, string? VetoReason);
public sealed record PortalUseCompleted(Guid EventId, int SchemaVersion, string Provenance, Guid RequestEventId, Guid DoorId, string ActorId, Guid DestinationId, string SpawnAnchorId, string Result);
public sealed record DoorStateEvent(Guid EventId, int SchemaVersion, string Provenance, string Name, Guid DoorId, string ActorId, string PreviousState, string CurrentState, string Trigger, DateTimeOffset OccurredAt);
public sealed record DoorLockEvent(Guid EventId, int SchemaVersion, string Provenance, string Name, Guid DoorId, string ActorId, string Method, string? Credential, LockState PreviousState, LockState CurrentState, DateTimeOffset OccurredAt);

public static class DoorEventService
{
    public static (PortalUseRequested Request, PortalUseCompleted? Completed) RequestPortalUse(DoorDefinition door, string actorId, string cause, IReadOnlySet<string> conditions, string provenance, Func<PortalUseRequested, string?>? veto = null)
    {
        var portal = door.Portal ?? throw new InvalidOperationException("RPG_DOOR_PORTAL_MISSING");
        if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(cause)) throw new InvalidOperationException("RPG_DOOR_PORTAL_REQUEST_INVALID");
        var requested = new PortalUseRequested(Guid.NewGuid(), 1, provenance, door.Id, actorId, portal.DestinationKind, portal.DestinationId, cause, conditions, false, null);
        var reason = veto?.Invoke(requested);
        if (!string.IsNullOrWhiteSpace(reason)) return (requested with { Vetoed = true, VetoReason = reason }, null);
        return (requested, new(Guid.NewGuid(), 1, provenance, requested.EventId, door.Id, actorId, portal.DestinationId, portal.SpawnAnchorId, "accepted"));
    }

    public static DoorStateEvent CompleteStateTransition(Guid doorId, string actorId, string previousState, string currentState, string trigger, string provenance, DateTimeOffset at)
    {
        var name = currentState switch { "open" => "DoorOpened", "closed" => "DoorClosed", _ => throw new InvalidOperationException("RPG_DOOR_TERMINAL_STATE_REQUIRED") };
        if (previousState == currentState) throw new InvalidOperationException("RPG_DOOR_STATE_UNCHANGED");
        return new(Guid.NewGuid(), 1, provenance, name, doorId, actorId, previousState, currentState, trigger, at);
    }

    public static DoorLockEvent CompleteLockTransition(Guid doorId, string actorId, string method, string? credential, LockState previousState, LockState currentState, string provenance, DateTimeOffset at)
    {
        var name = currentState switch { LockState.Locked => "DoorLocked", LockState.Unlocked => "DoorUnlocked", _ => throw new InvalidOperationException("RPG_DOOR_LOCK_TERMINAL_STATE_REQUIRED") };
        if (doorId == Guid.Empty || string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(method) ||
            string.IsNullOrWhiteSpace(provenance) || previousState == currentState)
            throw new InvalidOperationException("RPG_DOOR_LOCK_EVENT_INVALID");
        return new(Guid.NewGuid(), 1, provenance, name, doorId, actorId, method, credential, previousState, currentState, at);
    }
}
