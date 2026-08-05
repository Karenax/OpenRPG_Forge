using Godot;
using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;

namespace Forge.Runtime;

public partial class PlayerController : CharacterBody3D
{
    private const float Gravity = 24.0f;
    private ModuleGameplayControlProfile? _profile;
    private float _breath = 45;
    private float _stamina = 100;

    public bool ControlLocked { get; set; }

    public float SimulationScale { get; set; } = 1;

    public MovementCapabilityKind CurrentCapability { get; private set; } = MovementCapabilityKind.Ground;

    public float BreathRemaining => _breath;

    public float StaminaRemaining => _stamina;

    public void Configure(ModuleGameplayControlProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _breath = profile.Swimming.BreathSeconds;
        _stamina = profile.Climbing.StaminaCapacity;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_profile is null || ControlLocked)
        {
            Velocity = Vector3.Zero;
            return;
        }

        var scaledDelta = (float)delta * Math.Clamp(SimulationScale, .05f, 1);
        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var direction = new Vector3(input.X, 0, input.Y).Normalized();
        var velocity = Velocity;
        var inWater = IsInsideWater();
        var climb = IsOnAuthoredClimbSurface();
        var decision = MovementCapabilityRuntime.Resolve(_profile,
            new MovementCapabilityContext(inWater, Input.IsActionPressed("dive"), climb,
                Input.IsActionPressed("climb"), _breath, _stamina), scaledDelta);
        CurrentCapability = decision.Kind;
        _breath = decision.BreathRemaining;
        _stamina = decision.StaminaRemaining;
        velocity.X = direction.X * decision.Speed * SimulationScale;
        velocity.Z = direction.Z * decision.Speed * SimulationScale;
        velocity.Y = decision.Kind switch
        {
            MovementCapabilityKind.Swimming => Mathf.MoveToward(velocity.Y, decision.VerticalAcceleration,
                _profile.Swimming.Buoyancy * scaledDelta),
            MovementCapabilityKind.Diving => Input.IsActionPressed("dive") ? -decision.Speed : velocity.Y,
            MovementCapabilityKind.Climbing when decision.Accepted => input.Y * decision.Speed,
            _ => IsOnFloor() ? -0.1f : velocity.Y - (Gravity * scaledDelta),
        };

        Velocity = velocity;
        MoveAndSlide();

        if (direction.LengthSquared() > 0.01f)
        {
            LookAt(GlobalPosition + direction, Vector3.Up);
        }
    }

    private bool IsInsideWater()
    {
        foreach (var node in GetTree().GetNodesInGroup("forge_water_volume"))
        {
            if (node is Area3D area && area.GetMeta("forge_swimming_enabled", true).AsBool() &&
                area.GetOverlappingBodies().Any(body => body == this)) return true;
        }

        return false;
    }

    private bool IsOnAuthoredClimbSurface()
    {
        if (_profile?.Climbing.Enabled != true) return false;
        var forward = -GlobalTransform.Basis.Z;
        var query = PhysicsRayQueryParameters3D.Create(GlobalPosition + Vector3.Up * .7f,
            GlobalPosition + Vector3.Up * .7f + forward * _profile.Climbing.LedgeReach);
        query.Exclude = [GetRid()];
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || !hit.TryGetValue("collider", out var collider)) return false;
        return collider.AsGodotObject() is Node node && node.GetMeta("forge_climb", false).AsBool();
    }
}
