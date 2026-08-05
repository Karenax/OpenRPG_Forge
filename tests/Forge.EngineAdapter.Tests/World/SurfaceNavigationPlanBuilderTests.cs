using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.World;

namespace Forge.EngineAdapter.Tests.World;

public sealed class SurfaceNavigationPlanBuilderTests
{
    [Fact]
    public void BuildsTerrainPolygonsWhileBlockingRiverOutsideAuthoredBridge()
    {
        var areaId = AreaId.From(Guid.Parse("14f97676-028e-43cc-82c1-6f99642e83c8"));
        var world = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, new(64, 64));
        var river = new TerrainFeatureDefinition(
            Guid.Parse("54df15c0-25c0-4a62-8c99-a59f8217fc64"),
            TerrainFeatureDefinition.CurrentSchemaVersion,
            "River",
            TerrainFeatureKind.River,
            [new(-28, 0, 0), new(28, 0, 0)],
            6,
            .1f,
            0,
            TerrainFeatureCrossingPolicy.RequiresBridge,
            ["temperate"],
            string.Empty,
            "test");
        var bridge = new TerrainFeatureDefinition(
            Guid.Parse("8d71359f-e37d-4e75-88cc-b56a04345229"),
            TerrainFeatureDefinition.CurrentSchemaVersion,
            "Bridge",
            TerrainFeatureKind.Bridge,
            [new(0, .2f, -6), new(0, .2f, 6)],
            4,
            .2f,
            0,
            TerrainFeatureCrossingPolicy.Allow,
            ["temperate"],
            string.Empty,
            "test");
        world = new AreaWorldProfile(
            world.Id,
            world.SchemaVersion,
            world.Terrain,
            world.Biomes,
            [river, bridge],
            world.VolumetricNavigation,
            world.FlightRoutes,
            world.Loading,
            "test");
        var defaults = AreaNavigationDefinition.CreateDefault(areaId);
        var navigation = new AreaNavigationDefinition(
            defaults.Id,
            defaults.CellSize,
            defaults.AgentProfiles,
            [new OffMeshNavigationLink(
                OffMeshNavigationLinkId.From(Guid.Parse("83b634ad-aafa-4836-9399-2b4e9e2fe683")),
                "Bridge crossing",
                NavigationLinkKind.Jump,
                new Vector3Value(0, .2f, -4),
                new Vector3Value(0, .2f, 4),
                true,
                .6f,
                "test")],
            "test");

        var plan = SurfaceNavigationPlanBuilder.Build(world, navigation);

        Assert.True(plan.Enabled);
        Assert.NotNull(plan.Agent);
        Assert.NotEmpty(plan.Triangles);
        Assert.True(plan.BlockedTriangles > 0);
        Assert.True(plan.Triangles.Count < ((world.Terrain.SamplesX - 1) * (world.Terrain.SamplesZ - 1) * 2));
        Assert.Single(plan.Links);
        Assert.DoesNotContain(plan.Diagnostics, item => item.Code == SurfaceNavigationPlanBuilder.EmptySurfaceCode);
    }
}
