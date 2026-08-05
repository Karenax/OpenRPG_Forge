using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Areas;
using Forge.Application.Tests.Entities;

namespace Forge.Application.Tests.Projects;

public sealed class SetAreaExperienceProfileHandlerTests
{
    [Fact]
    public void Experience_command_previews_applies_and_round_trips_history()
    {
        var context = new PlaceEntityTestContext();
        var area = context.State.Project.Areas[0];
        var profile = AreaExperienceRuntimeServiceTests.Profile();
        profile = new(profile.Id == area.Id.Value ? profile.Id : area.Id.Value, profile.SchemaVersion,
            profile.AudioCues, profile.ScatterEmitters, profile.Voices, profile.PatrolRoutes, profile.Loading, "test");
        var handler = new SetAreaExperienceProfileHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetAreaExperienceProfileCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            area.Id, profile, true);

        var preview = handler.Handle(context.State, command);
        Assert.True(preview.Succeeded);
        Assert.Contains("experience", preview.Patch!.Path);
        var applied = handler.Handle(context.State, command with { DryRun = false });
        Assert.True(applied.Succeeded);
        Assert.Single(applied.State.Project.Areas[0].RuntimePolicies.Experience.ScatterEmitters);
        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Areas[0].RuntimePolicies.Experience.ScatterEmitters);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Areas[0].RuntimePolicies.Experience.ScatterEmitters);
    }
}
