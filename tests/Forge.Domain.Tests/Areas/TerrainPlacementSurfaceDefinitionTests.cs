using Forge.Domain.Areas;

namespace Forge.Domain.Tests.Areas;

public sealed class TerrainPlacementSurfaceDefinitionTests
{
    [Fact]
    public void HeightAtUsesTheBakedCellDiagonalInsteadOfBilinearApproximation()
    {
        var mainDiagonal = Surface(TerrainCellDiagonal.Main);
        var antiDiagonal = Surface(TerrainCellDiagonal.Anti);

        Assert.Equal(3.5f, mainDiagonal.HeightAt(2.5f, -2.5f), 4);
        Assert.Equal(2.5f, antiDiagonal.HeightAt(2.5f, -2.5f), 4);
    }

    [Fact]
    public void EnvironmentOccupancyCountsOnlyTheRequestedCategoryInsideTheRadius()
    {
        var profile = new AreaEnvironmentOccupancyProfile(
            Guid.Parse("d1580000-0000-4000-8000-000000000001"),
            AreaEnvironmentOccupancyProfile.CurrentSchemaVersion,
            100,
            100,
            10,
            [
                new("vegetation", "biomes", 0, 0, 12),
                new("vegetation", "biomes", 20, 0, 7),
                new("scatter", "scatter", 0, 0, 50),
            ],
            new string('a', 64),
            "test:environment-occupancy");

        Assert.Equal(12, profile.CountNear("vegetation", 0, 0, 9.9f));
        Assert.Equal(19, profile.CountNear("vegetation", 0, 0, 20));
        Assert.Equal(50, profile.CountNear("scatter", 0, 0, 1));
        Assert.Equal(12 * 10_000f / (MathF.PI * 9.9f * 9.9f),
            profile.DensityPerHectare("vegetation", 0, 0, 9.9f), 4);
        Assert.Equal(12, profile.CountIntersectingFootprint("vegetation", 0, 0, 8, 8));
        Assert.Equal(19, profile.CountIntersectingFootprint("vegetation", 15, 0, 20, 8));
        Assert.Equal(0, profile.CountIntersectingFootprint("scatter", 30, 30, 4, 4));
    }

    private static TerrainPlacementSurfaceDefinition Surface(TerrainCellDiagonal diagonal) => new(
        Guid.Parse("d1580000-0000-4000-8000-000000000002"),
        TerrainPlacementSurfaceDefinition.CurrentSchemaVersion,
        10,
        10,
        2,
        2,
        10,
        [0, .2f, .4f, 1],
        diagonal,
        new string('b', 64),
        "test:terrain-placement-surface");
}
