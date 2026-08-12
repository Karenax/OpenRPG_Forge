using Forge.Application.Navigation;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Navigation;

public sealed class WaypointLocatorRuntimeServiceTests
{
    [Fact]
    public void R065_collections_support_stable_nearest_and_random_deterministic_resolution()
    {
        var profile = Profile();
        var stable = profile.Collections[0];
        var nearest = stable with { Selection = LocatorSelectionMode.Nearest, Origin = Vector3Value.Zero };
        var random = stable with { Selection = LocatorSelectionMode.RandomDeterministic, Seed = int.MinValue };

        var stableResult = WaypointRouteService.ResolveCollection(stable, profile.Locators);
        var nearestResult = WaypointRouteService.ResolveCollection(nearest, profile.Locators);
        var randomResult = WaypointRouteService.ResolveCollection(random, profile.Locators);

        Assert.Equal(["Brasero", "Cloche"], stableResult.Select(item => item.Name));
        Assert.Equal("Brasero", Assert.Single(nearestResult).Name);
        Assert.Single(randomResult);
    }

    [Fact]
    public void R065_arrival_executes_only_with_authority_and_exposes_every_action()
    {
        var profile = Profile();
        var waypoint = profile.Waypoints[0];

        var accepted = WaypointRuntimeService.Arrive(profile, waypoint.Id);
        var denied = WaypointRuntimeService.Arrive(profile, waypoint.Id, authorityGranted: false);

        Assert.Equal(ArrivalFacingMode.Target, accepted.Arrival?.Facing);
        Assert.Equal("inspect", accepted.Arrival?.Animation);
        Assert.Equal("open", accepted.Arrival?.Interaction);
        Assert.Equal("waypoint.arrived", accepted.Arrival?.Callback);
        Assert.Contains(denied.Diagnostics, item => item.Code == WaypointRuntimeService.AuthorityDeniedCode);
        Assert.False(denied.Arrival?.AuthorityGranted);
    }

    [Fact]
    public void R065_route_diagnostics_expose_reverse_cost_action_broken_projection_and_timing()
    {
        var profile = Profile();
        var route = profile.PatrolRoutes.Single();
        var known = profile.Waypoints.Take(2).Select(item => item.Id).ToHashSet();
        var projected = new HashSet<Guid> { route.Points[1].Id };

        var diagnostics = WaypointRouteService.AnalyzeRoute(route, projected, known, 1.25);

        Assert.Equal(4, diagnostics.Count);
        Assert.Contains(diagnostics, item => item.Direction == -1);
        Assert.Contains(diagnostics, item => item.Action == "inspect");
        Assert.Contains(diagnostics, item => item.BrokenLink);
        Assert.Contains(diagnostics, item => item.NavProjected);
        Assert.All(diagnostics, item => Assert.True(item.Cost > 0 && item.TravelSeconds > 0));
    }

    [Fact]
    public void R065_script_handles_and_required_runtime_diagnostics_are_bounded()
    {
        var profile = Profile();
        var area = new AreaDefinition(profile.AreaId, "crypt", "Crypte", []);
        area = area.WithNavigation(area.Navigation.WithWaypoints(profile));
        var project = new ProjectDefinition(ProjectId.From(
            Guid.Parse("a0652000-0000-4000-8000-000000000099")), "Test", [], [area]);
        var script = new WaypointAuthoringScriptService(project, area.Id);
        var invalidCollection = profile.Collections[0] with
        {
            LocatorIds = [],
            Query = "missing",
            RequiredTags = ["missing"],
            RequiredAtRuntime = true,
        };
        var invalid = new WaypointAuthoringProfile(profile.Id, 1, "test:r065", area.Id, true,
            profile.Waypoints, profile.PatrolRoutes, profile.Locators, [invalidCollection],
            profile.RouteVisualization);

        Assert.Equal(2, script.QueryLocators(script.Handle).Count);
        Assert.Single(script.QueryCollections(script.Handle));
        Assert.Equal(profile.Locators[0].Id,
            script.ResolveLocator(script.Handle, profile.Locators[0].Id).LocatorId);
        Assert.Contains(WaypointRuntimeService.ValidateRuntime(invalid),
            item => item.Code == WaypointRuntimeService.RequiredMissingCode);
    }

    private static WaypointAuthoringProfile Profile()
    {
        var areaId = AreaId.From(Guid.Parse("a0652000-0000-4000-8000-000000000001"));
        var waypoints = Enumerable.Range(0, 3).Select(index => new WaypointProfile(
            Guid.Parse($"a0652000-0000-4000-8000-{index + 11:000000000000}"), 1, "test:r065",
            $"Point {index}", new Vector3Value(index * 3, 0, index),
            new("flag", "#ff8844", "patrol", true, 28), null, RequiredAtRuntime: true,
            Arrival: index == 0 ? new(ArrivalFacingMode.Target, 0, "gate", "inspect", 1,
                "open", "waypoint.arrived") : null)).ToArray();
        var route = new PatrolRoute(Guid.Parse("a0652000-0000-4000-8000-000000000020"), 1,
            "test:r065", PatrolTraversalPolicy.PingPong,
            waypoints.Select((item, index) => new PatrolPoint(item.Id, item.Position, index, 0, 2,
                index == 1 ? "inspect" : null, null, index + 1)).ToArray());
        var locators = new[]
        {
            new GenericLocator(Guid.Parse("a0652000-0000-4000-8000-000000000031"), 1,
                "test:r065", "Brasero", new(1, 0, 0), LocatorPurpose.Vfx,
                ["crypt", "fire"], null, "vfx:ember"),
            new GenericLocator(Guid.Parse("a0652000-0000-4000-8000-000000000032"), 1,
                "test:r065", "Cloche", new(5, 0, 0), LocatorPurpose.Audio,
                ["crypt", "ritual"], null, "audio:bell"),
        };
        var collection = new AnchorCollection(Guid.Parse("a0652000-0000-4000-8000-000000000040"),
            1, "test:r065", "crypt", locators.Select(item => item.Id).ToArray(), "Crypte",
            RequiredTags: ["crypt"], Seed: 2026, RequiredAtRuntime: true);
        var visualization = new RouteVisualizationProfile(
            Guid.Parse("a0652000-0000-4000-8000-000000000050"), 1, "test:r065",
            true, true, true, true, true, true, true, 1.25, true);
        return new(Guid.Parse("a0652000-0000-4000-8000-000000000060"), 1, "test:r065", areaId,
            true, waypoints, [route], locators, [collection], visualization);
    }
}
