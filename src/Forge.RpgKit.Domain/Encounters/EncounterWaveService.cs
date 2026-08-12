namespace Forge.RpgKit.Domain.Encounters;

public enum WaveCompletionPolicy { AllDefeated, ObjectiveCompleted, TimerElapsed, Script }
public sealed record EncounterWave(
    string Id,
    int Order,
    IReadOnlyList<EncounterGroup> Groups,
    TimeSpan Delay,
    WaveCompletionPolicy Completion,
    string? CompletionTargetId,
    Guid? QuestId = null,
    string? ObjectiveId = null,
    string? VoiceCueId = null,
    string? NextWaveId = null);
public sealed record EncounterPhase(string Id, int Order, IReadOnlyList<EncounterWave> Waves, IReadOnlySet<string> EntryConditions, IReadOnlyList<string> ReinforcementEncounterIds);
public sealed record EncounterEntered(Guid EventId, Guid EncounterId, string ActivatorId, DateTimeOffset At, string Provenance);
public sealed record EncounterExited(Guid EventId, Guid EncounterId, string ActivatorId, DateTimeOffset At, TimeSpan DurationInside, string Provenance);
public sealed record EncounterExhausted(
    Guid EventId,
    Guid EncounterId,
    DateTimeOffset At,
    int SpawnedCount,
    string Provenance,
    string Cause = "exhausted",
    int Kills = 0,
    int Escaped = 0,
    int Waves = 0,
    IReadOnlyList<string>? RewardIds = null);
public sealed record EncounterRunResult(int Seed, int SpawnedCount, double Cost, double DurationSeconds, double RewardValue, IReadOnlyDictionary<string, int> Composition);
public sealed record EncounterPercentile(double Percentile, int SpawnedCount, double Cost, double DurationSeconds,
    double RewardValue);
public sealed record EncounterStatistics(int Runs, double MeanCount, double MeanCost, double MeanDurationSeconds,
    double MeanRewardValue, double CostVariance, IReadOnlyDictionary<string, int> CompositionTotals,
    IReadOnlyList<EncounterRunResult> Results, double CountVariance = 0, double DurationVariance = 0,
    double RewardVariance = 0, IReadOnlyDictionary<double, EncounterPercentile>? Percentiles = null);

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
    public static EncounterExhausted Exhaust(
        EncounterDefinition encounter,
        DateTimeOffset at,
        int spawnedCount,
        string cause = "exhausted",
        int kills = 0,
        int escaped = 0,
        int waves = 0,
        IReadOnlyList<string>? rewardIds = null) =>
        new(Guid.NewGuid(), encounter.Id, at, spawnedCount, encounter.Provenance, cause, kills, escaped, waves,
            rewardIds ?? encounter.RewardIds);
}

public static class EncounterWaveService
{
    public static IReadOnlyList<EncounterDiagnostic> Validate(EncounterDefinition encounter, IReadOnlyList<EncounterPhase> phases)
    {
        var root = $"encounters/{encounter.Id}/phases"; var diagnostics = new List<EncounterDiagnostic>();
        if (phases.Count == 0 || phases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != phases.Count || phases.Select(item => item.Order).Distinct().Count() != phases.Count) diagnostics.Add(new("RPG_ENCOUNTER_PHASE_INVALID", root, "Phases require unique identities and ordering."));
        var orderedWaves = phases.OrderBy(item => item.Order).SelectMany(item => item.Waves.OrderBy(wave => wave.Order)).ToArray();
        var waveIndexes = orderedWaves.Select((wave, index) => (wave.Id, index)).ToDictionary(item => item.Id, item => item.index, StringComparer.Ordinal);
        foreach (var phase in phases)
        {
            if (phase.Waves.Count == 0 || phase.Waves.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != phase.Waves.Count || phase.Waves.Select(item => item.Order).Distinct().Count() != phase.Waves.Count || phase.Waves.Any(item => item.Delay < TimeSpan.Zero ||
                    item.Completion == WaveCompletionPolicy.Script && string.IsNullOrWhiteSpace(item.CompletionTargetId) ||
                    item.Completion == WaveCompletionPolicy.TimerElapsed && item.Delay <= TimeSpan.Zero ||
                    item.Completion == WaveCompletionPolicy.ObjectiveCompleted &&
                    (item.QuestId is null || string.IsNullOrWhiteSpace(item.ObjectiveId)) ||
                    item.VoiceCueId is not null && !item.VoiceCueId.StartsWith("audio:", StringComparison.Ordinal) ||
                    item.NextWaveId is { } next && (!waveIndexes.ContainsKey(next) || next == item.Id ||
                        waveIndexes[next] <= waveIndexes[item.Id])))
                diagnostics.Add(new("RPG_ENCOUNTER_WAVE_INVALID", $"{root}/{phase.Id}/waves", "Waves require unique order, groups, timing and completion target."));
        }
        return diagnostics;
    }

    public static EncounterStatistics Simulate(EncounterDefinition encounter, EncounterRuntimePolicy policy, PartyAssessment party, int runs, int baseSeed, Func<EncounterComposition, double> duration, Func<EncounterComposition, double> rewards, IReadOnlyList<double>? percentiles = null)
    {
        if (runs <= 0 || runs > 100_000) throw new ArgumentOutOfRangeException(nameof(runs));
        var results = new List<EncounterRunResult>(runs);
        for (var index = 0; index < runs; index++)
        {
            var seed = checked(baseSeed + index); var composition = EncounterCompositionService.Preview(encounter, policy, party, seed);
            results.Add(new(seed, composition.TotalCount, composition.TotalCost, duration(composition), rewards(composition), composition.Candidates.GroupBy(item => item.BlueprintId).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)));
        }
        var meanCount = results.Average(item => item.SpawnedCount);
        var meanCost = results.Average(item => item.Cost);
        var meanDuration = results.Average(item => item.DurationSeconds);
        var meanReward = results.Average(item => item.RewardValue);
        var requestedPercentiles = percentiles ?? [];
        if (requestedPercentiles.Any(item => !double.IsFinite(item) || item <= 0 || item >= 1) ||
            requestedPercentiles.Distinct().Count() != requestedPercentiles.Count)
            throw new ArgumentOutOfRangeException(nameof(percentiles));
        var distribution = requestedPercentiles.OrderBy(item => item).ToDictionary(item => item,
            item => new EncounterPercentile(item,
                Quantile(results.Select(run => run.SpawnedCount).OrderBy(value => value).ToArray(), item),
                Quantile(results.Select(run => run.Cost).OrderBy(value => value).ToArray(), item),
                Quantile(results.Select(run => run.DurationSeconds).OrderBy(value => value).ToArray(), item),
                Quantile(results.Select(run => run.RewardValue).OrderBy(value => value).ToArray(), item)));
        return new(runs, meanCount, meanCost, meanDuration, meanReward,
            results.Average(item => Math.Pow(item.Cost - meanCost, 2)),
            results.SelectMany(item => item.Composition).GroupBy(item => item.Key)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Value), StringComparer.Ordinal),
            results, results.Average(item => Math.Pow(item.SpawnedCount - meanCount, 2)),
            results.Average(item => Math.Pow(item.DurationSeconds - meanDuration, 2)),
            results.Average(item => Math.Pow(item.RewardValue - meanReward, 2)), distribution);
    }

    private static T Quantile<T>(IReadOnlyList<T> sorted, double percentile) =>
        sorted[Math.Clamp((int)Math.Ceiling(percentile * sorted.Count) - 1, 0, sorted.Count - 1)];
}
