namespace Forge.Domain.Spatial;

public readonly record struct Vector3Value
{
    public static Vector3Value Zero { get; } = new(0, 0, 0);

    public static Vector3Value One { get; } = new(1, 1, 1);

    public float X { get; }

    public float Y { get; }

    public float Z { get; }

    public Vector3Value(float x, float y, float z)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Vector components must be finite.");
        }

        X = x;
        Y = y;
        Z = z;
    }
}
