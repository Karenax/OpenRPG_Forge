using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Navigation;

public sealed class WaypointAuthoringValidatorTests
{
    [Fact]
    public void R064_profile_validates_appearance_map_note_reveal_and_ordered_patrol_graph()
    {
        var area = CreateArea(Profile(required: true));

        var diagnostics = WaypointAuthoringValidator.Validate(area);

        Assert.Empty(diagnostics);
        var route = Assert.Single(area.Navigation.Waypoints.PatrolRoutes);
        Assert.Equal(2, route.Edges.Count);
        Assert.Equal([0, 1], route.Edges.Select(item => item.DisplayOrder));
    }

    [Fact]
    public void R064_validation_reports_required_identity_appearance_marker_anchor_and_route_limits()
    {
        var areaId = AreaId.From(Guid.Parse("a0641000-0000-4000-8000-000000000001"));
        var waypoint = Waypoint("a0641000-0000-4000-8000-000000000011", "Braise", 0) with
        {
            Appearance = new("", "orange", "", true, 4),
            Marker = new("", "", 80, "", -1, MarkerVisibilityPolicy.HiddenUntilRevealed,
                MarkerVisibilityScope.Party, false, false, "", "", true),
            AnchorInstanceId = Guid.NewGuid(),
        };
        var points = new[]
        {
            new PatrolPoint(Guid.NewGuid(), Vector3Value.Zero, -1, 720, 0, null, null),
            new PatrolPoint(Guid.NewGuid(), new(1, 0, 1), 0, 0, 1, null, null),
        };
        var route = new PatrolRoute(Guid.NewGuid(), 1, "test:r064", PatrolTraversalPolicy.Scripted,
            points, scriptHandler: null, requiredAtRuntime: true);
        var profile = new WaypointAuthoringProfile(Guid.Empty, 2, "test:r064", areaId, true,
            [waypoint], [route]);
        var area = CreateArea(profile);

        var diagnostics = WaypointAuthoringValidator.Validate(area);

        Assert.Contains(diagnostics, item => item.Code == WaypointAuthoringValidator.IdentityInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == WaypointAuthoringValidator.AppearanceInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == WaypointAuthoringValidator.MarkerInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == WaypointAuthoringValidator.AnchorMissingCode);
        Assert.Contains(diagnostics, item => item.Code == WaypointAuthoringValidator.RouteInvalidCode);
    }

    [Fact]
    public void R064_optional_empty_is_safe_and_required_empty_is_diagnostic()
    {
        var areaId = AreaId.From(Guid.Parse("a0641000-0000-4000-8000-000000000001"));
        var optional = CreateArea(WaypointAuthoringProfile.CreateDefault(areaId));
        var required = CreateArea(new(Guid.NewGuid(), 1, "test:r064", areaId, true, [], []));

        Assert.Empty(WaypointAuthoringValidator.Validate(optional));
        Assert.Contains(WaypointAuthoringValidator.Validate(required),
            item => item.Code == WaypointAuthoringValidator.RequiredContentCode);
    }

    internal static WaypointAuthoringProfile Profile(bool required)
    {
        var areaId = AreaId.From(Guid.Parse("a0641000-0000-4000-8000-000000000001"));
        var waypoints = new[]
        {
            Waypoint("a0641000-0000-4000-8000-000000000011", "Braise", -3),
            Waypoint("a0641000-0000-4000-8000-000000000012", "Porte", 0, marker: false),
            Waypoint("a0641000-0000-4000-8000-000000000013", "Autel", 3, marker: false),
        };
        var points = waypoints.Select((item, index) => new PatrolPoint(item.Id, item.Position,
            index, index * 45, 2 + index, index == 1 ? "inspect" : null,
            index == 0 ? "quest.crypt.active" : null, index + 1)).ToArray();
        var route = new PatrolRoute(Guid.Parse("a0641000-0000-4000-8000-000000000020"), 1,
            "test:r064", PatrolTraversalPolicy.PingPong, points, requiredAtRuntime: true);
        return new(Guid.Parse("a0641000-0000-4000-8000-000000000030"), 1, "test:r064", areaId,
            required, waypoints, [route]);
    }

    internal static AreaDefinition CreateArea(WaypointAuthoringProfile profile)
    {
        var area = new AreaDefinition(profile.AreaId, "crypt", "Crypte", []);
        return area.WithNavigation(area.Navigation.WithWaypoints(profile));
    }

    private static WaypointProfile Waypoint(string id, string name, float x, bool marker = true) => new(
        Guid.Parse(id), 1, "test:r064", name, new(x, 0, 0),
        new("flag", "#ff8844", "gameplay", true, 28),
        marker ? new MapMarker($"{name} révélée", "quest", -1, "quest", 2,
            MarkerVisibilityPolicy.HiddenUntilRevealed, MarkerVisibilityScope.Party, true, false,
            "quest.crypt.found", "pulse", true) : null,
        RequiredAtRuntime: true);
}
