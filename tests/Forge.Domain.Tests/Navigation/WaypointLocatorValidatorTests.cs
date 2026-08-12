using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Navigation;

public sealed class WaypointLocatorValidatorTests
{
    [Fact]
    public void R065_arrival_locators_collections_and_route_visualization_are_canonical_and_valid()
    {
        var profile = ValidProfile();
        var area = WaypointAuthoringValidatorTests.CreateArea(profile);

        var diagnostics = WaypointAuthoringValidator.Validate(area);

        Assert.Empty(diagnostics);
        Assert.Equal(2, profile.Locators.Count);
        Assert.Single(profile.Collections);
        Assert.True(profile.RouteVisualization.ShowNavigationProjection);
    }

    [Fact]
    public void R065_validation_reports_arrival_locator_collection_and_visualization_limits()
    {
        var source = WaypointAuthoringValidatorTests.Profile(required: true);
        var invalidWaypoint = source.Waypoints[0] with
        {
            Arrival = new(ArrivalFacingMode.Direction, 720, "forbidden-target", "bad action", -1,
                "open!", "callback()"),
        };
        var invalidLocator = new GenericLocator(Guid.Empty, 2, string.Empty, string.Empty,
            new Vector3Value(10_000, 0, 0), LocatorPurpose.Audio, [string.Empty],
            null, ExplicitOverrides: new Dictionary<string, string> { [string.Empty] = "x" });
        var invalidCollection = new AnchorCollection(Guid.Empty, 2, string.Empty, string.Empty,
            [Guid.NewGuid()], string.Empty, Selection: LocatorSelectionMode.Nearest,
            RequiredAtRuntime: true);
        var invalidVisualization = new RouteVisualizationProfile(Guid.Empty, 2, string.Empty,
            false, false, false, false, false, false, false, 0, true);
        var profile = new WaypointAuthoringProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.AreaId, true, [invalidWaypoint, .. source.Waypoints.Skip(1)], source.PatrolRoutes,
            [invalidLocator], [invalidCollection], invalidVisualization);

        var diagnostics = WaypointAuthoringValidator.Validate(
            WaypointAuthoringValidatorTests.CreateArea(profile));

        Assert.Contains(diagnostics, item => item.Code == WaypointAuthoringValidator.ArrivalInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == WaypointAuthoringValidator.LocatorInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == WaypointAuthoringValidator.CollectionInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == WaypointAuthoringValidator.VisualizationInvalidCode);
    }

    [Fact]
    public void R065_area_clone_remaps_locator_collection_and_visualization_identity()
    {
        var source = ValidProfile();
        var destination = AreaId.From(Guid.Parse("a0651000-0000-4000-8000-000000000099"));

        var clone = WaypointAuthoringProfile.CloneForArea(source, destination);

        Assert.Equal(destination, clone.AreaId);
        Assert.NotEqual(source.Locators[0].Id, clone.Locators[0].Id);
        Assert.All(clone.Collections.SelectMany(item => item.LocatorIds), id =>
            Assert.Contains(clone.Locators, locator => locator.Id == id));
        Assert.NotEqual(source.RouteVisualization.Id, clone.RouteVisualization.Id);
    }

    internal static WaypointAuthoringProfile ValidProfile()
    {
        var source = WaypointAuthoringValidatorTests.Profile(required: true);
        var waypoints = source.Waypoints.Select((item, index) => index == 0 ? item with
        {
            Arrival = new(ArrivalFacingMode.Target, 0, "gate.ashes", "inspect", 1, "open",
                "waypoint.arrived"),
        } : item).ToArray();
        var locators = new[]
        {
            new GenericLocator(Guid.Parse("a0651000-0000-4000-8000-000000000011"), 1,
                "test:r065", "Brasero", new(-2, 0, 2), LocatorPurpose.Vfx,
                ["crypt", "fire"],
                new ArrivalBehavior(ArrivalFacingMode.Direction, 90, null, "ignite", .5,
                    "inspect", "locator.arrived"), "vfx:ember"),
            new GenericLocator(Guid.Parse("a0651000-0000-4000-8000-000000000012"), 1,
                "test:r065", "Cloche", new(2, 0, 2), LocatorPurpose.Audio,
                ["crypt", "ritual"], null, "audio:bell"),
        };
        var collection = new AnchorCollection(Guid.Parse("a0651000-0000-4000-8000-000000000020"),
            1, "test:r065", "crypt", locators.Select(item => item.Id).ToArray(), "Crypte",
            RequiredTags: ["crypt"], Selection: LocatorSelectionMode.Nearest,
            Origin: Vector3Value.Zero, Seed: 2026, RequiredAtRuntime: true);
        var visualization = new RouteVisualizationProfile(
            Guid.Parse("a0651000-0000-4000-8000-000000000030"), 1, "test:r065",
            true, true, true, true, true, true, true, 1.25, true);
        return new(source.Id, source.SchemaVersion, "test:r065", source.AreaId, true, waypoints,
            source.PatrolRoutes, locators, [collection], visualization);
    }
}
