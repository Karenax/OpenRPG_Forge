using Forge.Application.Commands;
using Forge.Application.Navigation;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Navigation;

public sealed class SetNavigationTopologyHandlerTests
{
    [Fact]
    public void Command_applies_navigation_and_portal_removal_as_one_history_entry()
    {
        var data = CreateProject();
        var state = new ProjectState(data.Project, new ProjectRevision(7));
        var navigation = new AreaNavigationDefinition(data.Source.Navigation.Id, .5f,
            data.Source.Navigation.AgentProfiles, data.Source.Navigation.OffMeshLinks, "test:ux2",
            anchors: data.Source.Navigation.Anchors, waypoints: data.Source.Navigation.Waypoints);
        var command = new SetNavigationTopologyCommand(
            CommandId.From(Guid.Parse("72020000-0000-4000-8000-000000000030")), state.Revision,
            [new(data.Source.Id, navigation)],
            [NavigationPortalMutation.Remove(data.Source.Id, data.Portal.Id)], null, false);
        var handler = new SetNavigationTopologyHandler(new FixedClock(
            new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero)));

        var preview = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.Same(state, preview.State);
        Assert.True(applied.Succeeded);
        Assert.Equal(8L, applied.NewRevision!.Value.Value);
        Assert.Equal(.5f, applied.State.Project.Areas[0].Navigation.CellSize);
        Assert.Empty(applied.State.Project.Areas[0].Portals);
        Assert.Contains("portal_removals=1", applied.Patch!.Summary, StringComparison.Ordinal);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(1f, history.State.Project.Areas[0].Navigation.CellSize);
        Assert.Single(history.State.Project.Areas[0].Portals);
        Assert.True(history.Redo().Succeeded);
        Assert.Empty(history.State.Project.Areas[0].Portals);
    }

    [Fact]
    public void Command_rejects_stale_revision_and_missing_portal_without_mutation()
    {
        var data = CreateProject();
        var state = new ProjectState(data.Project, new ProjectRevision(2));
        var handler = new SetNavigationTopologyHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var missing = PortalId.From(Guid.Parse("72020000-0000-4000-8000-000000000099"));
        var command = new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()), state.Revision, [],
            [NavigationPortalMutation.Remove(data.Source.Id, missing)], null, false);

        var stale = handler.Handle(state, command with { BaseRevision = state.Revision.Next() });
        var invalid = handler.Handle(state, command);

        Assert.Equal(SetNavigationTopologyHandler.RevisionConflictCode, Assert.Single(stale.Diagnostics).Code);
        Assert.Equal(SetNavigationTopologyHandler.MissingPortalCode, Assert.Single(invalid.Diagnostics).Code);
        Assert.Same(state, stale.State);
        Assert.Same(state, invalid.State);
    }

    private static Data CreateProject()
    {
        var sourceId = AreaId.From(Guid.Parse("72020000-0000-4000-8000-000000000001"));
        var destinationId = AreaId.From(Guid.Parse("72020000-0000-4000-8000-000000000002"));
        var blueprintId = BlueprintId.From(Guid.Parse("72020000-0000-4000-8000-000000000003"));
        var blueprint = new BlueprintDefinition(blueprintId, "portal-marker", "Repère", BlueprintKind.Placeable);
        var sourceEntity = new EntityInstance(EntityId.From(Guid.Parse("72020000-0000-4000-8000-000000000004")),
            blueprintId, TransformValue.Identity);
        var destinationEntity = new EntityInstance(
            EntityId.From(Guid.Parse("72020000-0000-4000-8000-000000000005")), blueprintId,
            TransformValue.Identity);
        var portal = new PortalLink(PortalId.From(Guid.Parse("72020000-0000-4000-8000-000000000006")),
            "Passage", sourceEntity.Id, destinationId, destinationEntity.Id, false, true);
        var source = new AreaDefinition(sourceId, "source", "Source", [sourceEntity], portals: [portal]);
        var destination = new AreaDefinition(destinationId, "destination", "Destination", [destinationEntity]);
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("72020000-0000-4000-8000-000000000010")),
            "Navigation topology", [blueprint], [source, destination]);
        return new(project, source, portal);
    }

    private sealed record Data(ProjectDefinition Project, AreaDefinition Source, PortalLink Portal);
}
