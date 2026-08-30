namespace Forge.Domain.Areas;

public sealed record AreaEnvironmentOccupancyProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public int Width { get; }
    public int Depth { get; }
    public float CellSizeMeters { get; }
    public IReadOnlyList<AreaEnvironmentOccupancyCell> Cells { get; }
    public string SourceFingerprint { get; }
    public string Provenance { get; }

    public AreaEnvironmentOccupancyProfile(
        Guid id,
        int schemaVersion,
        int width,
        int depth,
        float cellSizeMeters,
        IReadOnlyList<AreaEnvironmentOccupancyCell> cells,
        string sourceFingerprint,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            width is < AreaDimensions.MinimumSize or > AreaDimensions.MaximumSize ||
            depth is < AreaDimensions.MinimumSize or > AreaDimensions.MaximumSize ||
            !float.IsFinite(cellSizeMeters) || cellSizeMeters is < 1 or > 512 || cells.Count > 65_536)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Environment-occupancy dimensions, sampling or schema are invalid.");
        }
        if (!IsSha256(sourceFingerprint))
            throw new ArgumentException("Environment-occupancy source fingerprint must be a lowercase SHA-256 value.",
                nameof(sourceFingerprint));

        var halfWidth = width / 2f;
        var halfDepth = depth / 2f;
        var values = cells.OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Role, StringComparer.Ordinal).ThenBy(item => item.Z).ThenBy(item => item.X)
            .ToArray();
        if (values.Any(item => !IsToken(item.Category) || !IsToken(item.Role) ||
            !float.IsFinite(item.X) || !float.IsFinite(item.Z) ||
            item.X < -halfWidth || item.X > halfWidth || item.Z < -halfDepth || item.Z > halfDepth ||
            item.Count is < 1 or > 1_000_000))
        {
            throw new ArgumentException("Environment-occupancy cells must be finite, bounded and categorized.",
                nameof(cells));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Width = width;
        Depth = depth;
        CellSizeMeters = cellSizeMeters;
        Cells = values;
        SourceFingerprint = sourceFingerprint;
        Provenance = provenance.Trim();
    }

    public int CountNear(string category, float x, float z, float radiusMeters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        if (!float.IsFinite(x) || !float.IsFinite(z) || !float.IsFinite(radiusMeters) || radiusMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(radiusMeters));
        var normalized = category.Trim().ToLowerInvariant();
        var squaredRadius = radiusMeters * radiusMeters;
        return Cells.Where(item => string.Equals(item.Category, normalized, StringComparison.Ordinal) &&
                ((item.X - x) * (item.X - x)) + ((item.Z - z) * (item.Z - z)) <= squaredRadius)
            .Sum(item => item.Count);
    }

    public float DensityPerHectare(string category, float x, float z, float radiusMeters)
    {
        if (!float.IsFinite(radiusMeters) || radiusMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(radiusMeters));
        var areaSquareMeters = MathF.PI * radiusMeters * radiusMeters;
        return CountNear(category, x, z, radiusMeters) * 10_000f / areaSquareMeters;
    }

    public int CountIntersectingFootprint(
        string category,
        float centerX,
        float centerZ,
        float width,
        float depth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        if (!float.IsFinite(centerX) || !float.IsFinite(centerZ) ||
            !float.IsFinite(width) || !float.IsFinite(depth) ||
            width is < 1 or > 512 || depth is < 1 or > 512)
            throw new ArgumentOutOfRangeException(nameof(width));
        var normalized = category.Trim().ToLowerInvariant();
        var maximumXDistance = (width / 2f) + (CellSizeMeters / 2f);
        var maximumZDistance = (depth / 2f) + (CellSizeMeters / 2f);
        return Cells.Where(item => string.Equals(item.Category, normalized, StringComparison.Ordinal) &&
                MathF.Abs(item.X - centerX) <= maximumXDistance &&
                MathF.Abs(item.Z - centerZ) <= maximumZDistance)
            .Sum(item => item.Count);
    }

    private static bool IsToken(string value) => value.Length is >= 2 and <= 80 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');

    private static bool IsSha256(string value) => value.Length == 64 && value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
