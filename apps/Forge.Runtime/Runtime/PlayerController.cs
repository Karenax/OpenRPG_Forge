using Godot;

namespace Forge.Runtime;

public partial class PlayerController : CharacterBody3D
{
    private const float Speed = 6.0f;
    private const float Gravity = 24.0f;

    public override void _PhysicsProcess(double delta)
    {
        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var direction = new Vector3(input.X, 0, input.Y).Normalized();
        var velocity = Velocity;

        velocity.X = direction.X * Speed;
        velocity.Z = direction.Z * Speed;
        velocity.Y = IsOnFloor()
            ? -0.1f
            : velocity.Y - (Gravity * (float)delta);

        Velocity = velocity;
        MoveAndSlide();

        if (direction.LengthSquared() > 0.01f)
        {
            LookAt(GlobalPosition + direction, Vector3.Up);
        }
    }
}
