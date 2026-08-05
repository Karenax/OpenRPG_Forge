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
        var vertices = new List<TerrainBuildVertex>(terrain.SamplesX * terrain.SamplesZ);
        for (var z = 0; z < terrain.SamplesZ; z++)
        {
            for (var x = 0; x < terrain.SamplesX; x++)
            {
                var position = provider.PositionOf(x, z);
                var slope = SlopeDegrees(provider, terrain, x, z);
                var color = SelectColor(world, materialPaint, x, z, position.Y, slope);
                vertices.Add(new TerrainBuildVertex(position, x / (float)(terrain.SamplesX - 1),
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

    private static string SelectColor(AreaWorldProfile world, IReadOnlyDictionary<(int X, int Z), Guid> materialPaint,
        int sampleX, int sampleZ, float altitude, float slope)
    {
        var cell = (Math.Min(sampleX, world.Terrain.SamplesX - 2), Math.Min(sampleZ, world.Terrain.SamplesZ - 2));
        if (materialPaint.TryGetValue(cell, out var paintedMaterialId))
        {
            return world.Terrain.MaterialLayers.First(layer => layer.Id == paintedMaterialId).AlbedoColor;
        }

        var biome = world.Biomes.FirstOrDefault(candidate => altitude >= candidate.MinimumAltitude &&
            altitude <= candidate.MaximumAltitude && slope >= candidate.MinimumSlopeDegrees &&
            slope <= candidate.MaximumSlopeDegrees && .5f >= candidate.MinimumMoisture && .5f <= candidate.MaximumMoisture);
        var materialId = biome?.MaterialLayerId ?? world.Terrain.MaterialLayers[0].Id;
        return world.Terrain.MaterialLayers.First(layer => layer.Id == materialId).AlbedoColor;
    }
}
