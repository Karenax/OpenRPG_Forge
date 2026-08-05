using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.Domain.Navigation;

public static class VolumetricNavigationGraphBuilder
{
    public const int DefaultMaximumCells = 1_000_000;

    public static VolumetricNavigationGraph Build(VolumetricNavigationProfile profile,
        int maximumCells = DefaultMaximumCells)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.Enabled)
        {
            throw new InvalidOperationException("Volumetric navigation must be enabled before building a graph.");
        }

        var width = Math.Max(1, (int)MathF.Ceiling(profile.Size.X / profile.CellSize));
        var height = Math.Max(1, (int)MathF.Ceiling(profile.Size.Y / profile.CellSize));
        var depth = Math.Max(1, (int)MathF.Ceiling(profile.Size.Z / profile.CellSize));
        var count = (long)width * height * depth;
        if (maximumCells <= 0 || count > maximumCells)
        {
            throw new InvalidOperationException($"Volumetric navigation grid requires {count} cells; budget is {maximumCells}.");
        }

        var minimum = new Vector3Value(profile.Center.X - profile.Size.X / 2,
            profile.Center.Y - profile.Size.Y / 2, profile.Center.Z - profile.Size.Z / 2);
        var blocked = new HashSet<VolumetricCell>();
        var costs = new Dictionary<VolumetricCell, float>();
        for (var z = 0; z < depth; z++)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var cell = new VolumetricCell(x, y, z);
                    var point = new Vector3Value(minimum.X + ((x + .5f) * profile.CellSize),
                        minimum.Y + ((y + .5f) * profile.CellSize),
                        minimum.Z + ((z + .5f) * profile.CellSize));
                    if (profile.Obstacles.Any(volume => Contains(volume, point, profile.AvoidanceRadius)))
                    {
                        blocked.Add(cell);
                        continue;
                    }

                    var multiplier = profile.CostVolumes.Where(volume => Contains(volume, point, 0))
                        .Select(volume => volume.CostMultiplier).DefaultIfEmpty(1).Max();
                    if (MathF.Abs(multiplier - 1) > .0001f)
                    {
                        costs[cell] = multiplier;
                    }
                }
            }
        }

        return new VolumetricNavigationGraph(minimum, profile.CellSize, width, height, depth, blocked, costs);
    }

    private static bool Contains(NavigationVolumeDefinition volume, Vector3Value point, float padding) =>
        MathF.Abs(point.X - volume.Center.X) <= volume.Size.X / 2 + padding &&
        MathF.Abs(point.Y - volume.Center.Y) <= volume.Size.Y / 2 + padding &&
        MathF.Abs(point.Z - volume.Center.Z) <= volume.Size.Z / 2 + padding;
}
