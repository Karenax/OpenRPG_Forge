using Forge.Application.Areas;
using Forge.Application.Tests.Entities;

namespace Forge.Application.Tests.Areas;

public sealed class AreaPerformanceAnalyzerTests
{
    [Fact]
    public void Reports_asset_budget_occlusion_sectors_and_lod_tiers_deterministically()
    {
        var context = new PlaceEntityTestContext();
        var placed = context.Handler.Handle(context.State, context.CreateCommand());
        var report = AreaPerformanceAnalyzer.Analyze(placed.State.Project, context.AreaId,
            new AreaPerformanceBudget(0, 0, 0));

        Assert.Equal(3, report.Diagnostics.Count);
        Assert.False(report.IsWithinBudget);
        Assert.Equal(1, report.OccupiedOcclusionSectors);
        Assert.Equal(1, report.NearLodCount);
    }
}
