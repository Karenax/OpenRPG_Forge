namespace Forge.Domain.Spatial;

public readonly record struct QuaternionValue
{
    public static QuaternionValue Identity { get; } = new(0, 0, 0, 1);

    public float X { get; }

    public float Y { get; }

    public float Z { get; }

    public float W { get; }

    public QuaternionValue(float x, float y, float z, float w)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || !float.IsFinite(w))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Quaternion components must be finite.");
        }

        if (x == 0 && y == 0 && z == 0 && w == 0)
        {
            throw new ArgumentException("A quaternion cannot have zero length.", nameof(w));
        }

        X = x;
        Y = y;
        Z = z;
        W = w;
    }
}
