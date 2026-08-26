using Forge.Domain.Areas;
using Forge.EngineAdapter.World;

namespace Forge.EngineAdapter.Tests.World;

public sealed class TerrainChunkPlanBuilderTests
{
    [Fact]
    public void AdjacentSectorsShareCanonicalNormalsAndVertexAttributesOnASlope()
    {
        var areaId = AreaId.From(Guid.Parse("a1190000-0000-4000-8000-000000000093"));
        var source = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, new AreaDimensions(4, 4));
        const int samples = 5;
        var heights = new float[samples * samples];
        for (var z = 0; z < samples; z++)
            for (var x = 0; x < samples; x++)
                heights[(z * samples) + x] = (x + (z * .25f)) / 8f;
        var terrain = new TerrainDefinition(
            source.Terrain.Id,
            TerrainDefinition.CurrentSchemaVersion,
            TerrainProviderKind.Heightmap,
            4,
            4,
            samples,
            samples,
            2,
            20260825,
            8,
            heights,
            source.Terrain.MaterialLayers,
            [],
            string.Empty,
            string.Empty,
            string.Empty,
            "test:shared-sector-normals");
        var world = new AreaWorldProfile(
            source.Id,
            source.SchemaVersion,
            terrain,
            source.Biomes,
            [],
            source.VolumetricNavigation,
            [],
            source.Loading,
            "test:shared-sector-normals");

        var plan = TerrainBuildPlanBuilder.Build(world);
        var sectors = TerrainChunkPlanBuilder.Build(world, plan);
        var left = sectors.Single(sector => sector.X == 0 && sector.Z == 0);
        var right = sectors.Single(sector => sector.X == 1 && sector.Z == 0);
        var shared = left.Indices.Distinct().Intersect(right.Indices.Distinct()).Order().ToArray();

        Assert.Equal(new[] { 2, 7, 12 }, shared);
        var expectedLength = MathF.Sqrt((1f * 1f) + (1f * 1f) + (.25f * .25f));
        var expectedNormal = new Forge.Domain.Spatial.Vector3Value(
            -1f / expectedLength,
            1f / expectedLength,
            -.25f / expectedLength);
        foreach (var sourceIndex in shared)
        {
            var leftVertex = plan.Vertices[left.Indices.First(index => index == sourceIndex)];
            var rightVertex = plan.Vertices[right.Indices.First(index => index == sourceIndex)];
            Assert.Equal(leftVertex.Position, rightVertex.Position);
            Assert.Equal(leftVertex.U, rightVertex.U);
            Assert.Equal(leftVertex.V, rightVertex.V);
            Assert.Equal(leftVertex.AlbedoColor, rightVertex.AlbedoColor);
            Assert.Equal(leftVertex.Normal, rightVertex.Normal);
            Assert.InRange(leftVertex.Normal.X, expectedNormal.X - .0001f, expectedNormal.X + .0001f);
            Assert.InRange(leftVertex.Normal.Y, expectedNormal.Y - .0001f, expectedNormal.Y + .0001f);
            Assert.InRange(leftVertex.Normal.Z, expectedNormal.Z - .0001f, expectedNormal.Z + .0001f);
        }
    }

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
