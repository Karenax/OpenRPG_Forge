namespace Forge.Domain.WorldFeatures;

public sealed record EncounterStatisticsProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    int Runs,
    int BaseSeed,
    string DurationEstimatorId,
    double SecondsPerActor,
    string RewardEstimatorId,
    double RewardPerCost,
    IReadOnlyList<double> Percentiles,
    bool IncludeRunDetails,
    IReadOnlyDictionary<string, string> Overrides)
{
    public const int CurrentSchemaVersion = 1;
}
