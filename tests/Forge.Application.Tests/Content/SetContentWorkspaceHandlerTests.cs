using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Content;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Content;

public sealed class SetContentWorkspaceHandlerTests
{
    [Fact]
    public void CommandSupportsDryRunRevisionAndUndoRedo()
    {
        var state = CreateState();
        var table = new DataTableDefinition(
            DataTableId.From(Guid.Parse("e2000000-0000-4000-8000-000000000001")),
            "creatures",
            "Creatures",
            [new DataColumnDefinition(
                DataColumnId.From(Guid.Parse("e2000000-0000-4000-8000-000000000002")),
                "name", "Name", DataColumnKind.Text, required: true)],
            [new DataRowDefinition(
                DataRowId.From(Guid.Parse("e2000000-0000-4000-8000-000000000003")),
                new Dictionary<string, string> { ["name"] = "Ash Warden" })]);
        var configuration = new ContentWorkspaceConfiguration(
            ContentWorkspaceConfigurationId.From(state.Project.Module.Id.Value),
            new ContentBatchConfiguration("*.json", [".json"], ContentBatchCollisionPolicy.PreserveBoth, "author", "CC0-1.0"),
            [table],
            ContentMergePolicy.Manual,
            "author-command");
        var command = new SetContentWorkspaceCommand(
            CommandId.From(Guid.Parse("e2000000-0000-4000-8000-000000000004")),
            state.Revision,
            configuration,
            DryRun: false);
        var handler = new SetContentWorkspaceHandler(new FixedClock(DateTimeOffset.UnixEpoch));

        var dryRun = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var stale = handler.Handle(state, command with { BaseRevision = state.Revision.Next() });
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(dryRun.Succeeded);
        Assert.Same(state, dryRun.State);
        Assert.Single(applied.State.Project.Module.Content.DataTables);
        Assert.Equal("command.revision_conflict", Assert.Single(stale.Diagnostics).Code);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.Content.DataTables);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Module.Content.DataTables);
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("e2000000-0000-4000-8000-000000000005")), "start", "Start", []);
        var module = new ModuleDefinition(
            ModuleId.From(Guid.Parse("e2000000-0000-4000-8000-000000000006")),
            "content-module", "Content module", "Test", area.Id);
        return new ProjectState(
            new ProjectDefinition(
                ProjectId.From(Guid.Parse("e2000000-0000-4000-8000-000000000007")),
                "Content", module, [], [area]),
            new ProjectRevision(4));
    }
}
