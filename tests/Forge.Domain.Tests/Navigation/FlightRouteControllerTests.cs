using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Navigation;

public sealed class FlightRouteControllerTests
{
    [Fact]
    public void FollowsCurveAtNodeSpeedAndAnimatesBank()
    {
        var route = CreateRoute(FlightObstaclePolicy.Avoid);
        var initial = FlightRouteController.CreateInitial(Guid.NewGuid(), route);

        var step = FlightRouteController.Step(route, initial, .5f, 1, new FixedProbe(false));

        Assert.True(step.State.Position.X > initial.Position.X);
        Assert.True(step.State.Forward.X > 0);
        Assert.InRange(step.State.BankDegrees, -30, 0);
        Assert.Equal("waypoint.flight.following", step.DiagnosticCode);
    }

    [Fact]
    public void AppliesAvoidStopAndIgnoreObstaclePolicies()
    {
        var actor = Guid.NewGuid();
        var blocked = new FixedProbe(true);
        var avoidRoute = CreateRoute(FlightObstaclePolicy.Avoid);
        var avoid = FlightRouteController.Step(avoidRoute,
            FlightRouteController.CreateInitial(actor, avoidRoute), .25f, 1, blocked);
        var stopRoute = CreateRoute(FlightObstaclePolicy.Stop);
        var stopState = FlightRouteController.CreateInitial(actor, stopRoute);
        var stop = FlightRouteController.Step(stopRoute, stopState, .25f, 1, blocked);
        var ignoreRoute = CreateRoute(FlightObstaclePolicy.Ignore);
        var ignore = FlightRouteController.Step(ignoreRoute,
            FlightRouteController.CreateInitial(actor, ignoreRoute), .25f, 1, blocked);

        Assert.Equal("waypoint.flight.obstacle_avoided", avoid.DiagnosticCode);
        Assert.True(avoid.State.Position.Y > 5);
        Assert.Equal(stopState, stop.State);
        Assert.Equal("waypoint.flight.obstacle_stopped", stop.DiagnosticCode);
        Assert.Equal("waypoint.flight.obstacle_ignored", ignore.DiagnosticCode);
    }

    [Fact]
    public void CompletesOpenRouteAndLoopsClosedRoute()
    {
        var open = CreateRoute(FlightObstaclePolicy.Ignore, closed: false);
        var nearEnd = FlightRouteController.CreateInitial(Guid.NewGuid(), open) with { SegmentProgress = .95f };
        var completed = FlightRouteController.Step(open, nearEnd, .5f, 1, new FixedProbe(false));
        var closed = CreateRoute(FlightObstaclePolicy.Ignore, closed: true);
        var loop = FlightRouteController.Step(closed,
            FlightRouteController.CreateInitial(Guid.NewGuid(), closed) with { SegmentProgress = .95f },
            .5f, 1, new FixedProbe(false));

        Assert.True(completed.State.Completed);
        Assert.Equal("waypoint.flight.completed", completed.DiagnosticCode);
        Assert.False(loop.State.Completed);
        Assert.Equal(0, loop.State.SegmentIndex);
    }

    private static FlightRouteDefinition CreateRoute(FlightObstaclePolicy policy, bool closed = false) => new(
        Guid.NewGuid(), 1, "Test flight",
        [
            new(Guid.NewGuid(), "Start", new(0, 5, 0), 10, -30),
            new(Guid.NewGuid(), "End", new(10, 5, 0), 10, 30),
        ], FlightRouteCurve.CatmullRom, policy, closed, true, "test://flight-route");

    private sealed class FixedProbe(bool blocked) : IFlightObstacleProbe
    {
        public bool IsBlocked(Vector3Value position, float clearanceRadius) => blocked;
    }
}
