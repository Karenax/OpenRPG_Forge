using Forge.Application.Areas;
using Forge.Application.Gameplay;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Gameplay;

public sealed class RuntimeMapExplorationTests
{
    [Fact]
    public void Multifloor_exploration_filters_projection_and_reveals_hidden_pin_once()
    {
        var fixture = CreateFixture();
        var dirty = new AreaMapExplorationState(
            AreaMapExplorationScope.Party,
            "party:test",
            new HashSet<string>(StringComparer.Ordinal)
            {
                "v0:0:0:0",
                "not-a-cell",
                "v1:0:3:0",
            },
            new HashSet<Guid>(),
            ActiveFloor: 0);

        var dirtyProjection = RuntimeModulePresentationProjector.Project(
            fixture.Project,
            fixture.Area.Id,
            new RuntimeModulePresentationContext(Exploration: dirty));

        Assert.Equal(18, dirtyProjection.Map.TotalCells);
        Assert.Equal(1, dirtyProjection.Map.RevealedCells);
        Assert.Contains(dirtyProjection.Map.ExplorationDiagnostics,
            item => item.Code == AreaMapExplorationGrid.LegacyCellKeyMigratedCode);
        Assert.Contains(dirtyProjection.Map.ExplorationDiagnostics,
            item => item.Code == AreaMapExplorationGrid.CellKeyInvalidCode);
        Assert.Contains(dirtyProjection.Map.ExplorationDiagnostics,
            item => item.Code == AreaMapExplorationGrid.CellOutOfScopeCode);

        var ground = AreaExperienceAuthoringScriptService.Reveal(
            fixture.Area, dirty, activeFloor: 0, x: 0, z: 0);
        var groundAgain = AreaExperienceAuthoringScriptService.Reveal(
            fixture.Area, ground, activeFloor: 0, x: 0, z: 0);
        var groundProjection = RuntimeModulePresentationProjector.Project(
            fixture.Project,
            fixture.Area.Id,
            new RuntimeModulePresentationContext(Exploration: groundAgain));

        Assert.Single(groundAgain.RevealedCells);
        Assert.Empty(groundAgain.RevealedPins);
        Assert.Equal(0, groundProjection.Map.ActiveFloor);
        Assert.Equal(2, groundProjection.Map.FloorCount);
        Assert.All(groundProjection.Map.Markers, marker => Assert.True(marker.Floor is -1 or 0));
        Assert.Contains(groundProjection.Map.Markers, marker => marker.Id == fixture.VisiblePinId.ToString("D"));
        Assert.DoesNotContain(groundProjection.Map.Markers, marker => marker.Id == fixture.HiddenPinId.ToString("D"));
        Assert.Single(groundProjection.Map.PatrolRoutes);
        Assert.Equal(0, groundProjection.Map.PatrolRoutes[0].Floor);

        var upper = AreaExperienceAuthoringScriptService.Reveal(
            fixture.Area, groundAgain, activeFloor: 1, x: 0, z: 0);
        var upperAgain = AreaExperienceAuthoringScriptService.Reveal(
            fixture.Area, upper, activeFloor: 1, x: 0, z: 0);
        var upperProjection = RuntimeModulePresentationProjector.Project(
            fixture.Project,
            fixture.Area.Id,
            new RuntimeModulePresentationContext(Exploration: upperAgain));

        Assert.Equal(2, upperAgain.RevealedCells.Count);
        Assert.Equal(upper.RevealedCells, upperAgain.RevealedCells);
        Assert.Equal([fixture.HiddenPinId], upperAgain.RevealedPins);
        Assert.Equal(upper.RevealedPins, upperAgain.RevealedPins);
        Assert.Equal(18, upperProjection.Map.TotalCells);
        Assert.Equal(2, upperProjection.Map.RevealedCells);
        Assert.Equal(2d * 100d / 18d, upperProjection.Map.ExplorationPercent!.Value, 8);
        Assert.InRange(upperProjection.Map.ExplorationPercent.Value, 0, 100);
        Assert.Equal(1, upperProjection.Map.ActiveFloor);
        Assert.All(upperProjection.Map.Markers, marker => Assert.True(marker.Floor is -1 or 1));
        Assert.Contains(upperProjection.Map.Markers, marker => marker.Id == fixture.HiddenPinId.ToString("D"));
        Assert.DoesNotContain(upperProjection.Map.Markers, marker => marker.Id == fixture.VisiblePinId.ToString("D"));
        Assert.Single(upperProjection.Map.PatrolRoutes);
        Assert.Equal(1, upperProjection.Map.PatrolRoutes[0].Floor);
    }

    [Fact]
    public void Out_of_range_saved_floor_falls_back_with_a_stable_diagnostic()
    {
        var fixture = CreateFixture();
        var invalid = new AreaMapExplorationState(
            AreaMapExplorationScope.Party,
            "party:test",
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<Guid>(),
            ActiveFloor: 2);
        RuntimeModulePresentation? presentation = null;

        var exception = Record.Exception(() => presentation = RuntimeModulePresentationProjector.Project(
            fixture.Project,
            fixture.Area.Id,
            new RuntimeModulePresentationContext(Exploration: invalid)));

        Assert.Null(exception);
        Assert.Equal(0, presentation!.Map.ActiveFloor);
        Assert.Contains(presentation.Map.ExplorationDiagnostics,
            item => item.Code == AreaExperienceAuthoringScriptService.ActiveFloorOutOfScopeCode);
    }

    [Fact]
    public void Disabled_map_still_bounds_a_restored_floor_before_projection()
    {
        var fixture = CreateFixture();
        var source = fixture.Area.Authoring;
        var map = source.Map;
        var disabledMap = new AreaMapProfile(
            false,
            map.FogOfWar,
            map.Projection,
            map.FloorCount,
            map.Pins,
            map.Id,
            map.SchemaVersion,
            map.Provenance,
            map.Generate2D,
            map.Generate3D,
            map.ExplorationScope,
            map.ExplorationCellSize,
            map.RevealApi);
        var disabledArea = fixture.Area.WithAuthoring(new AreaAuthoringProfile(
            source.SchemaVersion,
            source.UserEvents,
            disabledMap,
            source.Layers,
            source.Experience,
            source.RequireLayerRuntimeMetadata));
        var invalid = new AreaMapExplorationState(
            disabledMap.ExplorationScope,
            "party:test",
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<Guid>(),
            ActiveFloor: disabledMap.FloorCount);

        var normalized = AreaExperienceAuthoringScriptService.Normalize(disabledArea, invalid);

        Assert.Equal(0, normalized.State.ActiveFloor);
        Assert.Contains(normalized.Diagnostics,
            item => item.Code == AreaExperienceAuthoringScriptService.ActiveFloorOutOfScopeCode);
    }

    private static Fixture CreateFixture()
    {
        var areaId = AreaId.From(Guid.Parse("a0290000-0000-4000-8000-000000000001"));
        var visiblePinId = Guid.Parse("a0290000-0000-4000-8000-000000000002");
        var hiddenPinId = Guid.Parse("a0290000-0000-4000-8000-000000000003");
        var map = new AreaMapProfile(
            true,
            true,
            "orthographic",
            2,
            [
                new AreaMapPin(visiblePinId, "Ground pin", 0, 0, 0, true),
                new AreaMapPin(hiddenPinId, "Hidden upper pin", 0, 0, 1, false),
            ],
            Guid.Parse("a0290000-0000-4000-8000-000000000004"),
            provenance: "test:multifloor",
            explorationCellSize: 4);
        var authoring = new AreaAuthoringProfile(
            AreaAuthoringProfile.CurrentSchemaVersion,
            [],
            map,
            [],
            AreaExperienceAuthoringProfile.CreateDefault(areaId));
        var area = new AreaDefinition(
            areaId,
            "multifloor",
            "Multi-floor",
            [],
            dimensions: new AreaDimensions(10, 10),
            authoring: authoring);

        var groundA = Waypoint(areaId, 10, -1, 0, 0);
        var groundB = Waypoint(areaId, 11, 1, 0, 0);
        var upperA = Waypoint(areaId, 20, -1, 0, 1);
        var upperB = Waypoint(areaId, 21, 1, 0, 1);
        var routes = new[]
        {
            Route(30, groundA, groundB),
            Route(40, upperA, upperB),
        };
        var waypointProfile = new WaypointAuthoringProfile(
            Guid.Parse("a0290000-0000-4000-8000-000000000005"),
            WaypointAuthoringProfile.CurrentSchemaVersion,
            "test:multifloor",
            areaId,
            false,
            [groundA, groundB, upperA, upperB],
            routes);
        area = area.WithNavigation(area.Navigation.WithWaypoints(waypointProfile));
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("a0290000-0000-4000-8000-000000000006")),
            "Multi-floor fixture",
            [],
            [area]);
        return new(project, area, visiblePinId, hiddenPinId);
    }

    private static WaypointProfile Waypoint(AreaId areaId, int suffix, float x, float z, int floor)
    {
        var id = Guid.Parse($"a0290000-0000-4000-8000-{suffix:D12}");
        return new WaypointProfile(
            id,
            WaypointAuthoringProfile.CurrentSchemaVersion,
            "test:multifloor",
            $"Floor {floor} waypoint {suffix}",
            new Vector3Value(x, floor * 4, z),
            new WaypointEditorAppearance("dot", "#ffffff", "Map", true, 24),
            new Forge.Domain.Navigation.MapMarker(
                $"Floor {floor} waypoint {suffix}",
                "dot",
                floor,
                "exploration",
                1,
                MarkerVisibilityPolicy.Always,
                MarkerVisibilityScope.Party,
                false,
                true,
                string.Empty,
                string.Empty,
                true));
    }

    private static PatrolRoute Route(int suffix, WaypointProfile first, WaypointProfile second) => new(
        Guid.Parse($"a0290000-0000-4000-8001-{suffix:D12}"),
        WaypointAuthoringProfile.CurrentSchemaVersion,
        "test:multifloor",
        PatrolTraversalPolicy.Loop,
        [
            new PatrolPoint(first.Id, first.Position, 0, 0, 1, null, null),
            new PatrolPoint(second.Id, second.Position, 0, 0, 1, null, null),
        ]);

    private sealed record Fixture(
        ProjectDefinition Project,
        AreaDefinition Area,
        Guid VisiblePinId,
        Guid HiddenPinId);
}
