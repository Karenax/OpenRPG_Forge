using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Areas;

public sealed class AreaExperienceRuntimeServiceTests
{
    [Fact]
    public void Audio_api_emits_typed_started_completed_interrupted_music_and_snapshot_events()
    {
        var profile = Profile();
        var ids = Enumerable.Range(1, 8).Select(value => new Guid(value, 0, 0, new byte[8])).GetEnumerator();
        Guid NextId() { ids.MoveNext(); return ids.Current; }
        var now = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

        var started = AreaExperienceRuntimeService.PlayCue(profile, AreaAudioRuntimeState.Empty, "ambient.owl",
            now, NextId);
        var completed = AreaExperienceRuntimeService.Complete(started.State, started.Playback.PlaybackId, now,
            NextId);
        var second = AreaExperienceRuntimeService.PlayCue(profile, completed, "ambient.owl", now, NextId);
        var interrupted = AreaExperienceRuntimeService.Interrupt(second.State, second.Playback.PlaybackId,
            "area-transition", now, NextId);
        var music = AreaExperienceRuntimeService.SetMusicState(interrupted, "combat", now, NextId);
        var snapshot = AreaExperienceRuntimeService.PushSnapshot(music, "underwater", now, NextId);

        Assert.Contains(snapshot.Events, item => item is AudioCueStarted);
        Assert.Contains(snapshot.Events, item => item is AudioCueCompleted);
        Assert.Contains(snapshot.Events, item => item is AudioCueInterrupted);
        Assert.Contains(snapshot.Events, item => item is MusicStateChanged);
        Assert.Contains(snapshot.Events, item => item is AudioSnapshotPushed);
        Assert.Equal("underwater", Assert.Single(snapshot.Snapshots));
    }

    [Fact]
    public void Scatter_voice_and_loading_are_deterministic_localized_and_accessible()
    {
        var profile = Profile();
        var emitter = Assert.Single(profile.ScatterEmitters);
        var first = AreaExperienceRuntimeService.ResolveScatter(profile, emitter.Id, 4,
            new HashSet<string> { "forest" }, new HashSet<string>(), true);
        var repeated = AreaExperienceRuntimeService.ResolveScatter(profile, emitter.Id, 4,
            new HashSet<string> { "forest" }, new HashSet<string>(), true);
        Assert.Equal(first, repeated);
        Assert.InRange(first!.Position.X, -10, 10);
        Assert.Null(AreaExperienceRuntimeService.ResolveScatter(profile, emitter.Id, 4,
            new HashSet<string> { "forest" }, new HashSet<string> { "indoors" }, true));

        var voice = AreaExperienceRuntimeService.ResolveVoice(Assert.Single(profile.Voices), "de", "fr");
        Assert.True(voice.UsesTts);
        Assert.Equal("La route est sûre.", voice.Subtitle);
        var loading = AreaExperienceRuntimeService.ResolveLoading(profile.Loading, "fr", 2, 4, 0,
            reduceMotion: true, mediaAvailable: true);
        Assert.Equal(LoadingMediaKind.Image, loading.MediaKind);
        Assert.Equal(.5, loading.Progress);

        var videoOnlyReducedMotion = AreaExperienceRuntimeService.ResolveLoading(profile.Loading with
        { ImageResource = string.Empty }, "fr", 1, 1, 0, reduceMotion: true, imageAvailable: false,
            videoAvailable: true);
        Assert.Equal(LoadingMediaKind.AccessibleFallback, videoOnlyReducedMotion.MediaKind);
        Assert.Equal(1, AreaExperienceRuntimeService.ResolveLoading(profile.Loading, "fr", 0, 0, 0,
            reduceMotion: true, imageAvailable: false, videoAvailable: false).Progress);
    }

    internal static AreaExperienceProfile Profile()
    {
        var area = AreaId.From(Guid.NewGuid());
        var cue = new AudioCueDefinition(Guid.NewGuid(), "ambient.owl", string.Empty, "Ambience", .8f, false,
            string.Empty, string.Empty, "1.0", "test");
        return new AreaExperienceProfile(area.Value, 1, [cue],
            [new SpatialScatterEmitter(Guid.NewGuid(), cue.CueId, Vector3Value.Zero, new(20, 4, 20), true, 2026,
                2, 6, .8f, true, ["forest"], ["indoors"], "test")],
            [new CreatureVoiceProfile(Guid.NewGuid(), BlueprintId.From(Guid.NewGuid()), "fr",
                [new LocalizedVoiceTake("fr", string.Empty, "La route est sûre.", string.Empty, string.Empty,
                    string.Empty)], "tts.local", new VoiceConsentMetadata(true, true, "consent:test", "test"),
                "test")], [],
            new AreaLoadingPresentation(Guid.NewGuid(), "Chargement", "loading.png", "loading.ogv",
                new Dictionary<string, IReadOnlyList<string>> { ["fr"] = ["Explorez chaque détour."] }, true,
                "Chargement en cours.", true, 300, "test"), "test");
    }
}
