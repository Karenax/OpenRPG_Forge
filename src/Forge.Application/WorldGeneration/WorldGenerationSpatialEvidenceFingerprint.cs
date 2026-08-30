using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;

namespace Forge.Application.WorldGeneration;

public static class WorldGenerationSpatialEvidenceFingerprint
{
    public static string ComputeTerrain(
        int width,
        int depth,
        int samplesX,
        int samplesZ,
        float heightScale,
        IReadOnlyList<float> heights,
        TerrainCellDiagonal cellDiagonal)
    {
        ArgumentNullException.ThrowIfNull(heights);
        var value = new StringBuilder("openrpg-placement-surface-v2\n");
        value.Append(CultureInfo.InvariantCulture,
            $"width={width};depth={depth};samplesX={samplesX};samplesZ={samplesZ};heightScaleMicrounits={Microunits(heightScale)};diagonal={cellDiagonal.ToString().ToLowerInvariant()}\n");
        value.Append(CultureInfo.InvariantCulture, $"heights={heights.Count}\n");
        foreach (var height in heights)
            value.Append(CultureInfo.InvariantCulture, $"{Microunits(height)}\n");
        return Sha256(value.ToString());
    }

    public static string ComputeEnvironment(
        int width,
        int depth,
        float cellSizeMeters,
        IReadOnlyList<AreaEnvironmentOccupancyCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var ordered = cells.OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Role, StringComparer.Ordinal).ThenBy(item => item.Z).ThenBy(item => item.X)
            .ToArray();
        var value = new StringBuilder("openrpg-environment-occupancy-v2\n");
        value.Append(CultureInfo.InvariantCulture,
            $"width={width};depth={depth};cellSizeMicrounits={Microunits(cellSizeMeters)}\n");
        value.Append(CultureInfo.InvariantCulture, $"cells={ordered.Length}\n");
        foreach (var cell in ordered)
            value.Append(CultureInfo.InvariantCulture,
                $"{cell.Category}|{cell.Role}|{Microunits(cell.X)}|{Microunits(cell.Z)}|{cell.Count}\n");
        return Sha256(value.ToString());
    }

    private static long Microunits(float value) => checked((long)Math.Round(
        (double)value * 1_000_000d, MidpointRounding.AwayFromZero));

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
