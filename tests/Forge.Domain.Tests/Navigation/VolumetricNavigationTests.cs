using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Navigation;

public sealed class VolumetricNavigationTests
{
    [Fact]
    public void BuildsDeterministicGridAndRoutesAroundExpandedObstacle()
    {
        var obstacle = new NavigationVolumeDefinition(Guid.NewGuid(), "Tower", new Vector3Value(0, 2, 0),
            new Vector3Value(2, 4, 2), 1, "test");
        var cost = new NavigationVolumeDefinition(Guid.NewGuid(), "Headwind", new Vector3Value(3, 2, 0),
            new Vector3Value(2, 4, 4), 4, "test");
        var profile = new VolumetricNavigationProfile(Guid.NewGuid(), 1, true, VolumetricNavigationMode.Flight,
            1, new Vector3Value(0, 2, 0), new Vector3Value(12, 4, 12), .5f, [obstacle], [cost], true, "test");

        var graph = VolumetricNavigationGraphBuilder.Build(profile);
        var path = VolumetricNavigationPathfinder.FindPath(graph, new Vector3Value(-5.5f, .5f, 0),
            new Vector3Value(5.5f, .5f, 0));

        Assert.Equal(12, graph.WidthCells);
        Assert.Equal(4, graph.HeightCells);
        Assert.Equal(12, graph.DepthCells);
        Assert.NotEmpty(graph.Blocked);
        Assert.NotEmpty(graph.Costs);
        Assert.True(path.Succeeded);
        Assert.True(path.Points.Count > 12);
        Assert.DoesNotContain(path.Points, point => MathF.Abs(point.X) <= 1.5f && MathF.Abs(point.Z) <= 1.5f);
    }

    [Fact]
    public void HonorsGridAndSearchBudgets()
    {
        var profile = new VolumetricNavigationProfile(Guid.NewGuid(), 1, true, VolumetricNavigationMode.Space,
            1, Vector3Value.Zero, new Vector3Value(10, 10, 10), 0, [], [], false, "test");

        Assert.Throws<InvalidOperationException>(() => VolumetricNavigationGraphBuilder.Build(profile, 999));

        var graph = VolumetricNavigationGraphBuilder.Build(profile);
        var result = VolumetricNavigationPathfinder.FindPath(graph, new Vector3Value(-4.5f, -4.5f, -4.5f),
            new Vector3Value(4.5f, 4.5f, 4.5f), 2);
        Assert.False(result.Succeeded);
        Assert.Equal(2, result.VisitedCells);
    }
}
