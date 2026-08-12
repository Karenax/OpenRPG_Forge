using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Rendering;

namespace Forge.EngineAdapter.Tests.Rendering;

public sealed class AreaProductionPlanBuilderTests
{
    [Fact]
    public void ProductionPlansCoverTerrainFeaturesWindWeatherAndSky()
    {
        var areaId = AreaId.From(Guid.Parse("d1350000-0000-4000-8000-000000000001"));
        var world = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, new AreaDimensions(64, 64));
        var source = world.Terrain;
        var voxel = new TerrainDefinition(source.Id, source.SchemaVersion, TerrainProviderKind.Voxel,
            source.Width, source.Depth, source.SamplesX, source.SamplesZ, source.ChunkSize, source.Seed,
            source.HeightScale, source.Heights, source.MaterialLayers, source.Holes, source.HeightmapResource,
            "terrain/world.vox", source.MeshResource, "test:r135", source.MaterialPaint);
        var terrain = AreaProductionPlanBuilder.Terrain(voxel);
        Assert.True(terrain.UsesExternalResource);
        Assert.True(terrain.SupportsSculpt && terrain.SupportsMaterialPaint);

        TerrainFeatureDefinition Feature(Guid id, TerrainFeatureKind kind, Vector3Value a, Vector3Value b) =>
            new(id, 1, kind.ToString(), kind, [a, b], 2, 1, 0,
                TerrainFeatureCrossingPolicy.Allow, [], string.Empty, "test:r135");
        var features = AreaProductionPlanBuilder.Features([
            Feature(Guid.Parse("d1350000-0000-4000-8000-000000000002"), TerrainFeatureKind.Road,
                new(-10, 0, 0), new(10, 0, 0)),
            Feature(Guid.Parse("d1350000-0000-4000-8000-000000000003"), TerrainFeatureKind.River,
                new(0, 0, -10), new(0, 0, 10)),
        ]);
        Assert.All(features, plan => Assert.Single(plan.Intersections));
        Assert.All(features, plan => Assert.True(plan.Samples.Count > 2));

        var environment = AreaEnvironmentProfile.CreateDefault(areaId, AreaKind.Exterior);
        var wind = AreaProductionPlanBuilder.Wind(environment.Wind, null,
            ["materials/grass.tres", "materials/tree.tres"]);
        Assert.Equal(2, wind.MaterialResources.Count);
        Assert.Contains("wind_speed", wind.ShaderGlobals);
        var rain = environment.Weather.States.Single(state => state.Kind == WeatherKind.Rain);
        var weather = AreaProductionPlanBuilder.Weather(environment.Weather, rain);
        Assert.True(weather.ServerAuthoritative && weather.Replicated);
        Assert.StartsWith("weather.changed:", weather.ScriptEvent, StringComparison.Ordinal);
        Assert.Equal(rain.AudioCue, weather.AudioCue);
        Assert.Equal(rain.GameplayEffect, weather.GameplayEffect);

        var sky = AreaProductionPlanBuilder.Sky(environment.Sky,
            new SkyProfile(1, SkyProfileMode.Procedural, string.Empty, "#111111", "#222222", "#333333",
                1, .8, .5, 2, 10), 5);
        Assert.Equal(.5, sky.Progress, 3);
        Assert.Equal("area.sky.transition_ready", sky.DiagnosticCode);
    }

    [Fact]
    public void WaterPlanBuildsRiverSplineShaderBuoyancyNavigationAndAudio()
    {
        var body = new WaterBodyProfile(Guid.Parse("d1360000-0000-4000-8000-000000000001"),
            "River", WaterBodyKind.River, Vector3Value.Zero, new(12, 4, 40), 4,
            1, 0, 3, 2.5f, 4, true, WaterQualityTier.Ultra,
            "materials/water-river.tres", "water.river");

        var plan = AreaProductionPlanBuilder.Water(body, WaterQualityTier.High, .6f);

        Assert.Equal(18, plan.SurfaceVertices.Count);
        Assert.Equal(3, plan.FlowVelocity.X);
        Assert.Equal(1.5f, plan.BuoyancyAcceleration, 3);
        Assert.Equal(4, plan.NavigationCost);
        Assert.True(plan.SwimmingEnabled);
        Assert.Equal("water.river", plan.AudioCue);
        Assert.Equal(1, plan.ShaderParameters["water_foam"]);
        Assert.Equal("area.water.material_ready", plan.DiagnosticCode);
    }
}
