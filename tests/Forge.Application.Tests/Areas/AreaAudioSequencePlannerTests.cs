using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
namespace Forge.Application.Tests.Areas;

public sealed class AreaAudioSequencePlannerTests
{
    [Fact]
    public void Sequential_and_random_sequences_respect_interval_variation_and_volume()
    {
        var sequential = new AreaAudioProfile("", "", "", "", "forest", 1, ["birds.ogg", "wind.ogg"], AudioOrderMode.Sequential, 10, 2, .7f);
        var cues = AreaAudioSequencePlanner.Plan(sequential, 3, 42);
        Assert.Equal(["birds.ogg", "wind.ogg", "birds.ogg"], cues.Select(item => item.Resource));
        Assert.All(cues, cue => Assert.InRange(cue.DelaySeconds, 8, 12)); Assert.All(cues, cue => Assert.Equal(.7f, cue.RelativeVolume));
        var a = AreaAudioSequencePlanner.Plan(new AreaAudioProfile("", "", "", "", "forest", 1, sequential.Playlist, AudioOrderMode.DeterministicRandom, 10, 2, .7f), 8, 7);
        var b = AreaAudioSequencePlanner.Plan(new AreaAudioProfile("", "", "", "", "forest", 1, sequential.Playlist, AudioOrderMode.DeterministicRandom, 10, 2, .7f), 8, 7);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Weighted_shuffle_no_repeat_and_modulation_preview_are_deterministic()
    {
        var playlist = new[] { "rain.ogg", "wind.ogg", "owl.ogg" };
        var authoring = new AudioAuthoringProfile(
            Guid.Parse("a0611000-0000-4000-8000-000000000001"), 1, "test:r061",
            "assets/audio.json", "CC0-1.0", new string('a', 64), false,
            new(42, 1, new Dictionary<string, float>
            {
                ["rain.ogg"] = 8,
                ["wind.ogg"] = 2,
                ["owl.ogg"] = 1,
            }),
            new(4, 12, -2, 2, .8f, 1.2f, AudioModulationDistribution.Triangular,
                [new(0, 0), new(.5f, .25f), new(1, 1)], 17, true, 120, 1),
            new(-18, "Ambience", -3, 1, 1, AudioMeterMode.Loudness),
            new(string.Empty, "Ambience", AudioListenerPolicy.EveryListener,
                new Dictionary<string, float>()));
        var weighted = new AreaAudioProfile("", "", "", "", "forest", 1, playlist,
            AudioOrderMode.Weighted, 8, 4, 1, authoring: authoring);
        var first = AreaAudioSequencePlanner.Plan(weighted, 60, 99);
        var second = AreaAudioSequencePlanner.Plan(weighted, 60, 99);
        var statistics = AreaAudioSequencePlanner.PreviewStatistics(weighted, 512, 99);
        var shuffle = AreaAudioSequencePlanner.Plan(new AreaAudioProfile("", "", "", "", "forest", 1,
            playlist, AudioOrderMode.ShuffleBag, 8, 4, 1, authoring: authoring), 6, 99);

        Assert.Equal(first, second);
        Assert.All(Enumerable.Range(1, first.Count - 1), index =>
            Assert.NotEqual(first[index - 1].Resource, first[index].Resource));
        Assert.All(first, cue => Assert.InRange(Math.Abs(cue.DelaySeconds % .5f), 0, .0001f));
        Assert.InRange(statistics.MinimumDelaySeconds, 4, 12);
        Assert.InRange(statistics.MaximumDelaySeconds, 4, 12);
        Assert.Equal(playlist.Order(), shuffle.Take(3).Select(item => item.Resource).Order());
        Assert.True(statistics.ResourceCounts["rain.ogg"] > statistics.ResourceCounts["owl.ogg"]);
    }
}
