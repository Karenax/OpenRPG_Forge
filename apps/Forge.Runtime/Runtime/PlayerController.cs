using Godot;
using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;
using Forge.EngineAdapter.Assets;

namespace Forge.Runtime;

public partial class PlayerController : CharacterBody3D
{
    private const float Gravity = 24.0f;
    internal const float MovementSpeedMultiplier = 2.0f;
    internal const float JumpVelocity = 9.0f;
    private ModuleGameplayControlProfile? _profile;
    private GodotAssetAnimationState? _animations;
    private double _oneShotRemainingSeconds;
    private float _breath = 45;
    private float _stamina = 100;
    private bool _jumpQueued;

    public bool ControlLocked { get; set; }

    public float SimulationScale { get; set; } = 1;

    public Camera3D? MovementCamera { get; set; }

    public MovementCapabilityKind CurrentCapability { get; private set; } = MovementCapabilityKind.Ground;

    public float BreathRemaining => _breath;

    public float StaminaRemaining => _stamina;

    public int AttackAnimationCount { get; private set; }

    public int JumpCount { get; private set; }

    public string? CurrentAnimationClip => _animations?.CurrentClip;

    public void Configure(ModuleGameplayControlProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _breath = profile.Swimming.BreathSeconds;
        _stamina = profile.Climbing.StaminaCapacity;
    }

    public void ConfigureAnimations(GodotAssetAnimationState animations)
    {
        _animations = animations ?? throw new ArgumentNullException(nameof(animations));
        GodotAssetAnimationController.Play(animations, animations.IdleClip, 0);
    }

    public bool PlayAttackAnimation()
    {
        if (_animations?.AttackClip is not { } clip) return false;
        if (!GodotAssetAnimationController.Play(_animations, clip, .08, restart: true)) return false;
        _oneShotRemainingSeconds = Math.Max(
            .05,
            GodotAssetAnimationController.DurationSeconds(_animations, clip));
        AttackAnimationCount++;
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        _oneShotRemainingSeconds = Math.Max(0, _oneShotRemainingSeconds - delta);
        if (_profile is null || ControlLocked)
        {
            Velocity = Vector3.Zero;
            PlayMovementAnimation(Vector3.Zero, airborne: false);
            return;
        }

        var scaledDelta = (float)delta * Math.Clamp(SimulationScale, .05f, 1);
        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        if (Input.IsMouseButtonPressed(MouseButton.Left) && Input.IsMouseButtonPressed(MouseButton.Right))
        {
            input.Y = -1;
        }
        var direction = ResolveMovementDirection(input);
        var velocity = Velocity;
        var water = ResolveWaterContact();
        var climb = IsOnAuthoredClimbSurface();
        var decision = MovementCapabilityRuntime.Resolve(_profile,
            new MovementCapabilityContext(water.InWater, Input.IsActionPressed("dive"), climb,
                Input.IsActionPressed("climb"), _breath, _stamina), scaledDelta);
        CurrentCapability = decision.Kind;
        _breath = decision.BreathRemaining;
        _stamina = decision.StaminaRemaining;
        var movementSpeed = decision.Speed * MovementSpeedMultiplier;
        velocity.X = (direction.X * movementSpeed * SimulationScale) + water.Current.X;
        velocity.Z = (direction.Z * movementSpeed * SimulationScale) + water.Current.Z;
        var jumpRequested = _jumpQueued || Input.IsActionJustPressed("jump");
        _jumpQueued = false;
        if (decision.Kind == MovementCapabilityKind.Ground && jumpRequested && IsOnFloor())
        {
            velocity.Y = JumpVelocity;
            JumpCount++;
        }
        else velocity.Y = decision.Kind switch
        {
            MovementCapabilityKind.Swimming => Mathf.MoveToward(velocity.Y, decision.VerticalAcceleration,
                _profile.Swimming.Buoyancy * scaledDelta),
            MovementCapabilityKind.Diving => Input.IsActionPressed("dive") ? -decision.Speed : velocity.Y,
            MovementCapabilityKind.Climbing when decision.Accepted => input.Y * decision.Speed,
            _ => IsOnFloor() ? -0.1f : velocity.Y - (Gravity * scaledDelta),
        };

        Velocity = velocity;
        MoveAndSlide();
        PlayMovementAnimation(direction, !IsOnFloor() && Math.Abs(Velocity.Y) > .2f);

        if (direction.LengthSquared() > 0.01f)
        {
            FaceDirection(direction);
        }
    }

    internal bool QueueJump()
    {
        if (_profile is null || ControlLocked || CurrentCapability != MovementCapabilityKind.Ground || !IsOnFloor())
        {
            return false;
        }

        _jumpQueued = true;
        return true;
    }

    internal void FaceDirection(Vector3 direction)
    {
        direction.Y = 0;
        if (direction.LengthSquared() <= .001f) return;
        LookAt(GlobalPosition + direction.Normalized(), Vector3.Up);
    }

    private void PlayMovementAnimation(Vector3 direction, bool airborne)
    {
        if (_animations is null || _oneShotRemainingSeconds > 0) return;
        var clip = airborne
            ? _animations.JumpClip
            : direction.LengthSquared() > .01f
                ? _animations.RunClip
                : _animations.IdleClip;
        GodotAssetAnimationController.Play(_animations, clip);
    }

    private Vector3 ResolveMovementDirection(Vector2 input)
    {
        if (input.LengthSquared() <= .001f) return Vector3.Zero;
        if (MovementCamera is null) return new Vector3(input.X, 0, input.Y).Normalized();
        var forward = -MovementCamera.GlobalTransform.Basis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        var right = MovementCamera.GlobalTransform.Basis.X;
        right.Y = 0;
        right = right.Normalized();
        return ((right * input.X) - (forward * input.Y)).Normalized();
    }

    private WaterContact ResolveWaterContact()
    {
        foreach (var node in GetTree().GetNodesInGroup("forge_water_volume"))
        {
            if (node is Area3D area && area.GetMeta("forge_swimming_enabled", true).AsBool() &&
                area.GetOverlappingBodies().Any(body => body == this))
            {
                var direction = area.GetMeta("forge_flow_direction", Vector3.Zero).AsVector3();
                var speed = area.GetMeta("forge_flow_speed", 0).AsSingle();
                return new(true, direction.Normalized() * speed);
            }
        }

        return new(false, Vector3.Zero);
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

    private readonly record struct WaterContact(bool InWater, Vector3 Current);
}
