using Godot;

namespace Forge.Runtime;

internal sealed class RuntimeCameraRig
{
    private const float MinimumDistance = 8f;
    private const float MaximumDistance = 42f;
    private float _yaw = Mathf.DegToRad(39f);
    private float _pitch = Mathf.DegToRad(32f);
    private float _distance = 20f;
    private bool _orbiting;

    internal void HandleInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex is MouseButton.Middle or MouseButton.Right)
            {
                _orbiting = button.Pressed;
            }
            else if (button.Pressed && button.ButtonIndex == MouseButton.WheelUp)
            {
                _distance = Math.Max(MinimumDistance, _distance - 2f);
            }
            else if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown)
            {
                _distance = Math.Min(MaximumDistance, _distance + 2f);
            }
        }
        else if (@event is InputEventMouseMotion motion && _orbiting)
        {
            _yaw -= motion.Relative.X * .008f;
            _pitch = Math.Clamp(_pitch - motion.Relative.Y * .006f,
                Mathf.DegToRad(22f), Mathf.DegToRad(68f));
        }
    }

    internal void Update(Camera3D camera, Vector3 target, double delta)
    {
        var rotation = Input.GetAxis("camera_left", "camera_right");
        _yaw += rotation * (float)delta * 1.35f;
        var horizontal = MathF.Cos(_pitch) * _distance;
        var desired = target + new Vector3(
            MathF.Sin(_yaw) * horizontal,
            MathF.Sin(_pitch) * _distance,
            MathF.Cos(_yaw) * horizontal);
        var amount = 1f - MathF.Exp(-8f * (float)delta);
        camera.GlobalPosition = camera.GlobalPosition.Lerp(desired, amount);
        camera.LookAt(target + Vector3.Up * .9f, Vector3.Up);
    }

    internal void Snap(Camera3D camera, Vector3 target)
    {
        var horizontal = MathF.Cos(_pitch) * _distance;
        camera.GlobalPosition = target + new Vector3(
            MathF.Sin(_yaw) * horizontal,
            MathF.Sin(_pitch) * _distance,
            MathF.Cos(_yaw) * horizontal);
        camera.LookAt(target + Vector3.Up * .9f, Vector3.Up);
    }
}
