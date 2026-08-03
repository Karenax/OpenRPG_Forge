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
        var profile = new AreaEnvironmentProfile(1, AreaEnvironmentClass.Underground, ["cave", "wet"],
            new(DayNightMode.AlwaysNight, 30, .1f, .2f, 3200, true, true),
            new(FogMode.Volumetric, .25f, 2, 40, 3), new("day", "night", "combat", "drips", "cave", 1.5f));
        var handler = new SetAreaEnvironmentHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.AreaId, profile, false);
        var history = new ProjectCommandHistory(context.State);
        history.Record(handler.Handle(history.State, command));
        Assert.Equal(profile, history.State.Project.Areas.Single().Environment);
        Assert.True(history.Undo().Succeeded);
        Assert.NotEqual(profile, history.State.Project.Areas.Single().Environment);
    }
}
