using Forge.Domain.Entities;

namespace Forge.Domain.Tests.Entities;

public sealed class EntityPropertyOverridesTests
{
    [Fact]
    public void Constructor_NormalizesTypedValuesAndSortsKeys()
    {
        var overrides = new EntityPropertyOverrides(
            EntityPropertyOverrides.CurrentSchemaVersion,
            [
                new EntityPropertyOverride("locked", EntityPropertyValueKind.Boolean, "TRUE"),
                new EntityPropertyOverride("hitPoints", EntityPropertyValueKind.Integer, "0042"),
                new EntityPropertyOverride("displayName", EntityPropertyValueKind.String, "Gardien cendré"),
            ]);

        Assert.Equal(["displayName", "hitPoints", "locked"], overrides.Properties.Select(property => property.Key));
        Assert.Equal("42", overrides.Properties.Single(property => property.Key == "hitPoints").Value);
        Assert.Equal("true", overrides.Properties.Single(property => property.Key == "locked").Value);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("trailing ")]
    [InlineData("*")]
    public void Property_RejectsInvalidKeys(string key)
    {
        Assert.Throws<ArgumentException>(() =>
            new EntityPropertyOverride(key, EntityPropertyValueKind.String, "value"));
    }

    [Theory]
    [InlineData(EntityPropertyValueKind.Boolean, "yes")]
    [InlineData(EntityPropertyValueKind.Integer, "4.2")]
    [InlineData(EntityPropertyValueKind.Number, "NaN")]
    public void Property_RejectsInvalidTypedValues(EntityPropertyValueKind kind, string value)
    {
        Assert.Throws<ArgumentException>(() => new EntityPropertyOverride("key", kind, value));
    }

    [Fact]
    public void Constructor_RejectsDuplicateKeys()
    {
        Assert.Throws<ArgumentException>(() => new EntityPropertyOverrides(
            EntityPropertyOverrides.CurrentSchemaVersion,
            [
                new EntityPropertyOverride("name", EntityPropertyValueKind.String, "one"),
                new EntityPropertyOverride("name", EntityPropertyValueKind.String, "two"),
            ]));
    }

    [Fact]
    public void Constructor_RejectsUnknownSchemaVersion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EntityPropertyOverrides(2, []));
    }
}
