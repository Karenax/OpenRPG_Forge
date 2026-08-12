using Forge.Domain.Areas;
using Forge.Domain.Audio;

namespace Forge.Application.Areas;

public sealed record PlannedAudioCue(
    string Resource,
    float DelaySeconds,
    float RelativeVolume,
    float PitchSemitones = 0,
    string Bus = "Ambience");

public sealed record AudioModulationStatistics(
    int SampleCount,
    float MinimumDelaySeconds,
    float MaximumDelaySeconds,
    float MeanDelaySeconds,
    float MinimumPitchSemitones,
    float MaximumPitchSemitones,
    float MeanRelativeVolume,
    IReadOnlyDictionary<string, int> ResourceCounts);

public static class AreaAudioSequencePlanner
{
    public static IReadOnlyList<PlannedAudioCue> Plan(AreaAudioProfile profile, int count, int seed)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (profile.Playlist.Count == 0 || count == 0) return [];

        var authoring = profile.Authoring;
        var selectionRandom = new Random(HashCode.Combine(seed, authoring.Selection.DeterministicSeed));
        var modulationRandom = new Random(HashCode.Combine(seed, authoring.Modulation.DeterministicSeed));
        var recent = new Queue<string>();
        var shuffleBag = new Queue<string>();
        var result = new List<PlannedAudioCue>(count);
        for (var index = 0; index < count; index++)
        {
            var resource = Select(profile, index, selectionRandom, recent, shuffleBag);
            Remember(resource, recent, authoring.Selection.NoRepeatWindow);
            var intervalSample = Sample(modulationRandom, authoring.Modulation.Distribution);
            var shaped = ApplyCurve(authoring.Modulation.Curve, intervalSample);
            var delay = Lerp(authoring.Modulation.MinimumIntervalSeconds,
                authoring.Modulation.MaximumIntervalSeconds, shaped);
            if (authoring.Modulation.SyncToTempo)
            {
                var beatSeconds = 60f / authoring.Modulation.TempoBpm * authoring.Modulation.BeatDivision;
                delay = MathF.Max(0, MathF.Round(delay / beatSeconds) * beatSeconds);
            }
            var pitch = Lerp(authoring.Modulation.MinimumPitchSemitones,
                authoring.Modulation.MaximumPitchSemitones,
                Sample(modulationRandom, authoring.Modulation.Distribution));
            var volume = profile.RelativeVolume * Lerp(authoring.Modulation.MinimumVolume,
                authoring.Modulation.MaximumVolume,
                Sample(modulationRandom, authoring.Modulation.Distribution));
            result.Add(new(resource, delay, volume, pitch, authoring.Mix.Bus));
        }
        return result;
    }

    public static AudioModulationStatistics PreviewStatistics(
        AreaAudioProfile profile,
        int sampleCount,
        int seed)
    {
        if (sampleCount is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(sampleCount));
        var samples = Plan(profile, sampleCount, seed);
        if (samples.Count == 0)
            return new(0, 0, 0, 0, 0, 0, 0,
                new Dictionary<string, int>(StringComparer.Ordinal));
        return new(samples.Count,
            samples.Min(item => item.DelaySeconds), samples.Max(item => item.DelaySeconds),
            samples.Average(item => item.DelaySeconds), samples.Min(item => item.PitchSemitones),
            samples.Max(item => item.PitchSemitones), samples.Average(item => item.RelativeVolume),
            samples.GroupBy(item => item.Resource, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
    }

    private static string Select(AreaAudioProfile profile, int index, Random random,
        Queue<string> recent, Queue<string> shuffleBag)
    {
        return profile.OrderMode switch
        {
            AudioOrderMode.Sequential => profile.Playlist[index % profile.Playlist.Count],
            AudioOrderMode.ShuffleBag => SelectShuffleBag(profile.Playlist, random, recent, shuffleBag),
            AudioOrderMode.Weighted => SelectWeighted(profile, random, recent),
            _ => SelectRandom(profile.Playlist, random, recent),
        };
    }

    private static string SelectRandom(IReadOnlyList<string> playlist, Random random, Queue<string> recent)
    {
        var candidates = Candidates(playlist, recent);
        return candidates[random.Next(candidates.Count)];
    }

    private static string SelectShuffleBag(IReadOnlyList<string> playlist, Random random,
        Queue<string> recent, Queue<string> shuffleBag)
    {
        if (shuffleBag.Count == 0)
        {
            var shuffled = playlist.OrderBy(_ => random.Next()).ToList();
            if (shuffled.Count > 1 && recent.Count > 0 && recent.Contains(shuffled[0]))
            {
                var replacement = shuffled.FindIndex(item => !recent.Contains(item));
                if (replacement > 0) (shuffled[0], shuffled[replacement]) = (shuffled[replacement], shuffled[0]);
            }
            foreach (var resource in shuffled) shuffleBag.Enqueue(resource);
        }
        return shuffleBag.Dequeue();
    }

    private static string SelectWeighted(AreaAudioProfile profile, Random random, Queue<string> recent)
    {
        var candidates = Candidates(profile.Playlist, recent);
        var clipWeights = profile.Emitters.Definitions.SelectMany(item => item.ClipSet.Clips)
            .GroupBy(item => item.ResourcePath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Weight, StringComparer.Ordinal);
        var weighted = candidates.Select(resource => new
        {
            Resource = resource,
            Weight = profile.Authoring.Selection.Weights.GetValueOrDefault(resource,
                clipWeights.GetValueOrDefault(resource, 1)),
        }).ToArray();
        var total = weighted.Sum(item => item.Weight);
        var target = random.NextDouble() * total;
        foreach (var item in weighted)
        {
            target -= item.Weight;
            if (target <= 0) return item.Resource;
        }
        return weighted[^1].Resource;
    }

    private static IReadOnlyList<string> Candidates(IReadOnlyList<string> playlist, Queue<string> recent)
    {
        if (recent.Count == 0) return playlist;
        var candidates = playlist.Where(item => !recent.Contains(item)).ToArray();
        return candidates.Length == 0 ? playlist : candidates;
    }

    private static void Remember(string resource, Queue<string> recent, int window)
    {
        if (window == 0) return;
        recent.Enqueue(resource);
        while (recent.Count > Math.Min(window, 1024)) recent.Dequeue();
    }

    private static float Sample(Random random, AudioModulationDistribution distribution) => distribution switch
    {
        AudioModulationDistribution.Triangular => (float)((random.NextDouble() + random.NextDouble()) / 2),
        AudioModulationDistribution.Gaussian => (float)Enumerable.Range(0, 6).Average(_ => random.NextDouble()),
        _ => (float)random.NextDouble(),
    };

    private static float ApplyCurve(IReadOnlyList<AudioModulationCurvePoint> curve, float position)
    {
        var ordered = curve.OrderBy(item => item.Position).ToArray();
        if (position <= ordered[0].Position) return ordered[0].Value;
        if (position >= ordered[^1].Position) return ordered[^1].Value;
        for (var index = 1; index < ordered.Length; index++)
        {
            if (position > ordered[index].Position) continue;
            var first = ordered[index - 1];
            var second = ordered[index];
            var amount = (position - first.Position) / (second.Position - first.Position);
            return Lerp(first.Value, second.Value, amount);
        }
        return position;
    }

    private static float Lerp(float first, float second, float amount) => first + (second - first) * amount;
}
