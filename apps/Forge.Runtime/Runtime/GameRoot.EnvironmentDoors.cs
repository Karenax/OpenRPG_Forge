using System.Globalization;
using Forge.Domain.Entities;
using Godot;

namespace Forge.Runtime;

public partial class GameRoot
{
    private const string DoorOpenAngleProperty = "interaction.door.openAngleDegrees";
    private const float DefaultDoorOpenAngleDegrees = 95f;
    private const double DoorTransitionSeconds = .35;

    private readonly Dictionary<Node3D, RuntimeEnvironmentDoorState> _environmentDoors = [];

    private void RegisterEnvironmentDoor(
        Node3D node,
        EntityInstance entity,
        EntityClickTriggerSettings settings)
    {
        if (settings.Action != EntityClickTriggerAction.ToggleDoor) return;
        var openAngle = float.TryParse(
                GetOverride(entity, DoorOpenAngleProperty),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) && float.IsFinite(parsed) && Math.Abs(parsed) is >= 30 and <= 160
            ? parsed
            : DefaultDoorOpenAngleDegrees;
        _environmentDoors[node] = new RuntimeEnvironmentDoorState(node.Rotation.Y, Mathf.DegToRad(openAngle));
        node.AddToGroup("forge_environment_door");
    }

    private bool ToggleEnvironmentDoor(Node3D node)
    {
        if (!_environmentDoors.TryGetValue(node, out var state) || !GodotObject.IsInstanceValid(node))
            return false;

        state.Transition?.Kill();
        state.IsOpen = !state.IsOpen;
        var targetRotation = node.Rotation;
        targetRotation.Y = state.ClosedYawRadians + (state.IsOpen ? state.OpenAngleRadians : 0);
        state.Transition = CreateTween()
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);
        state.Transition.TweenProperty(
            node,
            new NodePath("rotation"),
            targetRotation,
            DoorTransitionSeconds);
        _message = state.IsOpen
            ? $"Vous ouvrez {node.Name}."
            : $"Vous fermez {node.Name}.";
        GD.Print($"OPENRPG_FORGE_ENVIRONMENT_DOOR_TOGGLE entity={node.Name} " +
                 $"state={(state.IsOpen ? "open" : "closed")} angle={Mathf.RadToDeg(state.OpenAngleRadians):0.#}");
        return true;
    }

    private sealed class RuntimeEnvironmentDoorState(float closedYawRadians, float openAngleRadians)
    {
        public float ClosedYawRadians { get; } = closedYawRadians;
        public float OpenAngleRadians { get; } = openAngleRadians;
        public bool IsOpen { get; set; }
        public Tween? Transition { get; set; }
    }
}
