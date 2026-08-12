using Forge.Application.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.WorldFeatures;

public sealed class EncounterStatisticsScriptService
{
    public sealed record Handle(
        int SchemaVersion,
        long Revision,
        Guid EncounterId,
        string Provenance,
        int Runs,
        int BaseSeed,
        string DurationEstimatorId,
        double SecondsPerActor,
        string RewardEstimatorId,
        double RewardPerCost,
        IReadOnlyList<double> Percentiles,
        bool IncludeRunDetails,
        bool RequiredAtRuntime,
        IReadOnlyDictionary<string, string> Overrides);

    public Handle Get(ProjectState state, Guid encounterId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var encounter = state.Project.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == encounterId)
            ?? throw new KeyNotFoundException($"Encounter world profile '{encounterId:D}' was not found.");
        var resolution = EncounterStatisticsProfileValidator.Resolve(encounter);
        var profile = resolution.Profile ?? throw new InvalidOperationException(
            resolution.Diagnostics.FirstOrDefault()?.Code ?? "WORLD_ENCOUNTER_STATISTICS_MISSING");
        if (!resolution.Available)
            throw new InvalidOperationException(string.Join('|', resolution.Diagnostics.Select(item => item.Code)));
        return new(profile.SchemaVersion, state.Revision.Value, encounter.Id, profile.Provenance, profile.Runs,
            profile.BaseSeed, profile.DurationEstimatorId, profile.SecondsPerActor, profile.RewardEstimatorId,
            profile.RewardPerCost, profile.Percentiles, profile.IncludeRunDetails,
            encounter.StatisticsRequiredAtRuntime, profile.Overrides);
    }
}
