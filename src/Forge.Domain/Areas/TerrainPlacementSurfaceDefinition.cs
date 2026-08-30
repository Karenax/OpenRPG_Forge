namespace Forge.Domain.Areas;

public sealed record TerrainPlacementSurfaceDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public int Width { get; }
    public int Depth { get; }
    public int SamplesX { get; }
    public int SamplesZ { get; }
    public float HeightScale { get; }
    public IReadOnlyList<float> Heights { get; }
    public TerrainCellDiagonal CellDiagonal { get; }
    public string SourceFingerprint { get; }
    public string Provenance { get; }

    public TerrainPlacementSurfaceDefinition(
        Guid id,
        int schemaVersion,
        int width,
        int depth,
        int samplesX,
        int samplesZ,
        float heightScale,
        IReadOnlyList<float> heights,
        TerrainCellDiagonal cellDiagonal,
        string sourceFingerprint,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(heights);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            width is < AreaDimensions.MinimumSize or > AreaDimensions.MaximumSize ||
            depth is < AreaDimensions.MinimumSize or > AreaDimensions.MaximumSize ||
            samplesX is < 2 or > 257 || samplesZ is < 2 or > 257 ||
            !float.IsFinite(heightScale) || heightScale is < 0 or > 8192 ||
            !Enum.IsDefined(cellDiagonal))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Placement-surface dimensions, sampling or schema are invalid.");
        }

        var values = heights.ToArray();
        if (values.Length != samplesX * samplesZ ||
            values.Any(value => !float.IsFinite(value) || value is < -1f or > 1f))
        {
            throw new ArgumentException(
                "Placement-surface heights must contain one normalized finite value per sample.", nameof(heights));
        }
        if (!IsSha256(sourceFingerprint))
            throw new ArgumentException("Placement-surface source fingerprint must be a lowercase SHA-256 value.",
                nameof(sourceFingerprint));

        Id = id;
        SchemaVersion = schemaVersion;
        Width = width;
        Depth = depth;
        SamplesX = samplesX;
        SamplesZ = samplesZ;
        HeightScale = heightScale;
        Heights = values;
        CellDiagonal = cellDiagonal;
        SourceFingerprint = sourceFingerprint;
        Provenance = provenance.Trim();
    }

    public float SampleHeight(int x, int z)
    {
        if (x < 0 || x >= SamplesX || z < 0 || z >= SamplesZ)
            throw new ArgumentOutOfRangeException(nameof(x));
        return Heights[(z * SamplesX) + x] * HeightScale;
    }

    public float HeightAt(float worldX, float worldZ)
    {
        if (!float.IsFinite(worldX) || !float.IsFinite(worldZ))
            throw new ArgumentOutOfRangeException(nameof(worldX));

        var sampleX = Math.Clamp((worldX + (Width / 2f)) / Width, 0, 1) * (SamplesX - 1);
        var sampleZ = Math.Clamp((worldZ + (Depth / 2f)) / Depth, 0, 1) * (SamplesZ - 1);
        var x0 = (int)MathF.Floor(sampleX);
        var z0 = (int)MathF.Floor(sampleZ);
        var x1 = Math.Min(x0 + 1, SamplesX - 1);
        var z1 = Math.Min(z0 + 1, SamplesZ - 1);
        var tx = sampleX - x0;
        var tz = sampleZ - z0;
        var h00 = SampleHeight(x0, z0);
        var h10 = SampleHeight(x1, z0);
        var h01 = SampleHeight(x0, z1);
        var h11 = SampleHeight(x1, z1);

        if (CellDiagonal == TerrainCellDiagonal.Main)
        {
            return tx >= tz
                ? (h00 * (1 - tx)) + (h10 * (tx - tz)) + (h11 * tz)
                : (h00 * (1 - tz)) + (h11 * tx) + (h01 * (tz - tx));
        }

        return tx + tz <= 1
            ? (h00 * (1 - tx - tz)) + (h10 * tx) + (h01 * tz)
            : (h10 * (1 - tz)) + (h01 * (1 - tx)) + (h11 * (tx + tz - 1));
    }

    private static bool IsSha256(string value) => value.Length == 64 && value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
