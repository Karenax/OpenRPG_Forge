using Forge.Studio.Core.Accessibility;

namespace Forge.Studio.Core.Tests.Accessibility;

public sealed class StudioDisplayScaleTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("1", 1.0)]
    [InlineData("1.5", 1.5)]
    [InlineData("2.0", 2.0)]
    [InlineData("0.75", 0.75)]
    public void OverrideUsesInvariantSupportedScale(string? value, double? expected)
    {
        Assert.True(StudioDisplayScale.TryParseOverride(value, out var scale));
        Assert.Equal(expected, scale);
    }

    [Theory]
    [InlineData("0.74")]
    [InlineData("2.01")]
    [InlineData("1,5")]
    [InlineData("not-a-scale")]
    public void OverrideRejectsUnsupportedOrLocaleDependentValues(string value)
    {
        Assert.False(StudioDisplayScale.TryParseOverride(value, out var scale));
        Assert.Null(scale);
    }

    [Fact]
    public void RequestedScaleOverridesThePersistedProfile()
    {
        Assert.Equal(2.0, StudioDisplayScale.Resolve(2.0, 1.25));
        Assert.Equal(1.25, StudioDisplayScale.Resolve(null, 1.25));
    }

    [Fact]
    public void InvalidPersistedScaleIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StudioDisplayScale.Resolve(null, 2.5));
    }
}
