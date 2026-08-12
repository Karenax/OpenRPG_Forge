using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class WaypointAuthoringPersistenceTests
{
    [Fact]
    public void R064_project_and_area_round_trip_preserve_waypoints_markers_edges_and_traversal_exactly()
    {
        var areaId = AreaId.From(Guid.Parse("a0644000-0000-4000-8000-000000000001"));
        var waypoints = Enumerable.Range(0, 3).Select(index => new WaypointProfile(
            Guid.Parse($"a0644000-0000-4000-8000-00000000001{index + 1}"), 1, "test:r064",
            $"Point {index}", new Vector3Value(index * 2, 0, 0),
            new("flag", "#ff8844", "gameplay", true, 28),
            index == 0 ? new MapMarker("Braise révélée", "quest", -1, "quest", 2,
                MarkerVisibilityPolicy.HiddenUntilRevealed, MarkerVisibilityScope.Party, true, false,
                "quest.crypt.found", "pulse", true) : null, RequiredAtRuntime: true)).ToArray();
        var points = waypoints.Select((item, index) => new PatrolPoint(item.Id, item.Position,
            index, index * 45, 2, null, null, index + 1)).ToArray();
        var route = new PatrolRoute(Guid.Parse("a0644000-0000-4000-8000-000000000020"), 1,
            "test:r064", PatrolTraversalPolicy.PingPong, points, requiredAtRuntime: true);
        var profile = new WaypointAuthoringProfile(
            Guid.Parse("a0644000-0000-4000-8000-000000000030"), 1, "test:r064", areaId,
            true, waypoints, [route]);
        var source = new AreaDefinition(areaId, "crypt", "Crypte", []);
        var area = source.WithNavigation(source.Navigation.WithWaypoints(profile));
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Waypoint round-trip", [], [area]);

        var projectJson = ProjectJsonSerializer.Serialize(project);
        var areaJson = AreaJsonSerializer.Serialize(area);
        var restoredProject = ProjectJsonSerializer.Deserialize(projectJson);
        var restoredArea = AreaJsonSerializer.Deserialize(areaJson);
        var restored = restoredProject.Areas.Single().Navigation.Waypoints;

        Assert.Equal(projectJson, ProjectJsonSerializer.Serialize(restoredProject));
        Assert.Equal(areaJson, AreaJsonSerializer.Serialize(restoredArea));
        Assert.Equal(3, restored.Waypoints.Count);
        Assert.Equal(MarkerVisibilityPolicy.HiddenUntilRevealed,
            restored.Waypoints.Single(item => item.Marker is not null).Marker!.VisibilityPolicy);
        Assert.Equal(PatrolTraversalPolicy.PingPong, restored.PatrolRoutes.Single().Traversal);
        Assert.Equal(2, restored.PatrolRoutes.Single().Edges.Count);
    }

    [Fact]
    public void R065_round_trip_preserves_arrival_locators_collections_and_route_visualization_exactly()
    {
        var areaId = AreaId.From(Guid.Parse("a0654000-0000-4000-8000-000000000001"));
        var waypoint = new WaypointProfile(Guid.Parse("a0654000-0000-4000-8000-000000000011"),
            1, "test:r065", "Entrée", Vector3Value.Zero,
            new("flag", "#ff8844", "gameplay", true, 28), null, RequiredAtRuntime: true,
            Arrival: new(ArrivalFacingMode.Target, 0, "gate", "inspect", 1, "open",
                "waypoint.arrived"));
        var second = waypoint with
        {
            Id = Guid.Parse("a0654000-0000-4000-8000-000000000012"),
            Name = "Porte",
            Position = new Vector3Value(4, 0, 0),
            Arrival = null,
        };
        var route = new PatrolRoute(Guid.Parse("a0654000-0000-4000-8000-000000000020"), 1,
            "test:r065", PatrolTraversalPolicy.PingPong,
            [new(waypoint.Id, waypoint.Position, 0, 0, 2, "inspect", null),
                new(second.Id, second.Position, 1, 180, 2, null, null)]);
        var locator = new GenericLocator(Guid.Parse("a0654000-0000-4000-8000-000000000030"),
            1, "test:r065", "Cloche", new(2, 0, 1), LocatorPurpose.Audio,
            ["crypt", "ritual"], null, "audio:bell",
            ExplicitOverrides: new Dictionary<string, string> { ["bus"] = "Ambience" },
            RequiredAtRuntime: true);
        var collection = new AnchorCollection(Guid.Parse("a0654000-0000-4000-8000-000000000040"),
            1, "test:r065", "crypt", [locator.Id], "Crypte", LocatorPurpose.Audio,
            ["ritual"], LocatorSelectionMode.RandomDeterministic,
            Vector3Value.Zero, 2026, true);
        var visualization = new RouteVisualizationProfile(
            Guid.Parse("a0654000-0000-4000-8000-000000000050"), 1, "test:r065",
            true, true, true, true, true, true, true, 1.25, true);
        var profile = new WaypointAuthoringProfile(
            Guid.Parse("a0654000-0000-4000-8000-000000000060"), 1, "test:r065", areaId,
            true, [waypoint, second], [route], [locator], [collection], visualization);
        var source = new AreaDefinition(areaId, "crypt", "Crypte", []);
        var area = source.WithNavigation(source.Navigation.WithWaypoints(profile));
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Locator round-trip", [], [area]);

        var json = ProjectJsonSerializer.Serialize(project);
        var restoredProject = ProjectJsonSerializer.Deserialize(json);
        var restored = restoredProject.Areas.Single().Navigation.Waypoints;

        Assert.Equal(json, ProjectJsonSerializer.Serialize(restoredProject));
        Assert.Equal("waypoint.arrived", restored.Waypoints.Single(item => item.Arrival is not null)
            .Arrival?.Callback);
        Assert.Equal("audio:bell", restored.Locators.Single().AssetId);
        Assert.Equal("Ambience", restored.Locators.Single().ExplicitOverrides?["bus"]);
        Assert.Equal(LocatorSelectionMode.RandomDeterministic, restored.Collections.Single().Selection);
        Assert.Equal(1.25, restored.RouteVisualization.CostPerMeter);
    }
}
