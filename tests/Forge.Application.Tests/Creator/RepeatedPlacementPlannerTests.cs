using Forge.Application.Creator;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Creator;

public sealed class RepeatedPlacementPlannerTests
{
    [Fact]
    public void Grid_preserves_origin_and_uses_requested_columns()
    {
        var positions = RepeatedPlacementPlanner.Plan(new(
            new Vector3Value(10, 2, 20), 6, 2, PlacementDistribution.Grid, Columns: 3));

        Assert.Equal(6, positions.Count);
        Assert.Equal(new Vector3Value(10, 2, 20), positions[0]);
        Assert.Equal(new Vector3Value(14, 2, 22), positions[5]);
    }

    [Fact]
    public void Scatter_is_repeatable_for_the_same_seed()
    {
        var request = new RepeatedPlacementRequest(
            new Vector3Value(0, 0, 0), 12, 1.5f, PlacementDistribution.DeterministicScatter, Seed: 42);

        Assert.Equal(RepeatedPlacementPlanner.Plan(request), RepeatedPlacementPlanner.Plan(request));
        Assert.NotEqual(
            RepeatedPlacementPlanner.Plan(request),
            RepeatedPlacementPlanner.Plan(request with { Seed = 43 }));
    }
}
