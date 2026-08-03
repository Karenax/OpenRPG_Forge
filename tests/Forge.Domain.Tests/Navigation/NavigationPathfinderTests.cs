using Forge.Domain.Areas;
using Forge.Domain.Navigation;

namespace Forge.Domain.Tests.Navigation;

public sealed class NavigationPathfinderTests
{
    [Fact]
    public void FindPath_IsDeterministicAndAvoidsDynamicObstacles()
    {
        var surface = Surface(5, 5);
        var query = new NavigationPathQuery(
            new NavigationCell(0, 2),
            new NavigationCell(4, 2),
            dynamicObstacles: new HashSet<NavigationCell> { new(2, 2) });

        var first = NavigationPathfinder.FindPath(surface, query);
        var second = NavigationPathfinder.FindPath(surface, query);

        Assert.True(first.Succeeded);
        Assert.Equal(first.Cells, second.Cells);
        Assert.Equal(first.TotalCost, second.TotalCost);
        Assert.Equal(first.VisitedCellCount, second.VisitedCellCount);
        Assert.DoesNotContain(new NavigationCell(2, 2), first.Cells);
        Assert.Equal(7, first.Cells.Count);
    }

    [Fact]
    public void FindPath_UsesCostsAndRecoversBlockedEndpoints()
    {
        var surface = Surface(5, 3);
        var query = new NavigationPathQuery(
            new NavigationCell(-1, 1),
            new NavigationCell(4, 1),
            costMultipliers: new Dictionary<NavigationCell, float>
            {
                [new(2, 1)] = 20,
            },
            recoveryRadius: 2);

        var result = NavigationPathfinder.FindPath(surface, query);

        Assert.True(result.Succeeded);
        Assert.True(result.RecoveredStart);
        Assert.Equal(new NavigationCell(0, 1), result.ResolvedStart);
        Assert.DoesNotContain(new NavigationCell(2, 1), result.Cells);
    }

    [Fact]
    public void FindPath_ReportsBudgetAndUnreachableFailures()
    {
        var surface = Surface(8, 8);
        var budget = NavigationPathfinder.FindPath(surface,
            new NavigationPathQuery(new(0, 0), new(7, 7), maximumVisitedCells: 1));
        var wall = Enumerable.Range(0, 8).Select(z => new NavigationCell(3, z)).ToHashSet();
        var unreachable = NavigationPathfinder.FindPath(surface,
            new NavigationPathQuery(new(0, 0), new(7, 7), dynamicObstacles: wall));

        Assert.Equal(NavigationPathFailure.SearchBudgetExceeded, budget.Failure);
        Assert.Equal(NavigationPathFailure.Unreachable, unreachable.Failure);
    }

    [Fact]
    public void DynamicUpdatePlanner_InvalidatesOnlyChangedObstacleAndCostCells()
    {
        var surface = Surface(20, 20);
        var previous = new HashSet<NavigationCell> { new(2, 2), new(4, 4) };
        var next = new HashSet<NavigationCell> { new(4, 4), new(6, 6) };

        var plan = NavigationDynamicUpdatePlanner.Plan(
            surface,
            previous,
            next,
            new Dictionary<NavigationCell, float> { [new(8, 8)] = 2 },
            new Dictionary<NavigationCell, float> { [new(8, 8)] = 3 });

        Assert.Equal([new NavigationCell(2, 2), new(6, 6), new(8, 8)], plan.ChangedCells);
        Assert.Equal(397, plan.ReusedCellCount);
        Assert.False(plan.RequiresFullSearchReset);
    }

    private static NavigationSurface Surface(int width, int depth) => new(
        NavigationSurfaceId.From(Guid.Parse("65b4577f-626d-4e57-a352-dd0547038b94")),
        AreaId.From(Guid.Parse("99488dd2-6323-4796-a903-d68439fc4fd1")),
        NavigationAgentProfileId.From(Guid.Parse("fc229488-9a2c-463a-8a2c-e64eae9ff53f")),
        NavigationDoorState.Current,
        width,
        depth,
        1,
        0,
        0,
        Enumerable.Range(0, depth).SelectMany(z => Enumerable.Range(0, width).Select(x => new NavigationCell(x, z))).ToArray(),
        [],
        "pathfinder-test",
        "test");
}
