using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.World;

namespace Forge.EngineAdapter.Tests.World;

public sealed class BiomeScatterCellPlanBuilderTests
{
    [Fact]
    public void PartitionsPlacementsIntoDeterministicTerrainAlignedCells()
    {
        var areaId = AreaId.From(Guid.Parse("a23d0000-0000-4000-8000-000000000010"));
        var terrain = AreaWorldProfile.CreateDefault(
            areaId,
            AreaKind.Exterior,
            new AreaDimensions(128, 128)).Terrain;
        var placements = new[]
        {
            Placement(-60, -60),
            Placement(-20, -20),
            Placement(20, 20),
            Placement(60, 60),
        };

        var cells = BiomeScatterCellPlanBuilder.Build(terrain, placements, 64);

        Assert.Equal(2, cells.Count);
        Assert.Equal((0, 0), (cells[0].CellX, cells[0].CellZ));
        Assert.Equal(new Vector3Value(-32, 0, -32), cells[0].Origin);
        Assert.Equal(2, cells[0].Placements.Count);
        Assert.Equal((1, 1), (cells[1].CellX, cells[1].CellZ));
        Assert.Equal(new Vector3Value(32, 0, 32), cells[1].Origin);
        Assert.Equal(2, cells[1].Placements.Count);
    }

    [Fact]
    public void RenderingPolicyBoundsVisibilityAndDisablesGroundCoverShadows()
    {
        var ground = Biome("ground-cover");
        var tree = Biome("tree");

        var groundPolicy = BiomeScatterRenderingPolicy.For(ground);
        var treePolicy = BiomeScatterRenderingPolicy.For(tree);

        Assert.Equal(64, groundPolicy.CellSize);
        Assert.Equal(54, groundPolicy.VisibilityRangeEnd);
        Assert.False(groundPolicy.CastShadows);
        Assert.Equal(96, treePolicy.VisibilityRangeEnd);
        Assert.True(treePolicy.CastShadows);
    }

    private static BiomeScatterPlacement Placement(float x, float z) =>
        new(new Vector3Value(x, 1, z), 0, 1);

    private static BiomeLayerDefinition Biome(string tag) => new(
        Guid.NewGuid(),
        "Test",
        Guid.NewGuid(),
        -10,
        10,
        0,
        90,
        0,
        1,
        0.1f,
        "asset:test",
        [tag],
        [],
        1,
        "test");
}
