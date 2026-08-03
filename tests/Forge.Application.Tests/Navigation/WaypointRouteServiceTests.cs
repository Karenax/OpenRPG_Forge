using Forge.Application.Navigation;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Navigation;

public sealed class WaypointRouteServiceTests
{
    [Fact]
    public void Marker_IsValidatedAndRevealedOnlyInMatchingScope()
    {
        var marker = new MapMarker("Crypte", "skull", -1, "quest", 2, MarkerVisibilityPolicy.HiddenUntilRevealed, MarkerVisibilityScope.Party, true, false, "quest.crypt.found", "pulse", true);
        var waypoint = new WaypointProfile(Guid.NewGuid(), 1, "author:test", "Entrée", Vector3Value.Zero, new("flag", "#ff8844", "gameplay", true, 24), marker);
        Assert.Same(waypoint, WaypointRouteService.Validate(waypoint));
        Assert.False(WaypointRouteService.Reveal(marker, MarkerVisibilityScope.Player, marker.RevealEvent).Revealed);
        Assert.True(WaypointRouteService.Reveal(marker, MarkerVisibilityScope.Party, marker.RevealEvent).Revealed);
    }

    [Fact]
    public void PatrolRoute_SupportsLoopPingPongOnceRandomWeightedAndScripted()
    {
        var points = Enumerable.Range(0, 3).Select(index => new PatrolPoint(Guid.NewGuid(), new Vector3Value(index, 0, 0), index, index * 90, 2, "look", null, index + 1)).ToArray();
        PatrolRoute Route(PatrolTraversalPolicy policy) => new(Guid.NewGuid(), 1, "author:test", policy, points);
        Assert.Equal(0, WaypointRouteService.Next(Route(PatrolTraversalPolicy.Loop), new(2, 1, false)).Index);
        Assert.Equal(-1, WaypointRouteService.Next(Route(PatrolTraversalPolicy.PingPong), new(2, 1, false)).Direction);
        Assert.True(WaypointRouteService.Next(Route(PatrolTraversalPolicy.Once), new(2, 1, false)).Complete);
        Assert.Equal(2, WaypointRouteService.Next(Route(PatrolTraversalPolicy.Random), new(0, 1, false), 5).Index);
        Assert.InRange(WaypointRouteService.Next(Route(PatrolTraversalPolicy.Weighted), new(0, 1, false), 9000).Index, 0, 2);
        Assert.Equal(1, WaypointRouteService.Next(Route(PatrolTraversalPolicy.Scripted), new(1, 1, false)).Index);
    }
}
