using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Doors;

public enum DoorPlacementMode { ArchitectureSocket, FreePlacement }
public enum DoorState { Open, Closed, Opening, Closing, Locked, Jammed, Broken, Disabled, Custom }
public enum PortalDestinationKind { Door, Trigger, Waypoint, SpawnAnchor }
public sealed record DoorKitAdapter(string KitId, string SocketKind, IReadOnlyDictionary<string, string> MaterialBindings, Vector3Value SizeOffset);
public sealed record DoorStateDefinition(string Id, DoorState Kind, string AnimationId, bool CollisionEnabled, bool InteractionEnabled);
public sealed record DoorTransition(string FromStateId, string ToStateId, string Trigger, TimeSpan Duration, string Condition);
public sealed record DoorStateMachine(string InitialStateId, IReadOnlyList<DoorStateDefinition> States, IReadOnlyList<DoorTransition> Transitions);
public sealed record DoorInteractionPolicy(double Range, string AllowedSide, Vector3Value Clearance, string Authority, int QueueCapacity, string FailureFeedback);
public sealed record PortalComponent(PortalDestinationKind DestinationKind, Guid DestinationId, string SpawnAnchorId, Vector3Value ExitOrientationDegrees, bool Preload, IReadOnlySet<string> Permissions);
public sealed record DoorDefinition(Guid Id, int SchemaVersion, string Provenance, string Slug, string DisplayName, DoorPlacementMode PlacementMode, string? ArchitectureSocketId, DoorKitAdapter? KitAdapter, PlaceableTransform Transform, DoorStateMachine StateMachine, DoorInteractionPolicy Interaction, PortalComponent? Portal);
public sealed record DoorDiagnostic(string Code, string Path, string Message);
public sealed record DoorInteractionRequest(Guid Id, Guid DoorId, string ActorId, string Side, Vector3Value ActorPosition, IReadOnlySet<string> Permissions, DateTimeOffset RequestedAt);
public sealed record DoorInteractionResult(DoorInteractionRequest Request, bool Accepted, string PreviousState, string CurrentState, string Feedback, DoorTransition? Transition, bool CollisionEnabled);
public sealed record DoorLink(
    Guid SourceDoorId,
    Guid TargetDoorId,
    bool OneWay,
    string ReturnNavigationLabel,
    Guid? PolicyId = null);
public sealed record DoorLinkMutation(IReadOnlyList<DoorLink> Links, IReadOnlyList<DoorLink> Added, string Patch, string InversePatch);

public static class DoorRules
{
    public static IReadOnlyList<DoorDiagnostic> Validate(DoorDefinition definition)
    {
        var diagnostics = new List<DoorDiagnostic>(); var root = $"doors/{definition.Id}";
        if (definition.Id == Guid.Empty || definition.SchemaVersion != 1 || string.IsNullOrWhiteSpace(definition.Provenance) || string.IsNullOrWhiteSpace(definition.Slug) || string.IsNullOrWhiteSpace(definition.DisplayName)) diagnostics.Add(new("RPG_DOOR_IDENTITY_INVALID", root, "Door identity is invalid."));
        if (definition.PlacementMode == DoorPlacementMode.ArchitectureSocket && (string.IsNullOrWhiteSpace(definition.ArchitectureSocketId) || definition.KitAdapter is null)) diagnostics.Add(new("RPG_DOOR_SOCKET_ADAPTER_MISSING", $"{root}/placement", "Socket doors require an architecture socket and kit adapter."));
        if (definition.StateMachine.States.All(item => item.Id != definition.StateMachine.InitialStateId)) diagnostics.Add(new("RPG_DOOR_INITIAL_STATE_MISSING", $"{root}/states", "Initial state is missing."));
        foreach (var transition in definition.StateMachine.Transitions.Where(item => definition.StateMachine.States.All(state => state.Id != item.FromStateId) || definition.StateMachine.States.All(state => state.Id != item.ToStateId))) diagnostics.Add(new("RPG_DOOR_TRANSITION_INVALID", $"{root}/states/{transition.Trigger}", "Transition references an unknown state."));
        if (definition.Interaction.Range <= 0 || definition.Interaction.QueueCapacity < 1 || definition.Interaction.Clearance.X <= 0 || definition.Interaction.Clearance.Y <= 0 || definition.Interaction.Clearance.Z <= 0 || string.IsNullOrWhiteSpace(definition.Interaction.Authority)) diagnostics.Add(new("RPG_DOOR_INTERACTION_INVALID", $"{root}/interaction", "Range, clearance, queue and authority are required."));
        if (definition.Portal is { } portal && (portal.DestinationId == Guid.Empty || string.IsNullOrWhiteSpace(portal.SpawnAnchorId))) diagnostics.Add(new("RPG_DOOR_PORTAL_INVALID", $"{root}/portal", "Typed destination and spawn anchor are required."));
        return diagnostics;
    }

    public static DoorInteractionResult Execute(DoorDefinition definition, string currentState, DoorInteractionRequest request, int queuedRequests, IReadOnlySet<string> conditions)
    {
        if (request.DoorId != definition.Id || request.ActorId.Length == 0) throw new InvalidOperationException("RPG_DOOR_REQUEST_INVALID");
        if (queuedRequests >= definition.Interaction.QueueCapacity) return new(request, false, currentState, currentState, "door-queue-full", null, State(definition, currentState).CollisionEnabled);
        if (definition.Interaction.AllowedSide != "both" && definition.Interaction.AllowedSide != request.Side) return new(request, false, currentState, currentState, "wrong-side", null, State(definition, currentState).CollisionEnabled);
        if (definition.Portal is { } portal && !portal.Permissions.All(request.Permissions.Contains)) return new(request, false, currentState, currentState, "permission-denied", null, State(definition, currentState).CollisionEnabled);
        var transition = definition.StateMachine.Transitions.Where(item => item.FromStateId == currentState && item.Trigger == "interact" && (item.Condition.Length == 0 || conditions.Contains(item.Condition))).OrderBy(item => item.ToStateId, StringComparer.Ordinal).FirstOrDefault();
        if (transition is null) return new(request, false, currentState, currentState, definition.Interaction.FailureFeedback, null, State(definition, currentState).CollisionEnabled);
        var target = State(definition, transition.ToStateId); return new(request, true, currentState, target.Id, "accepted", transition, target.CollisionEnabled);
    }

    private static DoorStateDefinition State(DoorDefinition definition, string id) => definition.StateMachine.States.FirstOrDefault(item => item.Id == id) ?? throw new InvalidOperationException("RPG_DOOR_STATE_NOT_FOUND");
}
