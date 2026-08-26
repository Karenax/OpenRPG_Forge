using Forge.Domain.Areas;

namespace Forge.Domain.Tests.Areas;

public sealed class AreaMapExplorationGridTests
{
    [Fact]
    public void Centered_multifloor_grid_uses_versioned_keys_and_rejects_invalid_cells()
    {
        var grid = new AreaMapExplorationGrid(new AreaDimensions(10, 10), floorCount: 2, cellSize: 4);

        var ground = grid.Resolve(floor: 0, x: 0, z: 0);
        var upper = grid.Resolve(floor: 1, x: 0, z: 0);
        var validation = grid.ValidateKeys(
        [
            ground.Key,
            upper.Key,
            "0:0",
            "not-a-cell",
            "v1:0:3:0",
        ]);

        Assert.Equal(3, grid.CellColumns);
        Assert.Equal(3, grid.CellRows);
        Assert.Equal(18, grid.TotalCells);
        Assert.Equal("v1:0:1:1", ground.Key);
        Assert.Equal("v1:1:1:1", upper.Key);
        Assert.NotEqual(ground, upper);
        Assert.Equal([ground.Key, upper.Key], validation.ValidCells.Order(StringComparer.Ordinal));
        Assert.Contains(validation.Diagnostics,
            item => item.Code == AreaMapExplorationGrid.LegacyCellKeyMigratedCode);
        Assert.Contains(validation.Diagnostics,
            item => item.Code == AreaMapExplorationGrid.CellKeyInvalidCode);
        Assert.Contains(validation.Diagnostics,
            item => item.Code == AreaMapExplorationGrid.CellOutOfScopeCode);
        Assert.Equal(
        [
            AreaMapExplorationGrid.LegacyCellKeyMigratedCode,
            AreaMapExplorationGrid.CellKeyInvalidCode,
            AreaMapExplorationGrid.CellOutOfScopeCode,
        ], validation.Diagnostics.Select(item => item.Code));
    }
}
