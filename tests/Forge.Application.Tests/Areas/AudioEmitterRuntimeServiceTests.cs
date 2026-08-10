using Forge.Application.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Areas;

public sealed class AudioEmitterRuntimeServiceTests
{
    [Fact]
    public void Creates_positioned_playback_with_loop_policy_and_safe_handle()
    {
        var definitionId = Guid.Parse("a0040000-0000-4000-8000-000000000011");
        var instanceId = Guid.Parse("a0040000-0000-4000-8000-000000000012");
        var clip = Clip(Guid.Parse("a0040000-0000-4000-8000-000000000013"), "audio/fire.ogg", 1);
        var catalog = Catalog(definitionId, instanceId, AudioClipSelectionMode.Ordered, [clip], AudioLoopMode.Loop);

        var playback = Assert.Single(AudioEmitterRuntimeService.CreateInitialPlaybacks(catalog));

        Assert.Equal(instanceId, playback.Handle.Id);
        Assert.Equal("audio/fire.ogg", playback.ResourcePath);
        Assert.Equal(new Vector3Value(4, 2, -1), playback.Transform.Position);
        Assert.Equal(AudioLoopMode.Loop, playback.Loop.Mode);
        Assert.True(playback.Autoplay);
    }

    [Fact]
    public void Weighted_selection_is_deterministic_and_event_driven_emitters_do_not_autoplay()
    {
        var definitionId = Guid.Parse("a0040000-0000-4000-8000-000000000021");
        var instanceId = Guid.Parse("a0040000-0000-4000-8000-000000000022");
        var catalog = Catalog(definitionId, instanceId, AudioClipSelectionMode.Weighted,
        [
            Clip(Guid.Parse("a0040000-0000-4000-8000-000000000023"), "audio/a.ogg", 1),
            Clip(Guid.Parse("a0040000-0000-4000-8000-000000000024"), "audio/b.ogg", 3),
        ], AudioLoopMode.EventDriven);

        var first = Assert.Single(AudioEmitterRuntimeService.CreateInitialPlaybacks(catalog, 42));
        var second = Assert.Single(AudioEmitterRuntimeService.CreateInitialPlaybacks(catalog, 42));

        Assert.Equal(first.ClipId, second.ClipId);
        Assert.False(first.Autoplay);
        Assert.Equal("audio.stop", first.Loop.StopEvent);
    }

    [Fact]
    public void Missing_optional_catalog_is_ignored_cleanly()
    {
        Assert.Empty(AudioEmitterRuntimeService.CreateInitialPlaybacks(null));
    }

    private static AudioEmitterCatalog Catalog(Guid definitionId, Guid instanceId,
        AudioClipSelectionMode selection, IReadOnlyList<AudioClipDefinition> clips, AudioLoopMode loopMode) =>
        new(1,
        [
            new(definitionId, 1, "Emitter", new AudioClipSet(Guid.NewGuid(), 1, "Clips", selection, clips, "test"),
                new AudioLoopPolicy(loopMode, loopMode == AudioLoopMode.FiniteCount ? 2 : 0, 0, 1, 0,
                    loopMode == AudioLoopMode.EventDriven ? "audio.stop" : string.Empty),
                new AudioEmitterSpatialProfile(1, 20, "inverse-square", 180, -12, 0, 0, true),
                null, 0, string.Empty, string.Empty, false, "test", "assets/manifest.json", "CC0-1.0",
                new string('b', 64)),
        ],
        [new(instanceId, definitionId,
            new TransformValue(new Vector3Value(4, 2, -1), QuaternionValue.Identity, Vector3Value.One), true, "test")]);

    private static AudioClipDefinition Clip(Guid id, string path, float weight) =>
        new(id, path, weight, ["ambient"], "default", "fr", -18, string.Empty, "audio/ogg", 128,
            "CC0-1.0", "test", new string('a', 64));
}
