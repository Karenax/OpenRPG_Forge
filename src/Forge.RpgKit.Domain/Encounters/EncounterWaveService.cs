namespace Forge.RpgKit.Domain.Encounters;

public enum WaveCompletionPolicy { AllDefeated, ObjectiveCompleted, TimerElapsed, Script }
public sealed record EncounterWave(string Id, int Order, IReadOnlyList<EncounterGroup> Groups, TimeSpan Delay, WaveCompletionPolicy Completion, string? CompletionTargetId);
public sealed record EncounterPhase(string Id, int Order, IReadOnlyList<EncounterWave> Waves, IReadOnlySet<string> EntryConditions, IReadOnlyList<string> ReinforcementEncounterIds);
public sealed record EncounterEntered(Guid EventId, Guid EncounterId, string ActivatorId, DateTimeOffset At, string Provenance);
public sealed record EncounterExited(Guid EventId, Guid EncounterId, string ActivatorId, DateTimeOffset At, TimeSpan DurationInside, string Provenance);
public sealed record EncounterExhausted(Guid EventId, Guid EncounterId, DateTimeOffset At, int SpawnedCount, string Provenance);
public sealed record EncounterRunResult(int Seed, int SpawnedCount, double Cost, double DurationSeconds, double RewardValue, IReadOnlyDictionary<string, int> Composition);
public sealed record EncounterStatistics(int Runs, double MeanCount, double MeanCost, double MeanDurationSeconds, double MeanRewardValue, double CostVariance, IReadOnlyDictionary<string, int> CompositionTotals, IReadOnlyList<EncounterRunResult> Results);

public sealed class EncounterLifecycle
{
    private readonly Dictionary<string, DateTimeOffset> _inside = new(StringComparer.Ordinal);
    public EncounterEntered Enter(EncounterDefinition encounter, string activatorId, DateTimeOffset at)
    {
        _inside[activatorId] = at;
        return new(Guid.NewGuid(), encounter.Id, activatorId, at, encounter.Provenance);
    }
    public EncounterExited? Exit(EncounterDefinition encounter, string activatorId, DateTimeOffset at)
    {
        if (!_inside.Remove(activatorId, out var enteredAt)) return null;
        return new(Guid.NewGuid(), encounter.Id, activatorId, at, at - enteredAt, encounter.Provenance);
    }
    public static EncounterExhausted Exhaust(EncounterDefinition encounter, DateTimeOffset at, int spawnedCount) => new(Guid.NewGuid(), encounter.Id, at, spawnedCount, encounter.Provenance);
}

public static class EncounterWaveService
{
    public static IReadOnlyList<EncounterDiagnostic> Validate(EncounterDefinition encounter, IReadOnlyList<EncounterPhase> phases)
    {
        var root = $"encounters/{encounter.Id}/phases"; var diagnostics = new List<EncounterDiagnostic>();
        if (phases.Count == 0 || phases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != phases.Count || phases.Select(item => item.Order).Distinct().Count() != phases.Count) diagnostics.Add(new("RPG_ENCOUNTER_PHASE_INVALID", root, "Phases require unique identities and ordering."));
        foreach (var phase in phases)
        {
            if (phase.Waves.Count == 0 || phase.Waves.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != phase.Waves.Count || phase.Waves.Select(item => item.Order).Distinct().Count() != phase.Waves.Count || phase.Waves.Any(item => item.Delay < TimeSpan.Zero || (item.Completion == WaveCompletionPolicy.Script && string.IsNullOrWhiteSpace(item.CompletionTargetId))))
                diagnostics.Add(new("RPG_ENCOUNTER_WAVE_INVALID", $"{root}/{phase.Id}/waves", "Waves require unique order, groups, timing and completion target."));
        }
        return diagnostics;
    }

    public static EncounterStatistics Simulate(EncounterDefinition encounter, EncounterRuntimePolicy policy, PartyAssessment party, int runs, int baseSeed, Func<EncounterComposition, double> duration, Func<EncounterComposition, double> rewards)
    {
        if (runs <= 0 || runs > 100_000) throw new ArgumentOutOfRangeException(nameof(runs));
        var results = new List<EncounterRunResult>(runs);
        for (var index = 0; index < runs; index++)
        {
            var seed = checked(baseSeed + index); var composition = EncounterCompositionService.Preview(encounter, policy, party, seed);
            results.Add(new(seed, composition.TotalCount, composition.TotalCost, duration(composition), rewards(composition), composition.Candidates.GroupBy(item => item.BlueprintId).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)));
        }
        var meanCost = results.Average(item => item.Cost);
        return new(runs, results.Average(item => item.SpawnedCount), meanCost, results.Average(item => item.DurationSeconds), results.Average(item => item.RewardValue), results.Average(item => Math.Pow(item.Cost - meanCost, 2)), results.SelectMany(item => item.Composition).GroupBy(item => item.Key).ToDictionary(group => group.Key, group => group.Sum(item => item.Value), StringComparer.Ordinal), results);
    }
}
