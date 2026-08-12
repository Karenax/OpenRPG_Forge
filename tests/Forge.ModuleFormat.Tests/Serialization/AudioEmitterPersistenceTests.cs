using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class AudioEmitterPersistenceTests
{
    [Fact]
    public void Project_and_area_round_trip_preserve_audio_emitters_exactly()
    {
        var project = WithAudio(TestProjectFactory.CreateValid(), CreateCatalog("audio/bell.ogg", 256, new string('a', 64)));
        var area = project.Areas.Single();

        var projectJson = ProjectJsonSerializer.Serialize(project);
        var areaJson = AreaJsonSerializer.Serialize(area);

        var restoredProject = ProjectJsonSerializer.Deserialize(projectJson);
        var restoredArea = AreaJsonSerializer.Deserialize(areaJson);
        Assert.Equal(projectJson, ProjectJsonSerializer.Serialize(restoredProject));
        Assert.Equal(areaJson, AreaJsonSerializer.Serialize(restoredArea));
        Assert.Equal("audio/bell.ogg", restoredArea.Environment.Audio.Emitters.Definitions.Single()
            .ClipSet.Clips.Single().ResourcePath);
        Assert.Equal(new Vector3Value(2, 1, -3), restoredArea.Environment.Audio.Emitters.Instances.Single().Transform.Position);
    }

    [Fact]
    public void R061_audio_authoring_round_trip_preserves_selection_modulation_mix_and_listener_policy()
    {
        var source = TestProjectFactory.CreateValid();
        var catalog = CreateCatalog("audio/bell.ogg", 256, new string('a', 64));
        var projectWithCatalog = WithAudio(source, catalog);
        var area = projectWithCatalog.Areas.Single();
        var environment = area.Environment;
        var audio = environment.Audio;
        var authoring = new AudioAuthoringProfile(Guid.Parse("a0614000-0000-4000-8000-000000000001"),
            1, "test:r061", "assets/audio.json", "CC0-1.0", new string('d', 64), true,
            new(88, 2, new Dictionary<string, float> { ["audio/bell.ogg"] = 4 }),
            new(2, 8, -1, 1, .8f, 1.2f, AudioModulationDistribution.Gaussian,
                [new(0, 0), new(.5f, .4f), new(1, 1)], 99, true, 110, .5f),
            new(-16, "Effects", -9, 1.5f, .8f, AudioMeterMode.Rms),
            new(area.Id.ToString(), "Effects", AudioListenerPolicy.Party,
                new Dictionary<string, float> { ["party-a"] = .7f }));
        var updatedAudio = new AreaAudioProfile(audio.DayMusic, audio.NightMusic, audio.CombatMusic,
            audio.Ambience, audio.ReverbPreset, audio.TransitionSeconds, ["audio/bell.ogg"],
            AudioOrderMode.Weighted, 5, 3, .9f, audio.Spatial, audio.RuntimeControl,
            audio.AdaptiveLayers, audio.Orchestration, catalog, authoring);
        var updatedEnvironment = new AreaEnvironmentProfile(environment.SchemaVersion,
            environment.Classification, environment.Tags, environment.Lighting, environment.Fog,
            updatedAudio, environment.Rendering, environment.Sky, environment.Wind, environment.Weather,
            environment.Water, environment.Reverb, environment.Authoring,
            environment.RequireAuthoringRuntimeMetadata);
        var project = new ProjectDefinition(projectWithCatalog.Id, projectWithCatalog.Title,
            projectWithCatalog.Module, projectWithCatalog.Blueprints, [area.WithEnvironment(updatedEnvironment)],
            projectWithCatalog.Dialogues, projectWithCatalog.Quests);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json).Areas.Single().Environment.Audio;

        Assert.Equal(json, ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(json)));
        Assert.Equal(AudioOrderMode.Weighted, restored.OrderMode);
        Assert.Equal(AudioModulationDistribution.Gaussian, restored.Authoring.Modulation.Distribution);
        Assert.Equal("Effects", restored.Authoring.Mix.Bus);
        Assert.Equal(AudioListenerPolicy.Party, restored.Authoring.Ambient.ListenerPolicy);
        Assert.Equal(.7f, restored.Authoring.Ambient.ListenerGains["party-a"]);
    }

    [Fact]
    public void R062_audio_orchestration_round_trip_preserves_debug_control_crossfade_sync_and_monitoring()
    {
        var project = TestProjectFactory.CreateValid();
        var area = project.Areas.Single();
        var environment = area.Environment;
        var master = Guid.Parse("a0624000-0000-4000-8000-000000000001");
        var audio = new AreaAudioProfile("day.ogg", "night.ogg", "combat.ogg", "forest.ogg",
            "forest", 2, [], spatial: new(AreaSoundMode.Spatial3D, 3, 42, .1f, "inverse-square", true),
            runtimeControl: new(false, AudioInstancePolicy.Restart, AudioAuthority.Server, 2,
                "audio.started", "audio.stopped", "audio.paused", "audio.faded", "1.0"),
            adaptiveLayers: [new("rain.ogg", 18, 6, "rain", "forest", .2f, false, .8f, 3,
                "forest-volume")],
            orchestration: new([
                new(MusicState.Combat, "combat.ogg", ["drums.ogg"], 1, 3, true, 120, 4),
            ], [new(master, "Master", null, .8f, "default", "all", .7f, true, true)], []));
        var updated = new AreaEnvironmentProfile(environment.SchemaVersion, environment.Classification,
            environment.Tags, environment.Lighting, environment.Fog, audio, environment.Rendering,
            environment.Sky, environment.Wind, environment.Weather, environment.Water,
            environment.Reverb, environment.Authoring, environment.RequireAuthoringRuntimeMetadata);
        var candidate = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            [area.WithEnvironment(updated)], project.Dialogues, project.Quests);

        var json = ProjectJsonSerializer.Serialize(candidate);
        var restored = ProjectJsonSerializer.Deserialize(json).Areas.Single().Environment.Audio;

        Assert.Equal(json, ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(json)));
        Assert.True(restored.Spatial.DebugSpheresVisible);
        Assert.Equal("audio.paused", restored.RuntimeControl.PausedEvent);
        Assert.Equal(3, restored.AdaptiveLayers.Single().CrossfadeSeconds);
        Assert.True(restored.Orchestration.MusicStates.Single().BeatSynchronized);
        Assert.True(restored.Orchestration.MixBuses.Single().MonitorEnabled);
    }

    [Fact]
    public void R063_sound_caption_round_trip_preserves_identity_locales_direction_and_governance()
    {
        var project = TestProjectFactory.CreateValid();
        var area = project.Areas.Single();
        var environment = area.Environment;
        var caption = new SoundCaption("builtin://audio/dragon",
            [new("fr", "[Rugissement à gauche]"), new("en", "[Roar to the left]")], true,
            "critical", Guid.Parse("a0634000-0000-4000-8000-000000000001"), 1, "test:r063",
            "assets/audio-captions.json", "CC0-1.0", new string('e', 64), "effect", 1.5f, 6, true);
        var orchestration = environment.Audio.Orchestration with { Captions = [caption] };
        var source = environment.Audio;
        var audio = new AreaAudioProfile(source.DayMusic, source.NightMusic, source.CombatMusic,
            source.Ambience, source.ReverbPreset, source.TransitionSeconds, source.Playlist,
            source.OrderMode, source.IntervalSeconds, source.IntervalVariationSeconds, source.RelativeVolume,
            source.Spatial, source.RuntimeControl, source.AdaptiveLayers, orchestration, source.Emitters,
            source.Authoring);
        var updated = new AreaEnvironmentProfile(environment.SchemaVersion, environment.Classification,
            environment.Tags, environment.Lighting, environment.Fog, audio, environment.Rendering,
            environment.Sky, environment.Wind, environment.Weather, environment.Water,
            environment.Reverb, environment.Authoring, environment.RequireAuthoringRuntimeMetadata);
        var candidate = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            [area.WithEnvironment(updated)], project.Dialogues, project.Quests);

        var json = ProjectJsonSerializer.Serialize(candidate);
        var restored = ProjectJsonSerializer.Deserialize(json).Areas.Single().Environment.Audio.Orchestration
            .Captions.Single();

        Assert.Equal(json, ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(json)));
        Assert.Equal(caption.Id, restored.Id);
        Assert.Equal(2, restored.Localizations.Count);
        Assert.True(restored.ShowDirection);
        Assert.Equal("CC0-1.0", restored.LicenseSpdx);
        Assert.True(restored.RequiredAtRuntime);
    }

    internal static ProjectDefinition WithAudio(ProjectDefinition project, AudioEmitterCatalog catalog)
    {
        var area = project.Areas.Single();
        var source = area.Environment;
        var audio = source.Audio;
        var updatedAudio = new AreaAudioProfile(audio.DayMusic, audio.NightMusic, audio.CombatMusic, audio.Ambience,
            audio.ReverbPreset, audio.TransitionSeconds, audio.Playlist, audio.OrderMode, audio.IntervalSeconds,
            audio.IntervalVariationSeconds, audio.RelativeVolume, audio.Spatial, audio.RuntimeControl,
            audio.AdaptiveLayers, audio.Orchestration, catalog, audio.Authoring);
        var environment = new AreaEnvironmentProfile(source.SchemaVersion, source.Classification, source.Tags,
            source.Lighting, source.Fog, updatedAudio, source.Rendering, source.Sky, source.Wind, source.Weather,
            source.Water, source.Reverb);
        return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            [area.WithEnvironment(environment)], project.Dialogues, project.Quests);
    }

    internal static AudioEmitterCatalog CreateCatalog(string resourcePath, long bytes, string hash)
    {
        var definitionId = Guid.Parse("a0040000-0000-4000-8000-000000000101");
        return new AudioEmitterCatalog(1,
        [
            new(definitionId, 1, "Bell", new AudioClipSet(
                    Guid.Parse("a0040000-0000-4000-8000-000000000102"), 1, "Bell clips",
                    AudioClipSelectionMode.Ordered,
                    [new(Guid.Parse("a0040000-0000-4000-8000-000000000103"), resourcePath, 1,
                        ["bell"], "bronze", "fr", -16, string.Empty, "audio/ogg", bytes, "CC0-1.0",
                        "test:audio", hash)], "test:audio"),
                new(AudioLoopMode.OneShot, 0, 0, 0, 0, string.Empty),
                new(1, 25, "inverse-square", 220, -10, .2f, 0, true), null, 0,
                string.Empty, string.Empty, true, "test:audio", "assets/manifest.json", "CC0-1.0",
                new string('b', 64)),
        ],
        [
            new(Guid.Parse("a0040000-0000-4000-8000-000000000104"), definitionId,
                new TransformValue(new Vector3Value(2, 1, -3), QuaternionValue.Identity, Vector3Value.One),
                true, "test:audio"),
        ]);
    }
}
