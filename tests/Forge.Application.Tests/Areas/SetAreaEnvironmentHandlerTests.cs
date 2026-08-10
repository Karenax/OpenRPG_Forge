using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.Domain.Audio;

namespace Forge.Application.Tests.Areas;

public sealed class SetAreaEnvironmentHandlerTests
{
    [Fact]
    public void Applies_complete_environment_atomically_and_round_trips_history()
    {
        var context = new PlaceEntityTestContext();
        var profile = new AreaEnvironmentProfile(AreaEnvironmentProfile.CurrentSchemaVersion, AreaEnvironmentClass.Underground, ["cave", "wet"],
            new(DayNightMode.AlwaysNight, 30, .1f, .2f, 3200, true, true),
            new(FogMode.Volumetric, .25f, 2, 40, 3), new("day", "night", "combat", "drips", "cave", 1.5f),
            new(1, PbrMaterialWorkflow.MetallicRoughness, ToneMappingProfile.Aces, RenderQualityTier.Ultra,
                true, true, true, true, 1.2, 8, string.Empty,
                [new("desktop", 48, 12, 8192, .8)]),
            new(1, SkyProfileMode.Procedural, string.Empty, "#171225", "#5a3248", "#100d17",
                .2, .4, .3, .6, 4),
            new(1, 1, 0, 7, .5f, 4, true, true, "wind.cave", 1, []),
            WeatherProfile.CreateDefault(AreaEnvironmentClass.Exterior),
            new(1, [new(
                Guid.NewGuid(), "Pool", WaterBodyKind.Volume, Vector3Value.Zero, new(8, 2, 6), 2,
                1, 0, .2f, 1, 2, true, WaterQualityTier.High, string.Empty, "water.pool")]),
            new(1, "cave", [new(
                Guid.NewGuid(), "Cave", Vector3Value.Zero, new(12, 6, 12), "cave", .7f, 1, 1,
                string.Empty, string.Empty, string.Empty, string.Empty)]));
        var handler = new SetAreaEnvironmentHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.AreaId, profile, false);
        var history = new ProjectCommandHistory(context.State);
        history.Record(handler.Handle(history.State, command));
        Assert.Equal(profile, history.State.Project.Areas.Single().Environment);
        Assert.Equal(ToneMappingProfile.Aces, history.State.Project.Areas.Single().Environment.Rendering.ToneMapping);
        Assert.Equal(SkyProfileMode.Procedural, history.State.Project.Areas.Single().Environment.Sky.Mode);
        Assert.Equal(7, history.State.Project.Areas.Single().Environment.Wind.Speed);
        Assert.Equal(WeatherKind.Clear, history.State.Project.Areas.Single().Environment.Weather.States
            .Single(state => state.Id == profile.Weather.InitialStateId).Kind);
        Assert.Single(history.State.Project.Areas.Single().Environment.Water.Bodies);
        Assert.Equal("cave", Assert.Single(history.State.Project.Areas.Single().Environment.Reverb.Zones).Preset);
        Assert.True(history.Undo().Succeeded);
        Assert.NotEqual(profile, history.State.Project.Areas.Single().Environment);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(profile, history.State.Project.Areas.Single().Environment);
    }

    [Fact]
    public void Rejects_invalid_audio_emitter_before_mutating_project()
    {
        var context = new PlaceEntityTestContext();
        var source = context.State.Project.Areas.Single().Environment;
        var definitionId = Guid.Parse("a0040000-0000-4000-8000-000000000201");
        var invalidCatalog = new AudioEmitterCatalog(1,
        [
            new(definitionId, 1, "Broken", new AudioClipSet(Guid.NewGuid(), 1, "Broken",
                AudioClipSelectionMode.Ordered,
                [new(Guid.NewGuid(), "../outside.ogg", 1, [], "", "fr", -18, "", "audio/ogg", 1,
                    "UNLICENSED", "", "bad")], "test"),
                new AudioLoopPolicy(AudioLoopMode.OneShot, 0, 0, 0, 0, ""),
                new AudioEmitterSpatialProfile(1, 20, "linear", 180, -12, 0, 0, true), null, 0, "", "",
                true, "test", "assets/manifest.json", "UNLICENSED", "bad"),
        ],
        [new(Guid.NewGuid(), definitionId, TransformValue.Identity, true, "test")]);
        var audio = source.Audio;
        var changedAudio = new AreaAudioProfile(audio.DayMusic, audio.NightMusic, audio.CombatMusic, audio.Ambience,
            audio.ReverbPreset, audio.TransitionSeconds, audio.Playlist, audio.OrderMode, audio.IntervalSeconds,
            audio.IntervalVariationSeconds, audio.RelativeVolume, audio.Spatial, audio.RuntimeControl,
            audio.AdaptiveLayers, audio.Orchestration, invalidCatalog);
        var environment = new AreaEnvironmentProfile(source.SchemaVersion, source.Classification, source.Tags,
            source.Lighting, source.Fog, changedAudio, source.Rendering, source.Sky, source.Wind, source.Weather,
            source.Water, source.Reverb);
        var command = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.AreaId, environment, false);

        var result = new SetAreaEnvironmentHandler(new FixedClock(PlaceEntityTestContext.Timestamp))
            .Handle(context.State, command);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, item => item.Code == AudioEmitterCatalogValidator.DefinitionInvalidCode);
        Assert.Contains(result.Diagnostics, item => item.Code == AudioEmitterCatalogValidator.ClipInvalidCode);
        Assert.Equal(context.State, result.State);
    }
}
