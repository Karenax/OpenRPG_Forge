using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.EngineAdapter.World;

public sealed record BiomeScatterPlacement(Vector3Value Position, float YawRadians, float UniformScale);

public static class BiomeScatterPlanBuilder
{
    public static IReadOnlyList<BiomeScatterPlacement> Build(
        AreaWorldProfile world,
        BiomeLayerDefinition biome)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(biome);
        var terrain = world.Terrain;
        if (!terrain.Enabled || biome.ScatterDensity <= 0 || string.IsNullOrWhiteSpace(biome.ScatterResource))
            return [];

        var largeProductionSurface = terrain.Width * terrain.Depth >= 256f * 256f;
        var maximumPlacements = biome.Tags.Contains("ground-cover", StringComparer.OrdinalIgnoreCase)
            ? largeProductionSurface ? 2_600 : 420
            : largeProductionSurface ? 1_600 : 220;
        var target = Math.Clamp(
            Math.Max(1, (int)Math.Round(terrain.Width * terrain.Depth / 90d * biome.ScatterDensity)),
            0,
            maximumPlacements);
        var result = new List<BiomeScatterPlacement>(target);
        var biomeSeed = BitConverter.ToInt32(biome.Id.ToByteArray(), 0);
        var random = new StableRandom(unchecked(terrain.Seed ^ biomeSeed));
        var minimumDistance = biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase) ? 3.4f : 1.1f;
        var materialPaint = terrain.MaterialPaint.ToDictionary(
            cell => (cell.X, cell.Z),
            cell => cell.MaterialLayerId);

        for (var attempt = 0; attempt < target * 18 && result.Count < target; attempt++)
        {
            var x = (random.NextFloat() - .5f) * terrain.Width;
            var z = (random.NextFloat() - .5f) * terrain.Depth;
            var height = terrain.HeightAt(x, z);
            var slope = SlopeDegrees(terrain, x, z);
            var cell = CellAt(terrain, x, z);
            if (height < biome.MinimumAltitude || height > biome.MaximumAltitude ||
                slope < biome.MinimumSlopeDegrees || slope > biome.MaximumSlopeDegrees ||
                terrain.IsHoleCell(cell.X, cell.Z) ||
                materialPaint.TryGetValue(cell, out var paintedMaterial) && paintedMaterial != biome.MaterialLayerId ||
                IsExcluded(world, biome, x, z) ||
                result.Any(existing => DistanceSquared(existing.Position, x, z) < minimumDistance * minimumDistance))
                continue;

            var authoredScale = biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase)
                ? 2.75f
                : biome.Tags.Contains("ground-cover", StringComparer.OrdinalIgnoreCase) ? 1.35f : 1f;
            var scale = (.84f + random.NextFloat() * .42f) * authoredScale;
            result.Add(new BiomeScatterPlacement(
                new Vector3Value(x, height, z),
                random.NextFloat() * MathF.Tau,
                scale));
        }

        return result;
    }

    private static (int X, int Z) CellAt(TerrainDefinition terrain, float x, float z)
    {
        var normalizedX = Math.Clamp((x + terrain.Width / 2f) / terrain.Width, 0, 1);
        var normalizedZ = Math.Clamp((z + terrain.Depth / 2f) / terrain.Depth, 0, 1);
        return (
            Math.Min((int)MathF.Floor(normalizedX * (terrain.SamplesX - 1)), terrain.SamplesX - 2),
            Math.Min((int)MathF.Floor(normalizedZ * (terrain.SamplesZ - 1)), terrain.SamplesZ - 2));
    }

    private static bool IsExcluded(AreaWorldProfile world, BiomeLayerDefinition biome, float x, float z)
    {
        foreach (var feature in world.Features)
        {
            var tag = feature.Kind.ToString().ToLowerInvariant();
            if (!biome.ExclusionTags.Contains(tag, StringComparer.OrdinalIgnoreCase)) continue;
            for (var index = 0; index < feature.ControlPoints.Count - 1; index++)
            {
                var a = feature.ControlPoints[index];
                var b = feature.ControlPoints[index + 1];
                if (DistanceToSegment(x, z, a.X, a.Z, b.X, b.Z) <= feature.Width * .5f + 1.5f)
                    return true;
            }
        }

        return false;
    }

    private static float SlopeDegrees(TerrainDefinition terrain, float x, float z)
    {
        const float step = 1f;
        var dx = terrain.HeightAt(x + step, z) - terrain.HeightAt(x - step, z);
        var dz = terrain.HeightAt(x, z + step) - terrain.HeightAt(x, z - step);
        return MathF.Atan(MathF.Sqrt(dx * dx + dz * dz) / (step * 2)) * 180f / MathF.PI;
    }

    private static float DistanceSquared(Vector3Value position, float x, float z)
    {
        var dx = position.X - x;
        var dz = position.Z - z;
        return dx * dx + dz * dz;
    }

    private static float DistanceToSegment(float px, float pz, float ax, float az, float bx, float bz)
    {
        var dx = bx - ax;
        var dz = bz - az;
        var length = dx * dx + dz * dz;
        if (length <= .0001f) return MathF.Sqrt((px - ax) * (px - ax) + (pz - az) * (pz - az));
        var t = Math.Clamp(((px - ax) * dx + (pz - az) * dz) / length, 0, 1);
        var x = ax + t * dx;
        var z = az + t * dz;
        return MathF.Sqrt((px - x) * (px - x) + (pz - z) * (pz - z));
    }

    private sealed class StableRandom
    {
        private uint _state;

        internal StableRandom(int seed) => _state = unchecked((uint)seed) | 1u;

        internal float NextFloat()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (_state & 0x00ffffff) / 16777216f;
        }
    }
}
