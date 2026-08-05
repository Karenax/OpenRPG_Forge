using Forge.Domain.Areas;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Domain.Tests.RuntimeExtensions;

public sealed class QualityAssuranceProfileTests
{
    [Fact]
    public void Defaults_are_deterministic_complete_and_platform_specific()
    {
        var owner = Guid.Parse("2f790000-0000-4000-8000-000000000001");

        var first = QualityAssuranceProfile.CreateDefault(owner);
        var second = QualityAssuranceProfile.CreateDefault(owner);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(3, first.Network.ClientCount);
        Assert.Equal([0, 3, 8], first.Network.LatencyTicks);
        Assert.Equal([0, 10, 25], first.Network.LossPercentages);
        Assert.True(first.Network.TestReconnect && first.Network.TestContentMismatch && first.Network.TestAuthorityAttacks);
        Assert.Equal(8, first.Visual.Resolutions.Count * first.Visual.CameraFixtures.Count * first.Visual.QualityTiers.Count);
        Assert.Contains(RenderQualityTier.High, first.Visual.QualityTiers);
        Assert.Equal(Enum.GetValues<AreaBenchmarkKind>(), first.Benchmarks.Scenarios);
        Assert.Equal(["dedicated-server", "desktop", "mobile"], first.PlatformBudgets.Select(item => item.Platform));
        Assert.Equal(Enum.GetValues<QualityTraceSurface>(), first.Profiler.Surfaces);
        Assert.True(first.Profiler.OneClickCapture);
    }

    [Fact]
    public void Invalid_visual_and_platform_limits_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new VisualRegressionProfile(Guid.NewGuid(), 1,
            ["320x200"], ["overview"], [RenderQualityTier.High], .02, ".forge/quality/visual", "test"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformPerformanceBudget(Guid.NewGuid(), 1,
            "desktop", .5, 4096, 2048, 12_000, 32, 4, 4096, .8, true, "test"));
    }
}
