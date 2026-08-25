using Godot;
using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;

namespace Forge.Runtime;

internal sealed class RuntimeCameraRig
{
    internal const float MouseYawSensitivity = .0032f;
    internal const float MousePitchSensitivity = .0028f;
    internal const float CollisionRetractSharpness = 24f;
    internal const float CollisionReleaseSharpness = 3.25f;
    internal const float FirstPersonFieldOfView = 75f;

    private float _minimumDistance = 4.5f;
    private float _maximumDistance = 32f;
    private float _yaw = Mathf.DegToRad(39f);
    private float _pitch = Mathf.DegToRad(24f);
    private float _minimumPitch = Mathf.DegToRad(-85f);
    private float _maximumPitch = Mathf.DegToRad(85f);
    private float _distance = 14f;
    private bool _leftOrbiting;
    private bool _rightOrbiting;
    private bool _middleOrbiting;
    private Vector3 _focusOffset = Vector3.Up * 1.25f;
    private bool _alignCharacterFacing = true;
    private float _fieldOfView = 60;
    private Vector3 _smoothedTarget;
    private Vector3 _lastMeasuredFocus;
    private Vector3 _lastMeasuredFocusStep;
    private bool _hasSmoothedTarget;
    private bool _hasMeasuredFocus;
    private bool _hasMeasuredFocusStep;
    private double _impulseRemaining;
    private double _impulseDuration;
    private float _impulseAmplitude;
    private int _impulseSequence;
    private float _collisionDistance;
    private bool _hasCollisionDistance;
    private bool _firstPersonActive;
    private float _firstPersonPitch;

    internal bool AlignCharacterFacing => _rightOrbiting && _alignCharacterFacing;

    internal bool FirstPersonActive => _firstPersonActive;

    internal float PitchDegrees => Mathf.RadToDeg(_pitch);

    internal float FirstPersonPitchDegrees => Mathf.RadToDeg(_firstPersonPitch);

    internal float MinimumPitchDegrees => Mathf.RadToDeg(_minimumPitch);

    internal float MaximumPitchDegrees => Mathf.RadToDeg(_maximumPitch);

    internal Vector3 ForwardDirection => new(-MathF.Sin(_yaw), 0, -MathF.Cos(_yaw));

    internal float MaximumFocusStep { get; private set; }

    internal float MaximumVerticalFocusStep { get; private set; }

    internal float MaximumFocusStepDelta { get; private set; }

    internal bool ImpulseActive => _impulseRemaining > 0;

    internal float MaximumAppliedImpulse { get; private set; }

    internal float MaximumCollisionDistanceStep { get; private set; }

    internal float TerrainClearance { get; private set; } = .9f;

    internal int TerrainProbeSamples { get; private set; } = 32;

    internal void Configure(GameplayCameraCatalog catalog)
    {
        var profile = GameplayFoundationRuntime.ResolveCamera(catalog);
        _distance = profile.Distance;
        _minimumDistance = profile.MinimumDistance;
        _maximumDistance = profile.MaximumDistance;
        _minimumPitch = Mathf.DegToRad(profile.MinimumPitchDegrees);
        _maximumPitch = Mathf.DegToRad(profile.MaximumPitchDegrees);
        _pitch = Mathf.DegToRad(profile.PitchDegrees);
        _focusOffset = new(profile.FocusOffset.X, profile.FocusOffset.Y, profile.FocusOffset.Z);
        _alignCharacterFacing = profile.AlignCharacterFacing;
        _fieldOfView = profile.FieldOfViewDegrees;
        TerrainClearance = profile.TerrainClearance;
        TerrainProbeSamples = profile.TerrainProbeSamples;
        _yaw = Mathf.DegToRad(profile.InitialYawDegrees);
        _firstPersonActive = false;
        _firstPersonPitch = Math.Clamp(0, _minimumPitch, _maximumPitch);
    }

    internal void SetYawDegrees(float degrees) => _yaw = Mathf.DegToRad(degrees);

    internal void AddImpulse(float meters, double durationSeconds)
    {
        if (!float.IsFinite(meters) || meters < 0 || meters > .35f)
            throw new ArgumentOutOfRangeException(nameof(meters));
        if (!double.IsFinite(durationSeconds) || durationSeconds < 0 || durationSeconds > .35)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        if (meters <= 0 || durationSeconds <= 0) return;
        _impulseAmplitude = Math.Max(_impulseAmplitude, meters);
        _impulseDuration = Math.Max(_impulseDuration, durationSeconds);
        _impulseRemaining = Math.Max(_impulseRemaining, durationSeconds);
        _impulseSequence++;
    }

    internal void ResetMotionDiagnostics()
    {
        MaximumFocusStep = 0;
        MaximumVerticalFocusStep = 0;
        MaximumFocusStepDelta = 0;
        MaximumAppliedImpulse = 0;
        MaximumCollisionDistanceStep = 0;
        _hasMeasuredFocus = false;
        _hasMeasuredFocusStep = false;
    }

    internal void HandleInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex is MouseButton.Left or MouseButton.Middle or MouseButton.Right)
            {
                if (button.ButtonIndex == MouseButton.Left) _leftOrbiting = button.Pressed;
                if (button.ButtonIndex == MouseButton.Middle) _middleOrbiting = button.Pressed;
                if (button.ButtonIndex == MouseButton.Right) _rightOrbiting = button.Pressed;
                Input.MouseMode = _leftOrbiting || _middleOrbiting || _rightOrbiting
                    ? Input.MouseModeEnum.Captured
                    : Input.MouseModeEnum.Visible;
            }
            else if (button.Pressed && button.ButtonIndex == MouseButton.WheelUp)
            {
                if (_firstPersonActive) return;
                _distance = Math.Max(_minimumDistance, _distance - ZoomStep(_distance));
                if (_distance <= _minimumDistance + .001f)
                {
                    _distance = _minimumDistance;
                    _firstPersonActive = true;
                    _firstPersonPitch = Math.Clamp(0, _minimumPitch, _maximumPitch);
                    _hasCollisionDistance = false;
                }
            }
            else if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown)
            {
                if (_firstPersonActive)
                {
                    _firstPersonActive = false;
                    _distance = Math.Min(_maximumDistance, _minimumDistance + ZoomStep(_minimumDistance));
                    _hasCollisionDistance = false;
                }
                else
                {
                    _distance = Math.Min(_maximumDistance, _distance + ZoomStep(_distance));
                }
            }
        }
        else if (@event is InputEventMouseMotion motion && (_leftOrbiting || _middleOrbiting || _rightOrbiting))
        {
            _yaw -= motion.Relative.X * MouseYawSensitivity;
            if (_firstPersonActive)
            {
                _firstPersonPitch = Math.Clamp(
                    _firstPersonPitch + motion.Relative.Y * MousePitchSensitivity,
                    _minimumPitch, _maximumPitch);
            }
            else
            {
                _pitch = Math.Clamp(_pitch + motion.Relative.Y * MousePitchSensitivity,
                    _minimumPitch, _maximumPitch);
            }
        }
    }

    internal void Update(
        Camera3D camera,
        Vector3 target,
        double delta,
        Func<Vector3, Vector3, Vector3>? constrainPosition = null)
    {
        camera.Fov = _firstPersonActive ? FirstPersonFieldOfView : _fieldOfView;
        var rotation = Input.GetAxis("camera_left", "camera_right");
        _yaw += rotation * (float)delta * 1.35f;
        if (!_hasSmoothedTarget)
        {
            _smoothedTarget = target;
            _hasSmoothedTarget = true;
        }
        else
        {
            var horizontalAmount = 1f - MathF.Exp(-10f * (float)delta);
            var verticalAmount = 1f - MathF.Exp(-8f * (float)delta);
            _smoothedTarget = new Vector3(
                Mathf.Lerp(_smoothedTarget.X, target.X, horizontalAmount),
                Mathf.Lerp(_smoothedTarget.Y, target.Y, verticalAmount),
                Mathf.Lerp(_smoothedTarget.Z, target.Z, horizontalAmount));
        }
        var horizontal = MathF.Cos(_pitch) * _distance;
        var focus = _smoothedTarget + _focusOffset;
        if (_firstPersonActive)
        {
            var firstPersonAmount = 1f - MathF.Exp(-16f * (float)delta);
            camera.GlobalPosition = camera.GlobalPosition.Lerp(focus, firstPersonAmount);
            camera.LookAt(camera.GlobalPosition + FirstPersonLookDirection(), Vector3.Up);
            MeasureFocusMotion(focus);
            return;
        }
        var desired = focus + new Vector3(
            MathF.Sin(_yaw) * horizontal,
            MathF.Sin(_pitch) * _distance,
            MathF.Cos(_yaw) * horizontal);
        if (_impulseRemaining > 0 && _impulseDuration > 0)
        {
            var progress = 1 - _impulseRemaining / _impulseDuration;
            var envelope = (float)Math.Clamp(_impulseRemaining / _impulseDuration, 0, 1);
            var wave = MathF.Sin((float)progress * Mathf.Pi * 3);
            var sign = (_impulseSequence & 1) == 0 ? 1 : -1;
            var direction = (camera.GlobalTransform.Basis.X * sign + camera.GlobalTransform.Basis.Y * .28f)
                .Normalized();
            var offset = direction * (_impulseAmplitude * envelope * wave);
            desired += offset;
            MaximumAppliedImpulse = Math.Max(MaximumAppliedImpulse, offset.Length());
            _impulseRemaining = Math.Max(0, _impulseRemaining - delta);
            if (_impulseRemaining <= 0)
            {
                _impulseAmplitude = 0;
                _impulseDuration = 0;
            }
        }
        desired = ResolveCollisionDistance(focus, desired, delta, constrainPosition);
        var amount = 1f - MathF.Exp(-12f * (float)delta);
        camera.GlobalPosition = camera.GlobalPosition.Lerp(desired, amount);
        camera.LookAt(focus, Vector3.Up);
        MeasureFocusMotion(focus);
    }

    internal void Snap(
        Camera3D camera,
        Vector3 target,
        Func<Vector3, Vector3, Vector3>? constrainPosition = null)
    {
        camera.Fov = _firstPersonActive ? FirstPersonFieldOfView : _fieldOfView;
        _impulseRemaining = 0;
        _impulseDuration = 0;
        _impulseAmplitude = 0;
        _smoothedTarget = target;
        _hasSmoothedTarget = true;
        var focus = target + _focusOffset;
        if (_firstPersonActive)
        {
            camera.GlobalPosition = focus;
            camera.LookAt(focus + FirstPersonLookDirection(), Vector3.Up);
            _collisionDistance = 0;
            _hasCollisionDistance = false;
            _lastMeasuredFocus = focus;
            _hasMeasuredFocus = true;
            return;
        }
        var horizontal = MathF.Cos(_pitch) * _distance;
        var desired = focus + new Vector3(
            MathF.Sin(_yaw) * horizontal,
            MathF.Sin(_pitch) * _distance,
            MathF.Cos(_yaw) * horizontal);
        camera.GlobalPosition = constrainPosition?.Invoke(focus, desired) ?? desired;
        _collisionDistance = focus.DistanceTo(camera.GlobalPosition);
        _hasCollisionDistance = true;
        camera.LookAt(focus, Vector3.Up);
        _lastMeasuredFocus = focus;
        _hasMeasuredFocus = true;
    }

    private Vector3 ResolveCollisionDistance(
        Vector3 focus,
        Vector3 ideal,
        double delta,
        Func<Vector3, Vector3, Vector3>? constrainPosition)
    {
        var idealOffset = ideal - focus;
        var idealDistance = idealOffset.Length();
        if (idealDistance <= .001f) return ideal;
        var direction = idealOffset / idealDistance;
        var constrained = constrainPosition?.Invoke(focus, ideal) ?? ideal;
        var targetDistance = Math.Min(idealDistance, focus.DistanceTo(constrained));
        if (!_hasCollisionDistance)
        {
            _collisionDistance = targetDistance;
            _hasCollisionDistance = true;
        }
        else
        {
            var previous = _collisionDistance;
            _collisionDistance = SmoothCollisionDistance(previous, targetDistance, delta);
            MaximumCollisionDistanceStep = Math.Max(MaximumCollisionDistanceStep,
                Math.Abs(_collisionDistance - previous));
        }

        var resolved = focus + (direction * _collisionDistance);
        var safe = constrainPosition?.Invoke(focus, resolved) ?? resolved;
        var safeDistance = focus.DistanceTo(safe);
        if (safeDistance < _collisionDistance)
            _collisionDistance = safeDistance;
        return safe;
    }

    internal static float SmoothCollisionDistance(float current, float target, double delta)
    {
        var sharpness = target < current ? CollisionRetractSharpness : CollisionReleaseSharpness;
        var amount = 1f - MathF.Exp(-sharpness * Math.Max(0, (float)delta));
        return Mathf.Lerp(current, target, amount);
    }

    internal static Vector3 RaiseForTerrainClearance(
        Vector3 focus,
        Vector3 desired,
        Func<float, float, float> sampleHeight,
        float clearance,
        int sampleCount = 24)
    {
        ArgumentNullException.ThrowIfNull(sampleHeight);
        if (!float.IsFinite(clearance) || clearance <= 0)
            throw new ArgumentOutOfRangeException(nameof(clearance));
        if (sampleCount is < 4 or > 128)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));

        var raisedY = desired.Y;
        for (var index = 1; index <= sampleCount; index++)
        {
            var amount = index / (float)sampleCount;
            var x = Mathf.Lerp(focus.X, desired.X, amount);
            var z = Mathf.Lerp(focus.Z, desired.Z, amount);
            var segmentY = Mathf.Lerp(focus.Y, raisedY, amount);
            var requiredY = sampleHeight(x, z) + clearance;
            var deficit = requiredY - segmentY;
            if (deficit > 0)
                raisedY += deficit / amount;
        }
        return new Vector3(desired.X, raisedY, desired.Z);
    }

    private static float ZoomStep(float distance) => Math.Max(1.2f, distance * .12f);

    private Vector3 FirstPersonLookDirection()
    {
        var horizontal = MathF.Cos(_firstPersonPitch);
        return new Vector3(
            -MathF.Sin(_yaw) * horizontal,
            -MathF.Sin(_firstPersonPitch),
            -MathF.Cos(_yaw) * horizontal).Normalized();
    }

    private void MeasureFocusMotion(Vector3 focus)
    {
        if (_hasMeasuredFocus)
        {
            var step = focus - _lastMeasuredFocus;
            MaximumFocusStep = Math.Max(MaximumFocusStep, step.Length());
            MaximumVerticalFocusStep = Math.Max(MaximumVerticalFocusStep,
                Math.Abs(step.Y));
            if (_hasMeasuredFocusStep)
                MaximumFocusStepDelta = Math.Max(MaximumFocusStepDelta,
                    step.DistanceTo(_lastMeasuredFocusStep));
            _lastMeasuredFocusStep = step;
            _hasMeasuredFocusStep = true;
        }
        _lastMeasuredFocus = focus;
        _hasMeasuredFocus = true;
    }
}
