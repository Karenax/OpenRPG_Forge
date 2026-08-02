namespace Forge.Domain.Spatial;

public sealed record TransformValue
{
    public static TransformValue Identity { get; } = new(
        Vector3Value.Zero,
        QuaternionValue.Identity,
        Vector3Value.One);

    public Vector3Value Position { get; }

    public QuaternionValue Rotation { get; }

    public Vector3Value Scale { get; }

    public TransformValue(
        Vector3Value position,
        QuaternionValue rotation,
        Vector3Value scale)
    {
        if (scale.X <= 0 || scale.Y <= 0 || scale.Z <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale components must be greater than zero.");
        }

        Position = position;
        Rotation = rotation;
        Scale = scale;
    }
}
