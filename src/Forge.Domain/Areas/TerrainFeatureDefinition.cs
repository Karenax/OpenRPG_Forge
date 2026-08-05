using Forge.Domain.Spatial;

namespace Forge.Domain.Areas;

public sealed record TerrainFeatureDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Name { get; }
    public TerrainFeatureKind Kind { get; }
    public IReadOnlyList<Vector3Value> ControlPoints { get; }
    public float Width { get; }
    public float Height { get; }
    public float BankDegrees { get; }
    public TerrainFeatureCrossingPolicy CrossingPolicy { get; }
    public IReadOnlyList<string> CompatibleBiomeTags { get; }
    public string MaterialResource { get; }
    public string Provenance { get; }

    public TerrainFeatureDefinition(Guid id, int schemaVersion, string name, TerrainFeatureKind kind,
        IReadOnlyList<Vector3Value> controlPoints, float width, float height, float bankDegrees,
        TerrainFeatureCrossingPolicy crossingPolicy, IReadOnlyList<string> compatibleBiomeTags,
        string materialResource, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(controlPoints);
        ArgumentNullException.ThrowIfNull(compatibleBiomeTags);
        ArgumentNullException.ThrowIfNull(materialResource);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || controlPoints.Count < 2 ||
            !float.IsFinite(width) || width is <= 0 or > 512 || !float.IsFinite(height) || height is < 0 or > 512 ||
            !float.IsFinite(bankDegrees) || bankDegrees is < -89 or > 89)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Terrain feature geometry is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Name = name.Trim();
        Kind = kind;
        ControlPoints = controlPoints.ToArray();
        Width = width;
        Height = height;
        BankDegrees = bankDegrees;
        CrossingPolicy = crossingPolicy;
        CompatibleBiomeTags = compatibleBiomeTags.Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        MaterialResource = WeatherStateProfile.NormalizeOptionalResource(materialResource);
        Provenance = provenance.Trim();
    }
}
