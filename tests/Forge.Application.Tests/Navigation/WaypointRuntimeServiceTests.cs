using Forge.Application.Navigation;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Navigation;

public sealed class WaypointRuntimeServiceTests
{
    [Fact]
    public void R064_reveal_is_scoped_authoritative_persistent_and_accessible()
    {
        var profile = Profile();

        var denied = WaypointRuntimeService.Reveal(profile, WaypointRuntimeState.Empty,
            MarkerVisibilityScope.Party, "quest.crypt.found", authorityGranted: false);
        var revealed = WaypointRuntimeService.Reveal(profile, WaypointRuntimeState.Empty,
            MarkerVisibilityScope.Party, "quest.crypt.found");
        var visible = WaypointRuntimeService.VisibleMarkers(profile, revealed.State,
            MarkerVisibilityScope.Party);

        Assert.Contains(denied.Diagnostics, item => item.Code == WaypointRuntimeService.AuthorityDeniedCode);
        Assert.Single(revealed.RevealedWaypointIds);
        var presentation = Assert.Single(visible);
        Assert.Contains("catégorie quest", presentation.AccessibleText, StringComparison.Ordinal);
        Assert.True(presentation.Tracked);
    }

    [Fact]
    public void R064_patrol_advances_ping_pong_and_scripted_routes_with_safe_diagnostics()
    {
        var profile = Profile();
        var route = profile.PatrolRoutes.Single();
        var first = WaypointRuntimeService.Advance(profile, WaypointRuntimeState.Empty, route.Id);
        var second = WaypointRuntimeService.Advance(profile, first.State, route.Id);
        var third = WaypointRuntimeService.Advance(profile, second.State, route.Id);
        var scriptedRoute = new PatrolRoute(Guid.NewGuid(), 1, "test:r064",
            PatrolTraversalPolicy.Scripted, route.Points, scriptHandler: "patrol.choose");
        var scriptedProfile = new WaypointAuthoringProfile(profile.Id, 1, profile.Provenance,
            profile.AreaId, true, profile.Waypoints, [scriptedRoute]);
        var invalid = WaypointRuntimeService.Advance(scriptedProfile, WaypointRuntimeState.Empty,
            scriptedRoute.Id);
        var scripted = WaypointRuntimeService.Advance(scriptedProfile, WaypointRuntimeState.Empty,
            scriptedRoute.Id, scriptedIndex: 2);

        Assert.Equal(1, first.Step!.Cursor.Index);
        Assert.Equal(2, second.Step!.Cursor.Index);
        Assert.Equal(1, third.Step!.Cursor.Index);
        Assert.Contains(invalid.Diagnostics,
            item => item.Code == WaypointRuntimeService.ScriptIndexInvalidCode);
        Assert.Equal(2, scripted.Step!.Cursor.Index);
    }

    [Fact]
    public void R064_forgescript_service_uses_generation_checked_handles()
    {
        var profile = Profile();
        var area = CreateArea(profile);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Waypoints", [], [area]);
        var service = new WaypointAuthoringScriptService(project, area.Id);

        var waypoints = service.Query(service.Handle, "Braise");
        var routes = service.QueryRoutes(service.Handle);

        Assert.Single(waypoints);
        Assert.Single(routes);
        Assert.Equal(waypoints[0].Id, service.ResolveWaypoint(service.Handle, waypoints[0].Id).WaypointId);
        Assert.Equal(routes[0].Id, service.ResolveRoute(service.Handle, routes[0].Id).RouteId);
        Assert.Throws<InvalidOperationException>(() => service.Query(service.Handle with { Generation = 9 }));
    }

    private static WaypointAuthoringProfile Profile()
    {
        var areaId = AreaId.From(Guid.Parse("a0642000-0000-4000-8000-000000000001"));
        var waypoints = Enumerable.Range(0, 3).Select(index => new WaypointProfile(
            Guid.Parse($"a0642000-0000-4000-8000-00000000001{index + 1}"), 1, "test:r064",
            index == 0 ? "Braise" : $"Point {index}", new Vector3Value(index * 2, 0, 0),
            new("flag", "#ff8844", "gameplay", true, 28),
            index == 0 ? new MapMarker("Braise révélée", "quest", -1, "quest", 2,
                MarkerVisibilityPolicy.HiddenUntilRevealed, MarkerVisibilityScope.Party, true, false,
                "quest.crypt.found", "pulse", true) : null, RequiredAtRuntime: true)).ToArray();
        var points = waypoints.Select((item, index) => new PatrolPoint(item.Id, item.Position,
            index, index * 45, 2, index == 1 ? "inspect" : null, null, index + 1)).ToArray();
        var route = new PatrolRoute(Guid.Parse("a0642000-0000-4000-8000-000000000020"), 1,
            "test:r064", PatrolTraversalPolicy.PingPong, points, requiredAtRuntime: true);
        return new(Guid.Parse("a0642000-0000-4000-8000-000000000030"), 1, "test:r064", areaId,
            true, waypoints, [route]);
    }

    private static AreaDefinition CreateArea(WaypointAuthoringProfile profile)
    {
        var area = new AreaDefinition(profile.AreaId, "crypt", "Crypte", []);
        return area.WithNavigation(area.Navigation.WithWaypoints(profile));
    }
}
