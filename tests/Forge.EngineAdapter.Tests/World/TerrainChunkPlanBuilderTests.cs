using Forge.Domain.Areas;
using Forge.EngineAdapter.World;

namespace Forge.EngineAdapter.Tests.World;

public sealed class TerrainChunkPlanBuilderTests
{
    [Fact]
    public void PartitionsA512MeterTerrainIntoDeterministic64MeterSectors()
    {
        var areaId = AreaId.From(Guid.Parse("a1190000-0000-4000-8000-000000000091"));
        var source = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, new AreaDimensions(512, 512));
        const int samples = 129;
        var terrain = new TerrainDefinition(
            source.Terrain.Id,
            TerrainDefinition.CurrentSchemaVersion,
            TerrainProviderKind.Heightmap,
            512,
            512,
            samples,
            samples,
            16,
            20260805,
            8,
            new float[samples * samples],
            source.Terrain.MaterialLayers,
            [],
            string.Empty,
            string.Empty,
            string.Empty,
            "test");
        var world = new AreaWorldProfile(
            source.Id,
            source.SchemaVersion,
            terrain,
            source.Biomes,
            [],
            source.VolumetricNavigation,
            [],
            source.Loading,
            "test");
        var plan = TerrainBuildPlanBuilder.Build(world);

        var sectors = TerrainChunkPlanBuilder.Build(world, plan);

        Assert.Equal(64, sectors.Count);
        Assert.Equal(plan.Indices.Count, sectors.Sum(sector => sector.Indices.Count));
        Assert.All(sectors, sector =>
        {
            Assert.Equal(64, sector.WidthMeters);
            Assert.Equal(64, sector.DepthMeters);
            Assert.True(sector.VisibilityRangeEnd >= 128);
        });
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, sectors.Select(sector => sector.X).Distinct());
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, sectors.Select(sector => sector.Z).Distinct());
    }
}
