namespace Forge.Domain.Assistant;

public sealed record AssistedAreaEnvironmentIntent
{
    public const int CurrentSchemaVersion = 2;
    public const float MinimumRequiredVegetationDensityPerHectare = 6f;

    public static AssistedAreaEnvironmentIntent Default { get; } = new(
        CurrentSchemaVersion,
        AssistedAreaVegetationIntent.PreserveExisting,
        0,
        64,
        .35f,
        "migration-default",
        0);

    public int SchemaVersion { get; }
    public AssistedAreaVegetationIntent Vegetation { get; }
    public int MinimumNearbyInstances { get; }
    public float EvaluationRadiusMeters { get; }
    public float MaximumStructureSurfaceVariationMeters { get; }
    public string Provenance { get; }
    public float MinimumVegetationDensityPerHectare { get; }

    public AssistedAreaEnvironmentIntent(
        int schemaVersion,
        AssistedAreaVegetationIntent vegetation,
        int minimumNearbyInstances,
        float evaluationRadiusMeters,
        float maximumStructureSurfaceVariationMeters,
        string provenance,
        float minimumVegetationDensityPerHectare = 0)
    {
        SchemaVersion = schemaVersion;
        Vegetation = vegetation;
        MinimumNearbyInstances = minimumNearbyInstances;
        EvaluationRadiusMeters = evaluationRadiusMeters;
        MaximumStructureSurfaceVariationMeters = maximumStructureSurfaceVariationMeters;
        Provenance = (provenance ?? string.Empty).Trim();
        MinimumVegetationDensityPerHectare = minimumVegetationDensityPerHectare;
    }
}
