using Forge.Domain.Entities;

namespace Forge.Domain.Tests.Entities;

public sealed class EntityIdTests
{
    [Fact]
    public void From_RejectsEmptyGuid()
    {
        var exception = Assert.Throws<ArgumentException>(() => EntityId.From(Guid.Empty));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Parse_RoundTripsCanonicalValue()
    {
        var value = Guid.Parse("c8ab819d-bbd5-49a1-91c8-d95e0cc09e03");

        var entityId = EntityId.Parse(value.ToString("D"));

        Assert.Equal(value, entityId.Value);
        Assert.Equal(value.ToString("D"), entityId.ToString());
    }

    [Fact]
    public void Parse_RejectsInvalidValue()
    {
        Assert.Throws<FormatException>(() => EntityId.Parse("not-an-id"));
    }

    [Fact]
    public void Equality_UsesTheOpaqueValue()
    {
        var value = Guid.Parse("c8ab819d-bbd5-49a1-91c8-d95e0cc09e03");

        Assert.Equal(EntityId.From(value), EntityId.From(value));
    }
}
