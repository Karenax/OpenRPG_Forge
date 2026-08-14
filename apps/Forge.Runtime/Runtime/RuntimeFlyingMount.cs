using Forge.EngineAdapter.Assets;
using Godot;

namespace Forge.Runtime;

internal partial class RuntimeFlyingMount : CharacterBody3D
{
    private const float CruiseSpeed = 19f;
    private const float VerticalSpeed = 10f;
    private const float Acceleration = 34f;
    private const float MinimumGroundOffset = 1.15f;
    private const float MaximumAltitude = 34f;
    private const float RiderHeight = 1.55f;
    private const float RiderScale = .62f;
    private PlayerController? _rider;
    private Camera3D? _movementCamera;
    private GodotAssetAnimationState? _animations;
    private Label3D? _interactionPrompt;
    private Rect2 _horizontalBounds;
    private uint _riderCollisionLayer;
    private uint _riderCollisionMask;
    private Vector3 _riderOriginalScale;
    private readonly List<Label3D> _hiddenRiderLabels = [];
    private Vector3 _mountedAt;

    internal bool IsMounted => _rider is not null;

    internal bool ControlLocked { get; set; }

    internal float DistanceTravelled { get; private set; }

    internal float MaximumHeightAboveGround { get; private set; }

    internal void Configure(Rect2 horizontalBounds, Camera3D? movementCamera)
    {
        if (horizontalBounds.Size.X <= 4 || horizontalBounds.Size.Y <= 4)
            throw new ArgumentOutOfRangeException(nameof(horizontalBounds));
        _horizontalBounds = horizontalBounds;
        _movementCamera = movementCamera;
    }

    internal void ConfigureAnimations(GodotAssetAnimationState animations)
    {
        _animations = animations ?? throw new ArgumentNullException(nameof(animations));
        GodotAssetAnimationController.Play(_animations, _animations.IdleClip, 0);
    }

    internal void ConfigureInteractionPrompt(Label3D prompt)
    {
        _interactionPrompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
    }

    internal void Mount(PlayerController rider)
    {
        ArgumentNullException.ThrowIfNull(rider);
        if (_rider is not null) return;
        _rider = rider;
        _riderCollisionLayer = rider.CollisionLayer;
        _riderCollisionMask = rider.CollisionMask;
        _riderOriginalScale = rider.Scale;
        rider.ControlLocked = true;
        rider.Visible = true;
        rider.Scale = _riderOriginalScale * RiderScale;
        _hiddenRiderLabels.Clear();
        foreach (var node in rider.FindChildren("*", "Label3D", recursive: true, owned: false))
        {
            if (node is not Label3D label || !label.Visible) continue;
            label.Visible = false;
            _hiddenRiderLabels.Add(label);
        }
        if (_interactionPrompt is not null) _interactionPrompt.Visible = false;
        rider.CollisionLayer = 0;
        rider.CollisionMask = 0;
        var ground = GroundHeight(GlobalPosition);
        GlobalPosition = new Vector3(GlobalPosition.X,
            MathF.Max(GlobalPosition.Y, ground + MinimumGroundOffset), GlobalPosition.Z);
        _mountedAt = GlobalPosition;
        SyncRiderTransform();
        rider.ResetPhysicsInterpolation();
        ResetPhysicsInterpolation();
    }

    internal bool TryDismount()
    {
        if (_rider is null) return false;
        var ground = GroundHeight(GlobalPosition);
        if (GlobalPosition.Y > ground + 2.35f) return false;

        var rider = _rider;
        _rider = null;
        rider.Visible = true;
        rider.Scale = _riderOriginalScale;
        foreach (var label in _hiddenRiderLabels.Where(GodotObject.IsInstanceValid)) label.Visible = true;
        _hiddenRiderLabels.Clear();
        if (_interactionPrompt is not null) _interactionPrompt.Visible = true;
        rider.CollisionLayer = _riderCollisionLayer;
        rider.CollisionMask = _riderCollisionMask;
        var right = GlobalTransform.Basis.X;
        right.Y = 0;
        if (right.LengthSquared() < .01f) right = Vector3.Right;
        var destination = GlobalPosition + right.Normalized() * 2.1f;
        destination = ClampHorizontal(destination);
        destination.Y = GroundHeight(destination) + 1f;
        rider.GlobalPosition = destination;
        rider.ConfigureFallRecovery(destination, 30);
        rider.ResetPhysicsInterpolation();
        rider.ControlLocked = false;
        Velocity = Vector3.Zero;
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_rider is null || ControlLocked)
        {
            Velocity = Vector3.Zero;
            return;
        }

        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var direction = ResolveMovementDirection(input);
        var vertical = Input.GetActionStrength("jump") - Input.GetActionStrength("dive");
        var target = direction * CruiseSpeed;
        target.Y = vertical * VerticalSpeed;
        var step = Acceleration * (float)Math.Max(0, delta);
        Velocity = new Vector3(
            Mathf.MoveToward(Velocity.X, target.X, step),
            Mathf.MoveToward(Velocity.Y, target.Y, step),
            Mathf.MoveToward(Velocity.Z, target.Z, step));

        var previous = GlobalPosition;
        MoveAndSlide();
        var ground = GroundHeight(GlobalPosition);
        var clamped = ClampHorizontal(GlobalPosition);
        clamped.Y = Math.Clamp(clamped.Y, ground + MinimumGroundOffset, ground + MaximumAltitude);
        GlobalPosition = clamped;
        DistanceTravelled += previous.DistanceTo(GlobalPosition);
        MaximumHeightAboveGround = MathF.Max(MaximumHeightAboveGround, GlobalPosition.Y - ground);

        SyncRiderTransform();
        if (direction.LengthSquared() > .01f)
            LookAt(GlobalPosition + direction, Vector3.Up);
        AnimateVisual(input, vertical);
        if (_animations is not null)
            GodotAssetAnimationController.Play(_animations, _animations.RunClip);
    }

    private Vector3 ResolveMovementDirection(Vector2 input)
    {
        if (input.LengthSquared() <= .001f) return Vector3.Zero;
        if (_movementCamera is null) return new Vector3(input.X, 0, input.Y).Normalized();
        var forward = -_movementCamera.GlobalTransform.Basis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        var right = _movementCamera.GlobalTransform.Basis.X;
        right.Y = 0;
        right = right.Normalized();
        return ((right * input.X) - (forward * input.Y)).Normalized();
    }

    private Vector3 ClampHorizontal(Vector3 position)
    {
        const float clearance = 3f;
        return new Vector3(
            Math.Clamp(position.X, _horizontalBounds.Position.X + clearance,
                _horizontalBounds.End.X - clearance),
            position.Y,
            Math.Clamp(position.Z, _horizontalBounds.Position.Y + clearance,
                _horizontalBounds.End.Y - clearance));
    }

    private float GroundHeight(Vector3 position)
    {
        var query = PhysicsRayQueryParameters3D.Create(
            new Vector3(position.X, position.Y + 48f, position.Z),
            new Vector3(position.X, position.Y - 96f, position.Z));
        query.Exclude = _rider is null ? [GetRid()] : [GetRid(), _rider.GetRid()];
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.TryGetValue("position", out var point) ? point.AsVector3().Y : _mountedAt.Y - 1f;
    }

    private void AnimateVisual(Vector2 input, float vertical)
    {
        var visual = GetNodeOrNull<Node3D>("AuthoredVisual");
        if (visual is null) return;
        var target = new Vector3(Mathf.DegToRad(-vertical * 8f), visual.Rotation.Y,
            Mathf.DegToRad(-input.X * 15f));
        visual.Rotation = visual.Rotation.Lerp(target, .12f);
    }

    private void SyncRiderTransform()
    {
        if (_rider is null) return;
        _rider.GlobalPosition = GlobalPosition + (Vector3.Up * RiderHeight);
        _rider.Rotation = new Vector3(0, Rotation.Y, 0);
        _rider.ResetPhysicsInterpolation();
    }
}
