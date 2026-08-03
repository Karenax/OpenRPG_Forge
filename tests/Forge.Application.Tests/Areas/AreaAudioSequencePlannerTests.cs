using Forge.Application.Areas;
using Forge.Domain.Areas;
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
}
