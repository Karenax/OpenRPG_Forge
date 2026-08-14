using Godot;
using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;

namespace Forge.Runtime;

internal sealed class RuntimeCameraRig
{
    private float _minimumDistance = 4.5f;
    private float _maximumDistance = 32f;
    private float _yaw = Mathf.DegToRad(39f);
    private float _pitch = Mathf.DegToRad(24f);
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

    internal bool AlignCharacterFacing => _rightOrbiting && _alignCharacterFacing;

    internal Vector3 ForwardDirection => new(-MathF.Sin(_yaw), 0, -MathF.Cos(_yaw));

    internal float MaximumFocusStep { get; private set; }

    internal float MaximumVerticalFocusStep { get; private set; }

    internal float MaximumFocusStepDelta { get; private set; }

    internal bool ImpulseActive => _impulseRemaining > 0;

    internal float MaximumAppliedImpulse { get; private set; }

    internal void Configure(GameplayCameraCatalog catalog)
    {
        var profile = GameplayFoundationRuntime.ResolveCamera(catalog);
        _distance = profile.Distance;
        _minimumDistance = profile.MinimumDistance;
        _maximumDistance = profile.MaximumDistance;
        _pitch = Mathf.DegToRad(profile.PitchDegrees);
        _focusOffset = new(profile.FocusOffset.X, profile.FocusOffset.Y, profile.FocusOffset.Z);
        _alignCharacterFacing = profile.AlignCharacterFacing;
        _fieldOfView = profile.FieldOfViewDegrees;
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
                _distance = Math.Max(_minimumDistance, _distance - Math.Max(1.2f, _distance * .12f));
            }
            else if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown)
            {
                _distance = Math.Min(_maximumDistance, _distance + Math.Max(1.2f, _distance * .12f));
            }
        }
        else if (@event is InputEventMouseMotion motion && (_leftOrbiting || _middleOrbiting || _rightOrbiting))
        {
            _yaw -= motion.Relative.X * .0065f;
            _pitch = Math.Clamp(_pitch + motion.Relative.Y * .0055f,
                Mathf.DegToRad(10f), Mathf.DegToRad(70f));
        }
    }

    internal void Update(
        Camera3D camera,
        Vector3 target,
        double delta,
        Func<Vector3, Vector3, Vector3>? constrainPosition = null)
    {
        camera.Fov = _fieldOfView;
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
        if (constrainPosition is not null) desired = constrainPosition(focus, desired);
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
        camera.Fov = _fieldOfView;
        _impulseRemaining = 0;
        _impulseDuration = 0;
        _impulseAmplitude = 0;
        _smoothedTarget = target;
        _hasSmoothedTarget = true;
        var horizontal = MathF.Cos(_pitch) * _distance;
        var focus = target + _focusOffset;
        var desired = focus + new Vector3(
            MathF.Sin(_yaw) * horizontal,
            MathF.Sin(_pitch) * _distance,
            MathF.Cos(_yaw) * horizontal);
        camera.GlobalPosition = constrainPosition?.Invoke(focus, desired) ?? desired;
        camera.LookAt(focus, Vector3.Up);
        _lastMeasuredFocus = focus;
        _hasMeasuredFocus = true;
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
