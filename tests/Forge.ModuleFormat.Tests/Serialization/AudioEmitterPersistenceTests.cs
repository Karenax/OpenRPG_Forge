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

    internal static ProjectDefinition WithAudio(ProjectDefinition project, AudioEmitterCatalog catalog)
    {
        var area = project.Areas.Single();
        var source = area.Environment;
        var audio = source.Audio;
        var updatedAudio = new AreaAudioProfile(audio.DayMusic, audio.NightMusic, audio.CombatMusic, audio.Ambience,
            audio.ReverbPreset, audio.TransitionSeconds, audio.Playlist, audio.OrderMode, audio.IntervalSeconds,
            audio.IntervalVariationSeconds, audio.RelativeVolume, audio.Spatial, audio.RuntimeControl,
            audio.AdaptiveLayers, audio.Orchestration, catalog);
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
