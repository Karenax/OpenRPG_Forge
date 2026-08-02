using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Navigation;

public sealed class NavigationAndGeometryTests
{
    [Fact]
    public void DefaultProfiles_AreStableAndSupportMultipleAgentSizesAndFlight()
    {
        var areaId = AreaId.From(Guid.Parse("71000000-0000-4000-8000-000000000001"));

        var first = AreaNavigationDefinition.CreateDefault(areaId);
        var second = AreaNavigationDefinition.CreateDefault(areaId);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.AgentProfiles.Select(profile => profile.Id), second.AgentProfiles.Select(profile => profile.Id));
        Assert.Equal(4, first.AgentProfiles.Count);
        Assert.Contains(first.AgentProfiles, profile => profile.Radius == 0.3f);
        Assert.Contains(first.AgentProfiles, profile => profile.Radius == 0.9f);
        Assert.Contains(first.AgentProfiles, profile => profile.CanFly);
    }

    [Fact]
    public void Bake_AccountsForAgentRadiusDoorStatesOffMeshFlightAndPartialChanges()
    {
        var setup = CreateSplitArea(includeLink: true);
        var small = setup.Area.Navigation.AgentProfiles.Single(profile => profile.Name == "Petit");
        var large = setup.Area.Navigation.AgentProfiles.Single(profile => profile.Name == "Grand");
        var flying = setup.Area.Navigation.AgentProfiles.Single(profile => profile.CanFly);

        var current = NavigationSurfaceBuilder.Build(setup.Project, setup.Area, small);
        var closed = NavigationSurfaceBuilder.Build(setup.Project, setup.Area, small, NavigationDoorState.AllClosed);
        var largeSurface = NavigationSurfaceBuilder.Build(setup.Project, setup.Area, large);
        var flyingSurface = NavigationSurfaceBuilder.Build(setup.Project, setup.Area, flying);

        Assert.True(current.WalkableCells.Count > largeSurface.WalkableCells.Count);
        Assert.True(current.WalkableCells.Count > closed.WalkableCells.Count);
        Assert.NotEmpty(current.OffMeshEdges);
        Assert.Contains(flyingSurface.OffMeshEdges, edge =>
            setup.Area.Navigation.OffMeshLinks.Single(link => link.Kind == NavigationLinkKind.Flight).Id == edge.LinkId);
        Assert.DoesNotContain(current.OffMeshEdges, edge =>
            setup.Area.Navigation.OffMeshLinks.Single(link => link.Kind == NavigationLinkKind.Flight).Id == edge.LinkId);

        var identical = NavigationBakePlanner.Plan(current, current);
        var partial = NavigationBakePlanner.Plan(current, closed);
        Assert.False(identical.RequiresFullBake);
        Assert.Empty(identical.ChangedCells);
        Assert.False(partial.RequiresFullBake);
        Assert.NotEmpty(partial.ChangedCells);
        Assert.True(partial.ReusedCellCount > 0);
    }

    [Fact]
    public void Reachability_UsesOffMeshLinksAndReportsBlockedObjectives()
    {
        var linked = CreateSplitArea(includeLink: true);
        var unlinked = CreateSplitArea(includeLink: false);
        var linkedAgent = linked.Area.Navigation.AgentProfiles.Single(profile => profile.Name == "Petit");
        var unlinkedAgent = unlinked.Area.Navigation.AgentProfiles.Single(profile => profile.Name == "Petit");

        var reachable = AreaNavigationValidator.Analyze(linked.Project, linked.Area, linkedAgent);
        var blocked = AreaNavigationValidator.Analyze(unlinked.Project, unlinked.Area, unlinkedAgent);

        Assert.Empty(reachable.UnreachableEntityIds);
        Assert.DoesNotContain(reachable.Diagnostics, diagnostic =>
            diagnostic.Code == AreaNavigationValidator.UnreachableEntityCode);
        Assert.Single(blocked.UnreachableEntityIds);
        Assert.Contains(blocked.Diagnostics, diagnostic =>
            diagnostic.Code == AreaNavigationValidator.UnreachableEntityCode);
    }

    [Fact]
    public void GeometryValidation_DetectsBoundsCollisionOverlapAndSocketMismatch()
    {
        var wallBlueprint = Blueprint(BlueprintKind.Placeable, "71000000-0000-4000-8000-000000000011");
        var portalBlueprint = Blueprint(BlueprintKind.Trigger, "71000000-0000-4000-8000-000000000012");
        var firstPortal = Entity(portalBlueprint.Id, "71000000-0000-4000-8000-000000000021", -3, 0, 1, 1, ["socket:round"]);
        var secondPortal = Entity(portalBlueprint.Id, "71000000-0000-4000-8000-000000000022", 3, 0, 1, 1, ["socket:square"]);
        var firstAreaId = AreaId.From(Guid.Parse("71000000-0000-4000-8000-000000000031"));
        var secondAreaId = AreaId.From(Guid.Parse("71000000-0000-4000-8000-000000000032"));
        var firstArea = new AreaDefinition(
            firstAreaId,
            "first",
            "First",
            [
                Entity(wallBlueprint.Id, "71000000-0000-4000-8000-000000000023", 4.8f, 0, 2, 2, ["collision"]),
                Entity(wallBlueprint.Id, "71000000-0000-4000-8000-000000000024", 4.2f, 0, 2, 2, ["collision"]),
                firstPortal,
            ],
            dimensions: new AreaDimensions(10, 10),
            portals:
            [
                new PortalLink(
                    PortalId.From(Guid.Parse("71000000-0000-4000-8000-000000000041")),
                    "Mismatched",
                    firstPortal.Id,
                    secondAreaId,
                    secondPortal.Id,
                    true,
                    false),
            ]);
        var secondArea = new AreaDefinition(secondAreaId, "second", "Second", [secondPortal]);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("71000000-0000-4000-8000-000000000051")),
            "Geometry",
            [wallBlueprint, portalBlueprint],
            [firstArea, secondArea]);

        var diagnostics = AreaGeometryValidator.Validate(project, firstArea);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == AreaGeometryValidator.OutOfBoundsCode);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == AreaGeometryValidator.CollisionOverlapCode);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == AreaGeometryValidator.SocketMismatchCode);
    }

    private static (ProjectDefinition Project, AreaDefinition Area) CreateSplitArea(bool includeLink)
    {
        var floor = Blueprint(BlueprintKind.Placeable, "71000000-0000-4000-8000-000000000101");
        var wall = Blueprint(BlueprintKind.Placeable, "71000000-0000-4000-8000-000000000102");
        var door = Blueprint(BlueprintKind.Door, "71000000-0000-4000-8000-000000000103");
        var spawn = Blueprint(BlueprintKind.PlayerSpawn, "71000000-0000-4000-8000-000000000104");
        var item = Blueprint(BlueprintKind.Item, "71000000-0000-4000-8000-000000000105");
        var areaId = AreaId.From(Guid.Parse("71000000-0000-4000-8000-000000000111"));
        var navigation = AreaNavigationDefinition.CreateDefault(areaId);
        if (includeLink)
        {
            navigation = new AreaNavigationDefinition(
                navigation.Id,
                navigation.CellSize,
                navigation.AgentProfiles,
                [
                    new OffMeshNavigationLink(
                        OffMeshNavigationLinkId.From(Guid.Parse("71000000-0000-4000-8000-000000000121")),
                        "Jump gap",
                        NavigationLinkKind.Jump,
                        new Vector3Value(-2, 0, 0),
                        new Vector3Value(2, 0, 0),
                        true,
                        1,
                        "test"),
                    new OffMeshNavigationLink(
                        OffMeshNavigationLinkId.From(Guid.Parse("71000000-0000-4000-8000-000000000122")),
                        "Fly gap",
                        NavigationLinkKind.Flight,
                        new Vector3Value(-2, 1, 2),
                        new Vector3Value(2, 1, 2),
                        true,
                        1,
                        "test"),
                ],
                "test");
        }

        var area = new AreaDefinition(
            areaId,
            "split",
            "Split",
            [
                Entity(floor.Id, "71000000-0000-4000-8000-000000000131", 0, 0, 10, 10, ["floor"]),
                Entity(wall.Id, "71000000-0000-4000-8000-000000000132", 0, 0, 1, 10, ["wall"]),
                Entity(door.Id, "71000000-0000-4000-8000-000000000133", -3, 3, 1, 1, ["door", "locked"]),
                Entity(spawn.Id, "71000000-0000-4000-8000-000000000134", -3, 0, 1, 1),
                Entity(item.Id, "71000000-0000-4000-8000-000000000135", 3, 0, 1, 1),
            ],
            dimensions: new AreaDimensions(10, 10),
            navigation: navigation);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("71000000-0000-4000-8000-000000000141")),
            "Navigation",
            [floor, wall, door, spawn, item],
            [area]);
        return (project, area);
    }

    private static BlueprintDefinition Blueprint(BlueprintKind kind, string id) => new(
        BlueprintId.From(Guid.Parse(id)),
        $"blueprint-{id[^3..]}",
        kind.ToString(),
        kind);

    private static EntityInstance Entity(
        BlueprintId blueprintId,
        string id,
        float x,
        float z,
        float scaleX,
        float scaleZ,
        IReadOnlyList<string>? tags = null) => new(
        EntityId.From(Guid.Parse(id)),
        blueprintId,
        new TransformValue(
            new Vector3Value(x, 0, z),
            QuaternionValue.Identity,
            new Vector3Value(scaleX, 1, scaleZ)),
        tags);
}
