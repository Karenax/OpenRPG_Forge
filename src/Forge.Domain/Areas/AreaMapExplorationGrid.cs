using System.Globalization;
using Forge.Domain.Validation;

namespace Forge.Domain.Areas;

public readonly record struct AreaMapCell(int Floor, int X, int Z)
{
    public const string CurrentKeyVersion = "v1";
    public const string LegacyKeyVersion = "v0";

    public string Key => string.Create(CultureInfo.InvariantCulture, $"{CurrentKeyVersion}:{Floor}:{X}:{Z}");

    public string LegacyKey =>
        string.Create(CultureInfo.InvariantCulture, $"{LegacyKeyVersion}:{Floor}:{X}:{Z}");

    public static bool TryParse(string? value, out AreaMapCell cell, out bool legacy)
    {
        cell = default;
        legacy = false;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var segments = value.Split(':', StringSplitOptions.None);
        if (segments.Length == 4 &&
            string.Equals(segments[0], CurrentKeyVersion, StringComparison.Ordinal) &&
            TryInt(segments[1], out var floor) &&
            TryInt(segments[2], out var x) &&
            TryInt(segments[3], out var z))
        {
            var parsed = new AreaMapCell(floor, x, z);
            if (!string.Equals(value, parsed.Key, StringComparison.Ordinal)) return false;
            cell = parsed;
            return true;
        }

        if (segments.Length == 4 &&
            string.Equals(segments[0], LegacyKeyVersion, StringComparison.Ordinal) &&
            TryInt(segments[1], out floor) &&
            TryInt(segments[2], out x) &&
            TryInt(segments[3], out z))
        {
            var parsed = new AreaMapCell(floor, x, z);
            if (!string.Equals(value, parsed.LegacyKey, StringComparison.Ordinal)) return false;
            cell = parsed;
            legacy = true;
            return true;
        }

        if (segments.Length == 2 &&
            TryInt(segments[0], out x) &&
            TryInt(segments[1], out z))
        {
            var parsed = new AreaMapCell(0, x, z);
            var canonicalLegacy = string.Create(CultureInfo.InvariantCulture, $"{x}:{z}");
            if (!string.Equals(value, canonicalLegacy, StringComparison.Ordinal)) return false;
            cell = parsed;
            legacy = true;
            return true;
        }

        return false;
    }

    private static bool TryInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
}

public sealed record AreaMapCellValidationResult(
    IReadOnlySet<string> ValidCells,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public sealed class AreaMapExplorationGrid
{
    public const string CellKeyInvalidCode = "area.map.exploration_cell_key_invalid";
    public const string CellOutOfScopeCode = "area.map.exploration_cell_out_of_scope";
    public const string LegacyCellKeyMigratedCode = "area.map.exploration_cell_key_migrated";

    private readonly float _minimumX;
    private readonly float _maximumX;
    private readonly float _minimumZ;
    private readonly float _maximumZ;

    public AreaMapExplorationGrid(AreaDimensions dimensions, int floorCount, float cellSize)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        if (floorCount < 1) throw new ArgumentOutOfRangeException(nameof(floorCount));
        if (!float.IsFinite(cellSize) || cellSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellSize));

        FloorCount = floorCount;
        CellSize = cellSize;
        CellColumns = Math.Max(1, (int)MathF.Ceiling(dimensions.Width / cellSize));
        CellRows = Math.Max(1, (int)MathF.Ceiling(dimensions.Depth / cellSize));
        TotalCells = checked(CellColumns * CellRows * floorCount);
        _minimumX = dimensions.Width / -2f;
        _maximumX = dimensions.Width / 2f;
        _minimumZ = dimensions.Depth / -2f;
        _maximumZ = dimensions.Depth / 2f;
    }

    public int FloorCount { get; }

    public float CellSize { get; }

    public int CellColumns { get; }

    public int CellRows { get; }

    public int TotalCells { get; }

    public AreaMapCell Resolve(int floor, float x, float z)
    {
        if (!TryResolve(floor, x, z, out var cell))
            throw new ArgumentOutOfRangeException(nameof(x),
                "Map exploration coordinates or floor are outside the centered area grid.");
        return cell;
    }

    public bool TryResolve(int floor, float x, float z, out AreaMapCell cell)
    {
        cell = default;
        if (floor < 0 || floor >= FloorCount || !float.IsFinite(x) || !float.IsFinite(z) ||
            x < _minimumX || x > _maximumX || z < _minimumZ || z > _maximumZ)
            return false;

        var cellX = Math.Min(CellColumns - 1, (int)MathF.Floor((x - _minimumX) / CellSize));
        var cellZ = Math.Min(CellRows - 1, (int)MathF.Floor((z - _minimumZ) / CellSize));
        cell = new AreaMapCell(floor, cellX, cellZ);
        return true;
    }

    public AreaMapCellValidationResult ValidateKeys(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var valid = new HashSet<string>(StringComparer.Ordinal);
        var diagnostics = new List<DomainDiagnostic>();
        var index = 0;
        foreach (var key in keys.Order(StringComparer.Ordinal))
        {
            var path = $"revealedCells/{index}";
            if (!AreaMapCell.TryParse(key, out var cell, out var legacy))
            {
                diagnostics.Add(new DomainDiagnostic(
                    CellKeyInvalidCode,
                    $"Map exploration cell key '{key}' is malformed.",
                    path));
                index++;
                continue;
            }

            if (legacy)
            {
                if (!TryResolveLegacy(cell, out var migrated))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        CellOutOfScopeCode,
                        $"Legacy map exploration cell '{key}' is outside the active area grid.",
                        path));
                    index++;
                    continue;
                }

                valid.Add(migrated.Key);
                diagnostics.Add(new DomainDiagnostic(
                    LegacyCellKeyMigratedCode,
                    $"Legacy map exploration cell key '{key}' migrated to '{migrated.Key}'.",
                    path));
                index++;
                continue;
            }

            if (!Contains(cell))
            {
                diagnostics.Add(new DomainDiagnostic(
                    CellOutOfScopeCode,
                    $"Map exploration cell '{cell.Key}' is outside the active area grid.",
                    path));
                index++;
                continue;
            }
            valid.Add(cell.Key);
            index++;
        }

        return new AreaMapCellValidationResult(valid, diagnostics);
    }

    private bool Contains(AreaMapCell cell) =>
        cell.Floor >= 0 && cell.Floor < FloorCount &&
        cell.X >= 0 && cell.X < CellColumns &&
        cell.Z >= 0 && cell.Z < CellRows;

    private bool TryResolveLegacy(AreaMapCell legacy, out AreaMapCell cell)
    {
        cell = default;
        if (legacy.Floor < 0 || legacy.Floor >= FloorCount) return false;

        var legacyMinimumX = legacy.X * (double)CellSize;
        var legacyMaximumX = (legacy.X + 1d) * CellSize;
        var legacyMinimumZ = legacy.Z * (double)CellSize;
        var legacyMaximumZ = (legacy.Z + 1d) * CellSize;
        var overlapMinimumX = Math.Max(_minimumX, legacyMinimumX);
        var overlapMaximumX = Math.Min(_maximumX, legacyMaximumX);
        var overlapMinimumZ = Math.Max(_minimumZ, legacyMinimumZ);
        var overlapMaximumZ = Math.Min(_maximumZ, legacyMaximumZ);
        if (overlapMinimumX > overlapMaximumX || overlapMinimumZ > overlapMaximumZ) return false;

        var representativeX = (float)((overlapMinimumX + overlapMaximumX) / 2d);
        var representativeZ = (float)((overlapMinimumZ + overlapMaximumZ) / 2d);
        return TryResolve(legacy.Floor, representativeX, representativeZ, out cell);
    }
}
