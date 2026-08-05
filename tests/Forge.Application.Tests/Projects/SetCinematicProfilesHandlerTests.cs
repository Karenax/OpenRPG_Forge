using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Projects;

public sealed class SetCinematicProfilesHandlerTests
{
    [Fact]
    public void Combined_command_previews_applies_and_is_one_undoable_transaction()
    {
        var context = new PlaceEntityTestContext();
        var area = context.State.Project.Areas.Single();
        var anchor = new CameraAnchorDefinition(Guid.NewGuid(), "Intro",
            new TransformValue(new Vector3Value(1, 4, 6), QuaternionValue.Identity, Vector3Value.One),
            Vector3Value.Zero, CameraFraming.Wide, 55, 2_000, .08f, ["intro"], "test");
        var areaProfile = new AreaCinematicProfile(area.Id.Value, 1, [anchor], "test");
        var sequence = new SequenceDefinition(Guid.NewGuid(), 1, "intro", "Intro", 2_000, true, false,
            [new SequenceTrackDefinition(Guid.NewGuid(), "Camera", SequenceTrackKind.Camera,
                SequenceTargetKind.CameraAnchor, anchor.Id, string.Empty,
                [new SequenceKeyframe(Guid.NewGuid(), 0, null, string.Empty, SequenceEasing.Linear)], "test")],
            [], "test");
        var startup = new StartupFlowDefinition(Guid.NewGuid(), 1, StartupFlowKind.Sequence, sequence.Id,
            string.Empty, StartupFlowKind.None, null, string.Empty, true, "Passer", "test");
        var moduleProfile = new ModuleCinematicProfile(context.State.Project.Module.Id.Value, 1, startup,
            [sequence], "test");
        var handler = new SetCinematicProfilesHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetCinematicProfilesCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            moduleProfile, area.Id, areaProfile, true);

        var preview = handler.Handle(context.State, command);
        Assert.True(preview.Succeeded);
        Assert.Null(preview.NewRevision);
        Assert.Equal("runtimePolicies/cinematics", preview.Patch!.Path);
        var applied = handler.Handle(context.State, command with { DryRun = false });
        Assert.True(applied.Succeeded);
        Assert.Single(applied.State.Project.Module.RuntimePolicies.Cinematics.Sequences);
        Assert.Single(applied.State.Project.Areas.Single().RuntimePolicies.Cinematics.CameraAnchors);

        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.RuntimePolicies.Cinematics.Sequences);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Module.RuntimePolicies.Cinematics.Sequences);
    }
}
