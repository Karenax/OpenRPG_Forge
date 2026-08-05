namespace Forge.Domain.Areas;

public sealed record BiomeLayerDefinition
{
    public Guid Id { get; }
    public string Name { get; }
    public Guid MaterialLayerId { get; }
    public float MinimumAltitude { get; }
    public float MaximumAltitude { get; }
    public float MinimumSlopeDegrees { get; }
    public float MaximumSlopeDegrees { get; }
    public float MinimumMoisture { get; }
    public float MaximumMoisture { get; }
    public float ScatterDensity { get; }
    public string ScatterResource { get; }
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<string> ExclusionTags { get; }
    public int Priority { get; }
    public string Provenance { get; }

    public BiomeLayerDefinition(Guid id, string name, Guid materialLayerId, float minimumAltitude,
        float maximumAltitude, float minimumSlopeDegrees, float maximumSlopeDegrees, float minimumMoisture,
        float maximumMoisture, float scatterDensity, string scatterResource, IReadOnlyList<string> tags,
        IReadOnlyList<string> exclusionTags, int priority, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(scatterResource);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(exclusionTags);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || materialLayerId == Guid.Empty || !float.IsFinite(minimumAltitude) ||
            !float.IsFinite(maximumAltitude) || maximumAltitude < minimumAltitude ||
            minimumSlopeDegrees is < 0 or > 90 || maximumSlopeDegrees < minimumSlopeDegrees || maximumSlopeDegrees > 90 ||
            minimumMoisture is < 0 or > 1 || maximumMoisture < minimumMoisture || maximumMoisture > 1 ||
            scatterDensity is < 0 or > 1 || priority is < -1000 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Biome layer ranges are invalid.");
        }

        Id = id;
        Name = name.Trim();
        MaterialLayerId = materialLayerId;
        MinimumAltitude = minimumAltitude;
        MaximumAltitude = maximumAltitude;
        MinimumSlopeDegrees = minimumSlopeDegrees;
        MaximumSlopeDegrees = maximumSlopeDegrees;
        MinimumMoisture = minimumMoisture;
        MaximumMoisture = maximumMoisture;
        ScatterDensity = scatterDensity;
        ScatterResource = WeatherStateProfile.NormalizeOptionalResource(scatterResource);
        Tags = NormalizeTags(tags);
        ExclusionTags = NormalizeTags(exclusionTags);
        Priority = priority;
        Provenance = provenance.Trim();
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim().ToLowerInvariant())
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();
}
