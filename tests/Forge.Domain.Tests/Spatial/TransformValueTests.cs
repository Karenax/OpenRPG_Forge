using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Spatial;

public sealed class TransformValueTests
{
    [Fact]
    public void Identity_HasExpectedComponents()
    {
        Assert.Equal(Vector3Value.Zero, TransformValue.Identity.Position);
        Assert.Equal(QuaternionValue.Identity, TransformValue.Identity.Rotation);
        Assert.Equal(Vector3Value.One, TransformValue.Identity.Scale);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveScale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransformValue(
            Vector3Value.Zero,
            QuaternionValue.Identity,
            new Vector3Value(1, 0, 1)));
    }

    [Fact]
    public void Vector_RejectsNonFiniteComponents()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vector3Value(float.NaN, 0, 0));
    }
}
