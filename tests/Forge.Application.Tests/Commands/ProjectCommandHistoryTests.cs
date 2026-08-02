using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;

namespace Forge.Application.Tests.Commands;

public sealed class ProjectCommandHistoryTests
{
    [Fact]
    public void UndoAndRedoRestoreCommandStatesWithMonotonicRevisions()
    {
        var context = new Entities.PlaceEntityTestContext();
        var history = new ProjectCommandHistory(context.State);
        history.Record(context.Handler.Handle(context.State, context.CreateCommand()));

        var undo = history.Undo();
        var redo = history.Redo();

        Assert.True(undo.Succeeded);
        Assert.Empty(Assert.Single(undo.State.Project.Areas).Entities);
        Assert.Equal(new ProjectRevision(9), undo.State.Revision);
        Assert.Equal("remove", undo.Patch?.Operation);
        Assert.True(redo.Succeeded);
        Assert.Single(Assert.Single(redo.State.Project.Areas).Entities);
        Assert.Equal(new ProjectRevision(10), redo.State.Revision);
        Assert.Equal("add", redo.Patch?.Operation);
    }

    [Fact]
    public void RecordingACommandAfterUndoClearsRedoHistory()
    {
        var context = new Entities.PlaceEntityTestContext();
        var history = new ProjectCommandHistory(context.State);
        history.Record(context.Handler.Handle(context.State, context.CreateCommand()));
        history.Undo();
        var replacement = context.Handler.Handle(
            history.State,
            context.CreateCommand(revision: history.State.Revision));

        history.Record(replacement);

        Assert.False(history.CanRedo);
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void RecordRejectsADryRun()
    {
        var context = new Entities.PlaceEntityTestContext();
        var history = new ProjectCommandHistory(context.State);
        var dryRun = context.Handler.Handle(context.State, context.CreateCommand(dryRun: true));

        Assert.Throws<InvalidOperationException>(() => history.Record(dryRun));
        Assert.Same(context.State, history.State);
    }
}
