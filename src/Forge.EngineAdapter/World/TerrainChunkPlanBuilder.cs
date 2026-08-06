using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.EngineAdapter.World;

public sealed record TerrainChunkBuildPlan(
    int X,
    int Z,
    IReadOnlyList<int> Indices,
    Vector3Value Center,
    float WidthMeters,
    float DepthMeters,
    float VisibilityRangeEnd);

public static class TerrainChunkPlanBuilder
{
    public static IReadOnlyList<TerrainChunkBuildPlan> Build(
        AreaWorldProfile world,
        TerrainBuildPlan terrainPlan)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(terrainPlan);
        var terrain = world.Terrain;
        if (!terrainPlan.Enabled || terrainPlan.Indices.Count == 0) return [];

        var cellsX = terrain.SamplesX - 1;
        var cellsZ = terrain.SamplesZ - 1;
        var chunksX = (int)Math.Ceiling(cellsX / (double)terrain.ChunkSize);
        var chunksZ = (int)Math.Ceiling(cellsZ / (double)terrain.ChunkSize);
        var result = new List<TerrainChunkBuildPlan>(chunksX * chunksZ);
        for (var chunkZ = 0; chunkZ < chunksZ; chunkZ++)
        {
            for (var chunkX = 0; chunkX < chunksX; chunkX++)
            {
                var startX = chunkX * terrain.ChunkSize;
                var startZ = chunkZ * terrain.ChunkSize;
                var endX = Math.Min(startX + terrain.ChunkSize, cellsX);
                var endZ = Math.Min(startZ + terrain.ChunkSize, cellsZ);
                var indices = new List<int>((endX - startX) * (endZ - startZ) * 6);
                for (var z = startZ; z < endZ; z++)
                {
                    for (var x = startX; x < endX; x++)
                    {
                        if (terrain.IsHoleCell(x, z)) continue;
                        var topLeft = (z * terrain.SamplesX) + x;
                        var topRight = topLeft + 1;
                        var bottomLeft = topLeft + terrain.SamplesX;
                        var bottomRight = bottomLeft + 1;
                        indices.Add(topLeft);
                        indices.Add(bottomLeft);
                        indices.Add(topRight);
                        indices.Add(topRight);
                        indices.Add(bottomLeft);
                        indices.Add(bottomRight);
                    }
                }

                if (indices.Count == 0) continue;
                var minimum = terrainPlan.Vertices[(startZ * terrain.SamplesX) + startX].Position;
                var maximum = terrainPlan.Vertices[(endZ * terrain.SamplesX) + endX].Position;
                var width = MathF.Abs(maximum.X - minimum.X);
                var depth = MathF.Abs(maximum.Z - minimum.Z);
                var center = new Vector3Value(
                    (minimum.X + maximum.X) * .5f,
                    0,
                    (minimum.Z + maximum.Z) * .5f);
                var sectorSpan = MathF.Max(width, depth);
                var visibilityRange = MathF.Max(128, sectorSpan * (world.Loading.ChunkRadius + 2.5f));
                result.Add(new TerrainChunkBuildPlan(
                    chunkX,
                    chunkZ,
                    indices,
                    center,
                    width,
                    depth,
                    visibilityRange));
            }
        }

        return result;
    }
}
