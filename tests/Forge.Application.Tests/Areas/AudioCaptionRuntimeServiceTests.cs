using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Areas;

public sealed class AudioCaptionRuntimeServiceTests
{
    [Fact]
    public void Resolves_locale_fallback_direction_duration_and_required_missing_diagnostic()
    {
        var caption = new SoundCaption("builtin://audio/dragon",
            [new("fr", "[Rugissement de dragon]"), new("en", "[Dragon roar]")], true, "critical",
            Guid.Parse("a0632000-0000-4000-8000-000000000001"), 1, "test:r063",
            "assets/audio.json", "CC0-1.0", new string('a', 64), "effect", 1.5f, 6, true);
        var profile = new AreaAudioProfile("", "", "", "", "neutral", 1, [],
            orchestration: new([], [], [caption]));

        var resolution = AudioCaptionRuntimeService.Resolve(profile, caption.Resource, "de", "fr",
            Vector3Value.Zero, new(0, 0, -1), new(-4, 0, 0), true);
        var optional = AudioCaptionRuntimeService.Resolve(profile, "builtin://audio/missing", "fr", "fr",
            Vector3Value.Zero, new(0, 0, -1), Vector3Value.Zero);
        var required = AudioCaptionRuntimeService.Resolve(profile, "builtin://audio/missing", "fr", "fr",
            Vector3Value.Zero, new(0, 0, -1), Vector3Value.Zero, true);

        Assert.True(resolution.Available);
        Assert.Equal("fr", resolution.Presentation!.Locale);
        Assert.Equal(AudioCaptionDirection.Left, resolution.Presentation.Direction);
        Assert.StartsWith("left: ", resolution.Presentation.AccessibleText, StringComparison.Ordinal);
        Assert.InRange(resolution.Presentation.DisplaySeconds, 1.5f, 6);
        Assert.Empty(optional.Diagnostics);
        Assert.Equal(AudioCaptionRuntimeService.RequiredMissingCode, Assert.Single(required.Diagnostics).Code);
    }

    [Fact]
    public void ForgeScript_snapshot_reads_localized_caption_without_exposing_runtime_nodes()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("a0632000-0000-4000-8000-000000000010")),
            "caption-test", "Caption test", []);
        var environment = area.Environment;
        var caption = new SoundCaption("builtin://audio/bell", [new("fr", "[Cloche]")], false,
            "important", Guid.Parse("a0632000-0000-4000-8000-000000000002"), 1, "test:r063",
            "assets/audio.json", "CC0-1.0", new string('b', 64), "ambient");
        var orchestration = environment.Audio.Orchestration with { Captions = [caption] };
        var source = environment.Audio;
        var audio = new AreaAudioProfile(source.DayMusic, source.NightMusic, source.CombatMusic,
            source.Ambience, source.ReverbPreset, source.TransitionSeconds, source.Playlist,
            source.OrderMode, source.IntervalSeconds, source.IntervalVariationSeconds, source.RelativeVolume,
            source.Spatial, source.RuntimeControl, source.AdaptiveLayers, orchestration, source.Emitters,
            source.Authoring);
        var configured = area.WithEnvironment(new AreaEnvironmentProfile(environment.SchemaVersion,
            environment.Classification, environment.Tags, environment.Lighting, environment.Fog, audio,
            environment.Rendering, environment.Sky, environment.Wind, environment.Weather,
            environment.Water, environment.Reverb, environment.Authoring,
            environment.RequireAuthoringRuntimeMetadata));

        var snapshot = AreaAudioScriptService.ReadCaption(configured, caption.Resource, "fr");

        Assert.NotNull(snapshot);
        Assert.Equal(caption.Id, snapshot!.Id);
        Assert.Equal("[Cloche]", snapshot.Text);
        Assert.Equal("ambient", snapshot.CueKind);
    }
}
