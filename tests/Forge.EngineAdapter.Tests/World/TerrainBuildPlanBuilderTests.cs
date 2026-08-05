using Forge.Domain.Areas;
using Forge.EngineAdapter.World;

namespace Forge.EngineAdapter.Tests.World;

public sealed class TerrainBuildPlanBuilderTests
{
    [Fact]
    public void BuildsHeightmapMeshAndOmitsHoleTrianglesDeterministically()
    {
        var areaId = AreaId.From(Guid.Parse("a1190000-0000-4000-8000-000000000001"));
        var source = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, new AreaDimensions(64, 64));
        var hole = new TerrainHoleDefinition(Guid.NewGuid(), "Cave", 2, 3, 2, 1, "test");
        var terrain = new TerrainDefinition(source.Terrain.Id, 1, source.Terrain.Provider,
            source.Terrain.Width, source.Terrain.Depth, source.Terrain.SamplesX, source.Terrain.SamplesZ,
            source.Terrain.ChunkSize, source.Terrain.Seed, source.Terrain.HeightScale, source.Terrain.Heights,
            source.Terrain.MaterialLayers, [hole], string.Empty, string.Empty, string.Empty, "test");
        var world = new AreaWorldProfile(source.Id, 1, terrain, source.Biomes, [], source.VolumetricNavigation,
            [], source.Loading, "test");

        var first = TerrainBuildPlanBuilder.Build(world);
        var second = TerrainBuildPlanBuilder.Build(world);

        Assert.True(first.Enabled);
        Assert.Equal(17 * 17, first.Vertices.Count);
        Assert.Equal((((17 - 1) * (17 - 1)) - 2) * 2, first.TriangleCount);
        Assert.Equal(1, first.ChunkCount);
        Assert.Equal(first.Indices, second.Indices);
        Assert.Equal(first.Vertices, second.Vertices);
        Assert.Empty(first.Diagnostics);
    }

    [Fact]
    public void DisabledTerrainProducesEmptyBuildPlan()
    {
        var areaId = AreaId.From(Guid.Parse("a1190000-0000-4000-8000-000000000002"));
        var world = AreaWorldProfile.CreateDefault(areaId, AreaKind.Interior, new AreaDimensions(20, 20));

        var plan = TerrainBuildPlanBuilder.Build(world);

        Assert.False(plan.Enabled);
        Assert.Empty(plan.Vertices);
        Assert.Empty(plan.Indices);
        Assert.Equal(0, plan.ChunkCount);
    }

    [Fact]
    public void SparseMaterialPaintOverridesBiomeColorForPaintedCell()
    {
        var areaId = AreaId.From(Guid.Parse("a1190000-0000-4000-8000-000000000003"));
        var source = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, new AreaDimensions(64, 64));
        var material = source.Terrain.MaterialLayers.Last();
        var terrain = new TerrainDefinition(source.Terrain.Id, 1, source.Terrain.Provider,
            source.Terrain.Width, source.Terrain.Depth, source.Terrain.SamplesX, source.Terrain.SamplesZ,
            source.Terrain.ChunkSize, source.Terrain.Seed, source.Terrain.HeightScale, source.Terrain.Heights,
            source.Terrain.MaterialLayers, source.Terrain.Holes, string.Empty, string.Empty, string.Empty, "test",
            [new TerrainMaterialPaintCell(0, 0, material.Id)]);
        var world = new AreaWorldProfile(source.Id, 1, terrain, source.Biomes, [], source.VolumetricNavigation,
            [], source.Loading, "test");

        var plan = TerrainBuildPlanBuilder.Build(world);

        Assert.Equal(material.AlbedoColor, plan.Vertices[0].AlbedoColor);
        Assert.NotEqual(material.AlbedoColor, plan.Vertices[1].AlbedoColor);
    }
}
