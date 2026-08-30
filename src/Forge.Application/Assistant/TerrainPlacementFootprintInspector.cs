using Forge.Domain.Areas;

namespace Forge.Application.Assistant;

public static class TerrainPlacementFootprintInspector
{
    public static TerrainPlacementFootprintInspection Inspect(
        AreaDefinition area,
        float centerX,
        float centerZ,
        float width,
        float depth,
        int samplesPerAxis = 5)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (!float.IsFinite(centerX) || !float.IsFinite(centerZ) ||
            !float.IsFinite(width) || !float.IsFinite(depth) || width <= 0 || depth <= 0 ||
            samplesPerAxis is < 2 or > 17)
            throw new ArgumentOutOfRangeException(nameof(width));

        var surfaceAvailable = area.World.PlacementSurface is not null || area.World.Terrain.Enabled;
        if (!surfaceAvailable)
            return new(false, false, 0, 0, 0, 0, 0);
        var surfaceWidth = area.World.PlacementSurface?.Width ?? area.World.Terrain.Width;
        var surfaceDepth = area.World.PlacementSurface?.Depth ?? area.World.Terrain.Depth;
        var halfWidth = width / 2;
        var halfDepth = depth / 2;
        var inBounds = centerX - halfWidth >= -surfaceWidth / 2f &&
                       centerX + halfWidth <= surfaceWidth / 2f &&
                       centerZ - halfDepth >= -surfaceDepth / 2f &&
                       centerZ + halfDepth <= surfaceDepth / 2f;
        if (!inBounds)
            return new(true, false, area.World.GroundHeightAt(centerX, centerZ), 0, 0, 0, 0);

        var minimum = float.PositiveInfinity;
        var maximum = float.NegativeInfinity;
        for (var zIndex = 0; zIndex < samplesPerAxis; zIndex++)
        {
            var z = centerZ - halfDepth + (depth * zIndex / (samplesPerAxis - 1f));
            for (var xIndex = 0; xIndex < samplesPerAxis; xIndex++)
            {
                var x = centerX - halfWidth + (width * xIndex / (samplesPerAxis - 1f));
                var height = area.World.GroundHeightAt(x, z);
                minimum = Math.Min(minimum, height);
                maximum = Math.Max(maximum, height);
            }
        }

        return new(true, true, area.World.GroundHeightAt(centerX, centerZ), minimum, maximum,
            maximum - minimum, samplesPerAxis * samplesPerAxis);
    }
}
