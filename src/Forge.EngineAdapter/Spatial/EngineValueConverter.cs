using Forge.Domain.Spatial;
using Godot;

namespace Forge.EngineAdapter.Spatial;

public static class EngineValueConverter
{
    public static Vector3 ToEngine(Vector3Value value) => new(value.X, value.Y, value.Z);

    public static Quaternion ToEngine(QuaternionValue value) => new(value.X, value.Y, value.Z, value.W);

    public static Transform3D ToEngine(TransformValue value)
    {
        var rotation = new Basis(ToEngine(value.Rotation));
        var scaledBasis = rotation.Scaled(ToEngine(value.Scale));
        return new Transform3D(scaledBasis, ToEngine(value.Position));
    }
}
