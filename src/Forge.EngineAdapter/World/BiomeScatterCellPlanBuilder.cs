using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.EngineAdapter.World;

public sealed record BiomeScatterCellPlan(
    int CellX,
    int CellZ,
    Vector3Value Origin,
    IReadOnlyList<BiomeScatterPlacement> Placements);

public sealed record BiomeScatterRenderingPolicy(
    float CellSize,
    float VisibilityRangeEnd,
    bool CastShadows)
{
    public static BiomeScatterRenderingPolicy For(BiomeLayerDefinition biome)
    {
        ArgumentNullException.ThrowIfNull(biome);
        if (biome.Tags.Contains("lush-meadow", StringComparer.OrdinalIgnoreCase))
            return new(64, 92, false);
        if (biome.Tags.Contains("hero-landscape", StringComparer.OrdinalIgnoreCase) &&
            biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase))
            return new(64, 280, true);
        if (biome.Tags.Contains("hero-landscape", StringComparer.OrdinalIgnoreCase))
            return new(64, 140, false);
        if (biome.Tags.Contains("ground-cover", StringComparer.OrdinalIgnoreCase))
            return new(64, 54, false);
        if (biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase))
            return new(64, 96, true);
        return new(64, 72, false);
    }
}

public static class BiomeScatterCellPlanBuilder
{
    public static IReadOnlyList<BiomeScatterCellPlan> Build(
        TerrainDefinition terrain,
        IReadOnlyList<BiomeScatterPlacement> placements,
        float cellSize)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(placements);
        if (cellSize <= 0 || !float.IsFinite(cellSize))
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        if (placements.Count == 0) return [];

        var columns = Math.Max(1, (int)MathF.Ceiling(terrain.Width / cellSize));
        var rows = Math.Max(1, (int)MathF.Ceiling(terrain.Depth / cellSize));
        return placements
            .GroupBy(placement =>
            {
                var x = Math.Clamp((int)MathF.Floor((placement.Position.X + terrain.Width / 2f) / cellSize), 0, columns - 1);
                var z = Math.Clamp((int)MathF.Floor((placement.Position.Z + terrain.Depth / 2f) / cellSize), 0, rows - 1);
                return (X: x, Z: z);
            })
            .OrderBy(group => group.Key.Z)
            .ThenBy(group => group.Key.X)
            .Select(group =>
            {
                var startX = -terrain.Width / 2f + group.Key.X * cellSize;
                var startZ = -terrain.Depth / 2f + group.Key.Z * cellSize;
                var width = MathF.Min(cellSize, terrain.Width - group.Key.X * cellSize);
                var depth = MathF.Min(cellSize, terrain.Depth - group.Key.Z * cellSize);
                return new BiomeScatterCellPlan(
                    group.Key.X,
                    group.Key.Z,
                    new Vector3Value(startX + width / 2f, 0, startZ + depth / 2f),
                    group.ToArray());
            })
            .ToArray();
    }
}
