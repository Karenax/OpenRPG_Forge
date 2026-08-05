using Forge.Domain.Spatial;

namespace Forge.Domain.Areas;

public sealed record TerrainDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public TerrainProviderKind Provider { get; }
    public int Width { get; }
    public int Depth { get; }
    public int SamplesX { get; }
    public int SamplesZ { get; }
    public int ChunkSize { get; }
    public int Seed { get; }
    public float HeightScale { get; }
    public IReadOnlyList<float> Heights { get; }
    public IReadOnlyList<TerrainMaterialLayer> MaterialLayers { get; }
    public IReadOnlyList<TerrainHoleDefinition> Holes { get; }
    public string HeightmapResource { get; }
    public string VoxelResource { get; }
    public string MeshResource { get; }
    public string Provenance { get; }

    public bool Enabled => Provider != TerrainProviderKind.None;

    public TerrainDefinition(
        Guid id,
        int schemaVersion,
        TerrainProviderKind provider,
        int width,
        int depth,
        int samplesX,
        int samplesZ,
        int chunkSize,
        int seed,
        float heightScale,
        IReadOnlyList<float> heights,
        IReadOnlyList<TerrainMaterialLayer> materialLayers,
        IReadOnlyList<TerrainHoleDefinition> holes,
        string heightmapResource,
        string voxelResource,
        string meshResource,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(heights);
        ArgumentNullException.ThrowIfNull(materialLayers);
        ArgumentNullException.ThrowIfNull(holes);
        ArgumentNullException.ThrowIfNull(heightmapResource);
        ArgumentNullException.ThrowIfNull(voxelResource);
        ArgumentNullException.ThrowIfNull(meshResource);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            !Enum.IsDefined(provider) || width is < AreaDimensions.MinimumSize or > AreaDimensions.MaximumSize ||
            depth is < AreaDimensions.MinimumSize or > AreaDimensions.MaximumSize ||
            samplesX is < 2 or > 257 || samplesZ is < 2 or > 257 || chunkSize is < 2 or > 256 ||
            !float.IsFinite(heightScale) || heightScale is < 0 or > 512)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Terrain dimensions, provider or schema are invalid.");
        }

        var heightValues = heights.ToArray();
        if (heightValues.Length != samplesX * samplesZ || heightValues.Any(value => !float.IsFinite(value) || value is < -1f or > 1f))
        {
            throw new ArgumentException("Terrain heights must contain one normalized finite value per sample.", nameof(heights));
        }

        var layers = materialLayers.OrderBy(layer => layer.Name, StringComparer.Ordinal).ToArray();
        if (layers.Select(layer => layer.Id).Distinct().Count() != layers.Length ||
            layers.Select(layer => layer.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != layers.Length ||
            provider != TerrainProviderKind.None && layers.Length == 0)
        {
            throw new ArgumentException("Enabled terrain requires unique material layers.", nameof(materialLayers));
        }

        var holeValues = holes.OrderBy(hole => hole.Id).ToArray();
        if (holeValues.Select(hole => hole.Id).Distinct().Count() != holeValues.Length ||
            holeValues.Any(hole => hole.StartX + hole.Width > samplesX - 1 || hole.StartZ + hole.Depth > samplesZ - 1))
        {
            throw new ArgumentException("Terrain holes require unique IDs and must remain inside the sample grid.", nameof(holes));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Provider = provider;
        Width = width;
        Depth = depth;
        SamplesX = samplesX;
        SamplesZ = samplesZ;
        ChunkSize = chunkSize;
        Seed = seed;
        HeightScale = heightScale;
        Heights = heightValues;
        MaterialLayers = layers;
        Holes = holeValues;
        HeightmapResource = WeatherStateProfile.NormalizeOptionalResource(heightmapResource);
        VoxelResource = WeatherStateProfile.NormalizeOptionalResource(voxelResource);
        MeshResource = WeatherStateProfile.NormalizeOptionalResource(meshResource);
        Provenance = provenance.Trim();
    }

    public float SampleHeight(int x, int z)
    {
        if (x < 0 || x >= SamplesX || z < 0 || z >= SamplesZ)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        return Heights[(z * SamplesX) + x] * HeightScale;
    }

    public float HeightAt(float worldX, float worldZ)
    {
        if (!float.IsFinite(worldX) || !float.IsFinite(worldZ))
        {
            throw new ArgumentOutOfRangeException(nameof(worldX));
        }

        var normalizedX = Math.Clamp((worldX + (Width / 2f)) / Width, 0, 1) * (SamplesX - 1);
        var normalizedZ = Math.Clamp((worldZ + (Depth / 2f)) / Depth, 0, 1) * (SamplesZ - 1);
        var x0 = (int)MathF.Floor(normalizedX);
        var z0 = (int)MathF.Floor(normalizedZ);
        var x1 = Math.Min(x0 + 1, SamplesX - 1);
        var z1 = Math.Min(z0 + 1, SamplesZ - 1);
        var tx = normalizedX - x0;
        var tz = normalizedZ - z0;
        var first = Lerp(SampleHeight(x0, z0), SampleHeight(x1, z0), tx);
        var second = Lerp(SampleHeight(x0, z1), SampleHeight(x1, z1), tx);
        return Lerp(first, second, tz);
    }

    public bool IsHoleCell(int x, int z) => Holes.Any(hole =>
        x >= hole.StartX && x < hole.StartX + hole.Width && z >= hole.StartZ && z < hole.StartZ + hole.Depth);

    private static float Lerp(float start, float end, float amount) => start + ((end - start) * amount);
}
