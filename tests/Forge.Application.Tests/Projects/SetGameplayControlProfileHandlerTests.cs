using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Gameplay;

namespace Forge.Application.Tests.Projects;

public sealed class SetGameplayControlProfileHandlerTests
{
    [Fact]
    public void Command_previews_applies_and_round_trips_history_atomically()
    {
        var context = new PlaceEntityTestContext();
        var current = context.State.Project.Module.RuntimePolicies.Gameplay;
        var time = new TimeControlPolicy(current.TimeControl.Id, 1, true, true, .15f, true, 12, true,
            MultiplayerPausePolicy.Vote, .6f, ["session.player"], "test");
        var changed = new ModuleGameplayControlProfile(current.Id, 1, current.Swimming, current.Climbing,
            current.RadialMenu, time, current.CombatModes, "test");
        var handler = new SetGameplayControlProfileHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            changed, true);

        var preview = handler.Handle(context.State, command);
        Assert.True(preview.Succeeded);
        Assert.Null(preview.NewRevision);
        Assert.Equal("module/runtimePolicies/gameplay", preview.Patch!.Path);
        var applied = handler.Handle(context.State, command with { DryRun = false });
        Assert.True(applied.Succeeded);
        Assert.Equal(.15f, applied.State.Project.Module.RuntimePolicies.Gameplay.TimeControl.SlowMotionScale);

        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(current.TimeControl.SlowMotionScale,
            history.State.Project.Module.RuntimePolicies.Gameplay.TimeControl.SlowMotionScale);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(.15f, history.State.Project.Module.RuntimePolicies.Gameplay.TimeControl.SlowMotionScale);
    }
}
