using Forge.Domain.Areas;

namespace Forge.Application.Assistant;

public static class TerrainPlacementCandidateFinder
{
    public static IReadOnlyList<TerrainPlacementCandidate> Find(
        AreaDefinition area,
        float footprintWidth,
        float footprintDepth,
        float preferredX,
        float preferredZ,
        float searchRadiusMeters,
        float stepMeters,
        float maximumSurfaceVariationMeters,
        string environmentCategory,
        int minimumNearbyEnvironmentInstances,
        float environmentRadiusMeters,
        int maximumFootprintEnvironmentInstances,
        int limit,
        float minimumEnvironmentDensityPerHectare = 0)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentCategory);
        if (!float.IsFinite(footprintWidth) || !float.IsFinite(footprintDepth) ||
            footprintWidth is < 1 or > 512 || footprintDepth is < 1 or > 512 ||
            !float.IsFinite(preferredX) || !float.IsFinite(preferredZ) ||
            !float.IsFinite(searchRadiusMeters) || searchRadiusMeters is < 0 or > 4096 ||
            !float.IsFinite(stepMeters) || stepMeters is < 1 or > 128 ||
            !float.IsFinite(maximumSurfaceVariationMeters) || maximumSurfaceVariationMeters is < 0 or > 8 ||
            minimumNearbyEnvironmentInstances is < 0 or > 1_000_000 ||
            !float.IsFinite(environmentRadiusMeters) || environmentRadiusMeters is < 1 or > 1024 ||
            !float.IsFinite(minimumEnvironmentDensityPerHectare) ||
            minimumEnvironmentDensityPerHectare is < 0 or > 100_000 ||
            maximumFootprintEnvironmentInstances is < 0 or > 1_000_000 || limit is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(footprintWidth));
        if (area.World.PlacementSurface is null && !area.World.Terrain.Enabled)
            return [];

        var surfaceWidth = area.World.PlacementSurface?.Width ?? area.World.Terrain.Width;
        var surfaceDepth = area.World.PlacementSurface?.Depth ?? area.World.Terrain.Depth;
        var minimumX = Math.Max(-surfaceWidth / 2f + footprintWidth / 2f, preferredX - searchRadiusMeters);
        var maximumX = Math.Min(surfaceWidth / 2f - footprintWidth / 2f, preferredX + searchRadiusMeters);
        var minimumZ = Math.Max(-surfaceDepth / 2f + footprintDepth / 2f, preferredZ - searchRadiusMeters);
        var maximumZ = Math.Min(surfaceDepth / 2f - footprintDepth / 2f, preferredZ + searchRadiusMeters);
        if (minimumX > maximumX || minimumZ > maximumZ)
            return [];

        var category = environmentCategory.Trim().ToLowerInvariant();
        var occupancy = area.World.EnvironmentOccupancy;
        var candidates = new List<TerrainPlacementCandidate>();
        var iterations = 0;
        for (var z = minimumZ; z <= maximumZ + .001f; z += stepMeters)
        {
            for (var x = minimumX; x <= maximumX + .001f; x += stepMeters)
            {
                if (++iterations > 262_144)
                    throw new InvalidOperationException("Terrain candidate search exceeded its bounded grid.");
                var dx = x - preferredX;
                var dz = z - preferredZ;
                var distance = MathF.Sqrt((dx * dx) + (dz * dz));
                if (distance > searchRadiusMeters + .001f)
                    continue;
                var inspection = TerrainPlacementFootprintInspector.Inspect(
                    area, x, z, footprintWidth, footprintDepth);
                if (!inspection.SurfaceAvailable || !inspection.InBounds ||
                    inspection.SurfaceVariationMeters > maximumSurfaceVariationMeters + .0001f)
                    continue;
                var nearby = occupancy?.CountNear(category, x, z, environmentRadiusMeters) ?? 0;
                var density = occupancy?.DensityPerHectare(category, x, z, environmentRadiusMeters) ?? 0;
                var inside = occupancy?.CountIntersectingFootprint(
                    category, x, z, footprintWidth, footprintDepth) ?? 0;
                if (nearby < minimumNearbyEnvironmentInstances ||
                    density + .0001f < minimumEnvironmentDensityPerHectare ||
                    inside > maximumFootprintEnvironmentInstances)
                    continue;
                candidates.Add(new(x, z, inspection.MinimumHeight, inspection.SurfaceVariationMeters,
                    nearby, inside, distance, density));
            }
        }

        return candidates.OrderByDescending(item => item.NearbyEnvironmentDensityPerHectare)
            .ThenByDescending(item => item.NearbyEnvironmentInstances)
            .ThenBy(item => item.FootprintEnvironmentInstances)
            .ThenBy(item => item.SurfaceVariationMeters)
            .ThenBy(item => item.DistanceFromPreferredMeters)
            .ThenBy(item => item.Z)
            .ThenBy(item => item.X)
            .Take(limit)
            .ToArray();
    }
}
