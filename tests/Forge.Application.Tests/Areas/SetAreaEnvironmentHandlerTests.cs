using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;

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
                .2, .4, .3, .6, 4));
        var handler = new SetAreaEnvironmentHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.AreaId, profile, false);
        var history = new ProjectCommandHistory(context.State);
        history.Record(handler.Handle(history.State, command));
        Assert.Equal(profile, history.State.Project.Areas.Single().Environment);
        Assert.Equal(ToneMappingProfile.Aces, history.State.Project.Areas.Single().Environment.Rendering.ToneMapping);
        Assert.Equal(SkyProfileMode.Procedural, history.State.Project.Areas.Single().Environment.Sky.Mode);
        Assert.True(history.Undo().Succeeded);
        Assert.NotEqual(profile, history.State.Project.Areas.Single().Environment);
    }
}
