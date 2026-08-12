using Forge.Domain.Validation;

namespace Forge.Domain.WorldFeatures;

public sealed record EncounterStatisticsResolution(
    bool Available,
    bool OptionalFallback,
    EncounterStatisticsProfile? Profile,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class EncounterStatisticsProfileValidator
{
    public static IReadOnlyList<DomainDiagnostic> Validate(EncounterStatisticsProfile profile, string path)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != EncounterStatisticsProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            Add(diagnostics, "world.encounter.statistics_identity_invalid",
                "Encounter statistics require UUID, schema v1 and provenance.", path);
        if (profile.Runs is < 1 or > 100_000 || string.IsNullOrWhiteSpace(profile.DurationEstimatorId) ||
            !double.IsFinite(profile.SecondsPerActor) || profile.SecondsPerActor <= 0 ||
            string.IsNullOrWhiteSpace(profile.RewardEstimatorId) || !double.IsFinite(profile.RewardPerCost) ||
            profile.RewardPerCost < 0)
            Add(diagnostics, "world.encounter.statistics_configuration_invalid",
                "Statistics require 1–100000 runs and finite duration/reward estimator coefficients.", path);
        if (profile.Percentiles.Count == 0 || profile.Percentiles.Count > 10 ||
            profile.Percentiles.Any(item => !double.IsFinite(item) || item <= 0 || item >= 1) ||
            profile.Percentiles.Distinct().Count() != profile.Percentiles.Count ||
            !profile.Percentiles.SequenceEqual(profile.Percentiles.OrderBy(item => item)))
            Add(diagnostics, "world.encounter.statistics_percentiles_invalid",
                "Statistics percentiles must be unique, ascending and strictly between zero and one.",
                $"{path}/percentiles");
        if (profile.Overrides.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
            Add(diagnostics, "world.encounter.statistics_override_invalid",
                "Statistics overrides require non-empty keys and values.", $"{path}/overrides");
        return diagnostics;
    }

    public static EncounterStatisticsResolution Resolve(EncounterWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var path = $"module/worldFeatures/encounters/{profile.Id}/statistics";
        if (profile.Statistics is null)
        {
            var missing = profile.StatisticsRequiredAtRuntime
                ? new[] { new DomainDiagnostic("world.encounter.statistics_required",
                    "Encounter statistics are required by this runtime profile.", path) }
                : [];
            return new(false, !profile.StatisticsRequiredAtRuntime, null, missing);
        }
        var diagnostics = Validate(profile.Statistics, path);
        return new(diagnostics.Count == 0, false, profile.Statistics, diagnostics);
    }

    private static void Add(List<DomainDiagnostic> diagnostics, string code, string message, string path) =>
        diagnostics.Add(new(code, message, path));
}
