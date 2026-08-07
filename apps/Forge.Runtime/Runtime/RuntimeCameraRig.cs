using Godot;

namespace Forge.Runtime;

internal sealed class RuntimeCameraRig
{
    private const float MinimumDistance = 4.5f;
    private const float MaximumDistance = 32f;
    private float _yaw = Mathf.DegToRad(39f);
    private float _pitch = Mathf.DegToRad(24f);
    private float _distance = 14f;
    private bool _leftOrbiting;
    private bool _rightOrbiting;
    private bool _middleOrbiting;

    internal bool AlignCharacterFacing => _rightOrbiting;

    internal Vector3 ForwardDirection => new(-MathF.Sin(_yaw), 0, -MathF.Cos(_yaw));

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
                _distance = Math.Max(MinimumDistance, _distance - Math.Max(1.2f, _distance * .12f));
            }
            else if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown)
            {
                _distance = Math.Min(MaximumDistance, _distance + Math.Max(1.2f, _distance * .12f));
            }
        }
        else if (@event is InputEventMouseMotion motion && (_leftOrbiting || _middleOrbiting || _rightOrbiting))
        {
            _yaw -= motion.Relative.X * .0065f;
            _pitch = Math.Clamp(_pitch + motion.Relative.Y * .0055f,
                Mathf.DegToRad(10f), Mathf.DegToRad(70f));
        }
    }

    internal void Update(Camera3D camera, Vector3 target, double delta)
    {
        var rotation = Input.GetAxis("camera_left", "camera_right");
        _yaw += rotation * (float)delta * 1.35f;
        var horizontal = MathF.Cos(_pitch) * _distance;
        var focus = target + Vector3.Up * 1.25f;
        var desired = focus + new Vector3(
            MathF.Sin(_yaw) * horizontal,
            MathF.Sin(_pitch) * _distance,
            MathF.Cos(_yaw) * horizontal);
        var amount = 1f - MathF.Exp(-12f * (float)delta);
        camera.GlobalPosition = camera.GlobalPosition.Lerp(desired, amount);
        camera.LookAt(focus, Vector3.Up);
    }

    internal void Snap(Camera3D camera, Vector3 target)
    {
        var horizontal = MathF.Cos(_pitch) * _distance;
        var focus = target + Vector3.Up * 1.25f;
        camera.GlobalPosition = focus + new Vector3(
            MathF.Sin(_yaw) * horizontal,
            MathF.Sin(_pitch) * _distance,
            MathF.Cos(_yaw) * horizontal);
        camera.LookAt(focus, Vector3.Up);
    }
}
