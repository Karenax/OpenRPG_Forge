using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Spatial;

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
    }
}
