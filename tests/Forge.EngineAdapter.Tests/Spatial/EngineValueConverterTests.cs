using Forge.Domain.Spatial;
using Forge.EngineAdapter.Spatial;
using Godot;

namespace Forge.EngineAdapter.Tests.Spatial;

public sealed class EngineValueConverterTests
{
    [Fact]
    public void ToEngine_ConvertsVectorComponents()
    {
        var converted = EngineValueConverter.ToEngine(new Vector3Value(1.5f, -2, 3.25f));

        Assert.Equal(new Vector3(1.5f, -2, 3.25f), converted);
    }

    [Fact]
    public void ToEngine_ConvertsTransformOriginRotationAndScale()
    {
        var source = new TransformValue(
            new Vector3Value(4, 5, 6),
            QuaternionValue.Identity,
            new Vector3Value(2, 3, 4));

        var converted = EngineValueConverter.ToEngine(source);

        Assert.Equal(new Vector3(4, 5, 6), converted.Origin);
        Assert.Equal(new Vector3(2, 3, 4), converted.Basis.Scale);
        Assert.Equal(Quaternion.Identity, converted.Basis.GetRotationQuaternion());
    }
}
