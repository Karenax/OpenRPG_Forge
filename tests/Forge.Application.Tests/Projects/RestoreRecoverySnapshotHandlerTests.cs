using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Application.Tests.Projects;

public sealed class RestoreRecoverySnapshotHandlerTests
{
    [Fact]
    public void Selective_restore_supports_dry_run_apply_undo_and_redo()
    {
        var context = new PlaceEntityTestContext();
        var snapshot = context.State.Project;
        var currentProject = new ProjectDefinition(snapshot.Id, "Unsaved title", snapshot.Module,
            snapshot.Blueprints, snapshot.Areas, snapshot.Dialogues, snapshot.Quests);
        var state = new ProjectState(currentProject, new ProjectRevision(4));
        var selection = new RecoveryRestoreSelection(
            Guid.Parse("e0600000-0000-4000-8000-000000000010"), 1,
            RecoveryRestoreSection.ProjectAndModule, "test:r060");
        var handler = new RestoreRecoverySnapshotHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new RestoreRecoverySnapshotCommand(CommandId.From(Guid.NewGuid()), state.Revision,
            snapshot, selection, true);

        var preview = handler.Handle(state, command);
        Assert.True(preview.Succeeded);
        Assert.Equal("project/recovery", preview.Patch!.Path);
        Assert.Same(state, preview.State);

        var applied = handler.Handle(state, command with { DryRun = false });
        Assert.True(applied.Succeeded);
        Assert.Equal(snapshot.Title, applied.State.Project.Title);
        Assert.Equal(currentProject.Areas.Select(area => area.Id), applied.State.Project.Areas.Select(area => area.Id));
        var history = new ProjectCommandHistory(state);
        history.Record(applied);
        Assert.Equal("Unsaved title", history.Undo().State.Project.Title);
        Assert.Equal(snapshot.Title, history.Redo().State.Project.Title);
    }

    [Fact]
    public void Restore_rejects_a_snapshot_from_another_project_before_mutation()
    {
        var context = new PlaceEntityTestContext();
        var source = context.State.Project;
        var foreign = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), source.Title, source.Module,
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
        var selection = new RecoveryRestoreSelection(Guid.NewGuid(), 1, RecoveryRestoreSection.All, "test:r060");
        var result = new RestoreRecoverySnapshotHandler(new FixedClock(PlaceEntityTestContext.Timestamp)).Handle(
            context.State, new RestoreRecoverySnapshotCommand(CommandId.From(Guid.NewGuid()),
                context.State.Revision, foreign, selection, false));

        Assert.False(result.Succeeded);
        Assert.Same(context.State, result.State);
        Assert.Equal(RecoveryRestorePlanner.ProjectMismatchCode, Assert.Single(result.Diagnostics).Code);
    }
}
