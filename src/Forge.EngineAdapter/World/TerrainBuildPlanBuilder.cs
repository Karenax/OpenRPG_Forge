using Forge.Domain.Areas;
using Forge.Domain.Validation;

namespace Forge.EngineAdapter.World;

public static class TerrainBuildPlanBuilder
{
    public const string ExternalProviderFallbackCode = "area.world.terrain_provider_fallback";

    public static TerrainBuildPlan Build(AreaWorldProfile world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var terrain = world.Terrain;
        var diagnostics = new List<DomainDiagnostic>();
        if (!terrain.Enabled)
        {
            return new TerrainBuildPlan(false, [], [], 0, world.Features, world.VolumetricNavigation,
                world.FlightRoutes, diagnostics);
        }

        if (terrain.Provider is TerrainProviderKind.Mesh or TerrainProviderKind.Voxel &&
            string.IsNullOrWhiteSpace(terrain.Provider == TerrainProviderKind.Mesh ? terrain.MeshResource : terrain.VoxelResource))
        {
            diagnostics.Add(new DomainDiagnostic(ExternalProviderFallbackCode,
                $"Terrain provider '{terrain.Provider}' has no external resource; canonical samples are used.",
                "world/terrain/provider"));
        }

        ITerrainProvider provider = new CanonicalTerrainProvider(terrain);
        var materialPaint = terrain.MaterialPaint.ToDictionary(cell => (cell.X, cell.Z), cell => cell.MaterialLayerId);
        var materialColors = terrain.MaterialLayers.ToDictionary(
            layer => layer.Id,
            layer => TerrainVertexColor.Parse(layer.AlbedoColor));
        var vertices = new List<TerrainBuildVertex>(terrain.SamplesX * terrain.SamplesZ);
        for (var z = 0; z < terrain.SamplesZ; z++)
        {
            for (var x = 0; x < terrain.SamplesX; x++)
            {
                var position = provider.PositionOf(x, z);
                var normal = NormalAt(provider, terrain, x, z);
                var slope = SlopeDegrees(provider, terrain, x, z);
                var color = SelectColor(world, materialPaint, materialColors, x, z, position.Y, slope);
                vertices.Add(new TerrainBuildVertex(position, normal, x / (float)(terrain.SamplesX - 1),
                    z / (float)(terrain.SamplesZ - 1), color));
            }
        }

        var indices = new List<int>((terrain.SamplesX - 1) * (terrain.SamplesZ - 1) * 6);
        for (var z = 0; z < terrain.SamplesZ - 1; z++)
        {
            for (var x = 0; x < terrain.SamplesX - 1; x++)
            {
                if (provider.IsHoleCell(x, z))
                {
                    continue;
                }

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

        var chunksX = (int)Math.Ceiling((terrain.SamplesX - 1) / (double)terrain.ChunkSize);
        var chunksZ = (int)Math.Ceiling((terrain.SamplesZ - 1) / (double)terrain.ChunkSize);
        return new TerrainBuildPlan(true, vertices, indices, chunksX * chunksZ, world.Features,
            world.VolumetricNavigation, world.FlightRoutes, diagnostics);
    }

    private static float SlopeDegrees(ITerrainProvider provider, TerrainDefinition terrain, int x, int z)
    {
        var left = provider.SampleHeight(Math.Max(0, x - 1), z);
        var right = provider.SampleHeight(Math.Min(terrain.SamplesX - 1, x + 1), z);
        var back = provider.SampleHeight(x, Math.Max(0, z - 1));
        var front = provider.SampleHeight(x, Math.Min(terrain.SamplesZ - 1, z + 1));
        var stepX = terrain.Width / (float)(terrain.SamplesX - 1);
        var stepZ = terrain.Depth / (float)(terrain.SamplesZ - 1);
        var gradientX = (right - left) / Math.Max(stepX, .0001f);
        var gradientZ = (front - back) / Math.Max(stepZ, .0001f);
        return MathF.Atan(MathF.Sqrt((gradientX * gradientX) + (gradientZ * gradientZ))) * (180 / MathF.PI);
    }

    private static Forge.Domain.Spatial.Vector3Value NormalAt(
        ITerrainProvider provider,
        TerrainDefinition terrain,
        int x,
        int z)
    {
        var leftX = Math.Max(0, x - 1);
        var rightX = Math.Min(terrain.SamplesX - 1, x + 1);
        var backZ = Math.Max(0, z - 1);
        var frontZ = Math.Min(terrain.SamplesZ - 1, z + 1);
        var stepX = terrain.Width / (float)(terrain.SamplesX - 1);
        var stepZ = terrain.Depth / (float)(terrain.SamplesZ - 1);
        // Compute the normal once from the complete height field with a 3x3 Sobel
        // gradient. Besides making shared sector vertices bit-identical, the diagonal
        // contribution prevents isolated samples from revealing the canonical square
        // grid as cross-shaped lighting facets in Forward+.
        var gradientX = (
            provider.SampleHeight(rightX, backZ) +
            2f * provider.SampleHeight(rightX, z) +
            provider.SampleHeight(rightX, frontZ) -
            provider.SampleHeight(leftX, backZ) -
            2f * provider.SampleHeight(leftX, z) -
            provider.SampleHeight(leftX, frontZ)) /
            Math.Max(4f * (rightX - leftX) * stepX, .0001f);
        var gradientZ = (
            provider.SampleHeight(leftX, frontZ) +
            2f * provider.SampleHeight(x, frontZ) +
            provider.SampleHeight(rightX, frontZ) -
            provider.SampleHeight(leftX, backZ) -
            2f * provider.SampleHeight(x, backZ) -
            provider.SampleHeight(rightX, backZ)) /
            Math.Max(4f * (frontZ - backZ) * stepZ, .0001f);
        var length = MathF.Sqrt((gradientX * gradientX) + 1f + (gradientZ * gradientZ));
        return new(-gradientX / length, 1f / length, -gradientZ / length);
    }

    private static string SelectColor(
        AreaWorldProfile world,
        IReadOnlyDictionary<(int X, int Z), Guid> materialPaint,
        IReadOnlyDictionary<Guid, TerrainVertexColor> materialColors,
        int sampleX,
        int sampleZ,
        float altitude,
        float slope)
    {
        var biome = world.Biomes.FirstOrDefault(candidate => altitude >= candidate.MinimumAltitude &&
            altitude <= candidate.MaximumAltitude && slope >= candidate.MinimumSlopeDegrees &&
            slope <= candidate.MaximumSlopeDegrees && .5f >= candidate.MinimumMoisture && .5f <= candidate.MaximumMoisture);
        var materialId = biome?.MaterialLayerId ?? world.Terrain.MaterialLayers[0].Id;
        var fallback = materialColors[materialId];

        // Paint is authored per terrain cell, while rendering attributes live on shared
        // vertices. Reconstruct the vertex colour from every cell touching that vertex;
        // selecting only the lower-right cell exposes the canonical square grid.
        var minimumCellX = Math.Max(0, sampleX - 1);
        var maximumCellX = Math.Min(sampleX, world.Terrain.SamplesX - 2);
        var minimumCellZ = Math.Max(0, sampleZ - 1);
        var maximumCellZ = Math.Min(sampleZ, world.Terrain.SamplesZ - 2);
        var red = 0;
        var green = 0;
        var blue = 0;
        var alpha = 0;
        var count = 0;
        var includeAlpha = false;
        var first = default(TerrainVertexColor);
        var allSame = true;
        for (var cellZ = minimumCellZ; cellZ <= maximumCellZ; cellZ++)
        {
            for (var cellX = minimumCellX; cellX <= maximumCellX; cellX++)
            {
                var contribution = materialPaint.TryGetValue((cellX, cellZ), out var paintedMaterialId)
                    ? materialColors[paintedMaterialId]
                    : fallback;
                if (count == 0)
                {
                    first = contribution;
                }
                else if (contribution != first)
                {
                    allSame = false;
                }

                red += contribution.Red;
                green += contribution.Green;
                blue += contribution.Blue;
                alpha += contribution.Alpha;
                includeAlpha |= contribution.IncludeAlpha;
                count++;
            }
        }

        return allSame
            ? first.Hex
            : TerrainVertexColor.Blend(red, green, blue, alpha, count, includeAlpha).Hex;
    }

    private readonly record struct TerrainVertexColor(
        byte Red,
        byte Green,
        byte Blue,
        byte Alpha,
        bool IncludeAlpha,
        string Hex)
    {
        public static TerrainVertexColor Parse(string value)
        {
            var red = Convert.ToByte(value.Substring(1, 2), 16);
            var green = Convert.ToByte(value.Substring(3, 2), 16);
            var blue = Convert.ToByte(value.Substring(5, 2), 16);
            var includeAlpha = value.Length == 9;
            var alpha = includeAlpha ? Convert.ToByte(value.Substring(7, 2), 16) : byte.MaxValue;
            return new(red, green, blue, alpha, includeAlpha, value);
        }

        public static TerrainVertexColor Blend(
            int red,
            int green,
            int blue,
            int alpha,
            int count,
            bool includeAlpha)
        {
            var blendedRed = (byte)((red + (count / 2)) / count);
            var blendedGreen = (byte)((green + (count / 2)) / count);
            var blendedBlue = (byte)((blue + (count / 2)) / count);
            var blendedAlpha = (byte)((alpha + (count / 2)) / count);
            var hex = includeAlpha
                ? $"#{blendedRed:x2}{blendedGreen:x2}{blendedBlue:x2}{blendedAlpha:x2}"
                : $"#{blendedRed:x2}{blendedGreen:x2}{blendedBlue:x2}";
            return new(blendedRed, blendedGreen, blendedBlue, blendedAlpha, includeAlpha, hex);
        }
    }
}
