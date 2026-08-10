using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;

namespace Forge.Application.Tests.Areas;

public sealed class SetAreaNavigationHandlerTests
{
    [Fact]
    public void Handle_SupportsDryRunApplyUndoRedoAndRevisionValidation()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("72000000-0000-4000-8000-000000000001")),
            "area",
            "Area",
            []);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("72000000-0000-4000-8000-000000000002")),
            "Navigation command",
            [],
            [area]);
        var state = new ProjectState(project, new ProjectRevision(4));
        var definition = new AnchorDefinition(Guid.Parse("72000000-0000-4000-8000-000000000004"), 1,
            "test:r009", "waypoint", new LocalizedText("anchor.waypoint", "Waypoint"), AnchorKind.Waypoint,
            AnchorScope.Area, ["quest"], null, null, false);
        var instance = new AnchorInstance(Guid.Parse("72000000-0000-4000-8000-000000000005"), 1,
            "test:r009", definition.Id, Forge.Domain.Spatial.TransformValue.Identity, 1,
            EntityPropertyOverrides.Empty, null, null);
        var navigation = new AreaNavigationDefinition(
            area.Navigation.Id,
            0.5f,
            area.Navigation.AgentProfiles,
            area.Navigation.OffMeshLinks,
            "creator-command",
            anchors: new AnchorCatalog(Guid.Parse("72000000-0000-4000-8000-000000000006"), 1,
                "test:r009", area.Id, false, [definition], [instance]));
        var command = new SetAreaNavigationCommand(
            CommandId.From(Guid.Parse("72000000-0000-4000-8000-000000000003")),
            state.Revision,
            area.Id,
            navigation,
            false);
        var handler = new SetAreaNavigationHandler(new FixedClock(
            new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero)));

        var dryRun = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var conflict = handler.Handle(state, command with { BaseRevision = state.Revision.Next() });
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(dryRun.Succeeded);
        Assert.Same(state, dryRun.State);
        Assert.Equal(0.5f, applied.State.Project.Areas[0].Navigation.CellSize);
        Assert.Single(applied.State.Project.Areas[0].Navigation.Anchors.Instances);
        Assert.Contains("1 anchor instance", applied.Patch!.Summary, StringComparison.Ordinal);
        Assert.Equal(SetAreaNavigationHandler.RevisionConflictCode, Assert.Single(conflict.Diagnostics).Code);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(1f, history.State.Project.Areas[0].Navigation.CellSize);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(0.5f, history.State.Project.Areas[0].Navigation.CellSize);
    }
}
