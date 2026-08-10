using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Areas;

public sealed class AreaWorkflowTests
{
    private static readonly BlueprintId SpawnBlueprintId =
        BlueprintId.From(Guid.Parse("d7bd1673-bca1-485b-a1c0-66cb265fe1a8"));
    private static readonly AreaId EntryAreaId =
        AreaId.From(Guid.Parse("db217d16-90ee-48ef-83eb-25e6d70af007"));
    private static readonly EntityId EntryAnchorId =
        EntityId.From(Guid.Parse("f5a24df7-20dd-4e72-8d00-d358acc2533e"));
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 2, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateArea_CreatesPlayableIndependentAreaAndSupportsHistory()
    {
        var state = CreateState();
        var command = CreateAreaCommand(state.Revision);
        var handler = new CreateAreaHandler(new FixedClock(Timestamp));

        var dryRun = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(dryRun.Succeeded);
        Assert.False(dryRun.Audit.Applied);
        Assert.Single(dryRun.State.Project.Areas);
        Assert.True(applied.Succeeded);
        var area = Assert.Single(applied.State.Project.Areas, candidate => candidate.Id == command.AreaId);
        Assert.Equal("forgotten-forest", area.Slug);
        Assert.Equal(AreaKind.Exterior, area.Kind);
        Assert.Equal(new AreaDimensions(128, 96), area.Dimensions);
        Assert.Equal("forge-generic", area.Foundation.Creation.KitKey);
        Assert.Equal("heightmap", area.Foundation.Creation.TerrainProfile);
        Assert.Equal(command.SpawnEntityId, Assert.Single(area.Entities).Id);
        Assert.True(history.Undo().Succeeded);
        Assert.Single(history.State.Project.Areas);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(2, history.State.Project.Areas.Count);
    }

    [Fact]
    public void CreateArea_RejectsDuplicateSlugAndMissingPlayableTemplateData()
    {
        var state = CreateState();
        var handler = new CreateAreaHandler(new FixedClock(Timestamp));

        var duplicate = handler.Handle(
            state,
            CreateAreaCommand(state.Revision) with { DisplayName = "Entry area" });
        var missingSpawn = handler.Handle(
            state,
            CreateAreaCommand(state.Revision) with
            {
                SpawnBlueprintId = null,
                SpawnEntityId = null,
            });

        Assert.Equal(CreateAreaHandler.DuplicateSlugCode, Assert.Single(duplicate.Diagnostics).Code);
        Assert.Equal(CreateAreaHandler.SpawnRequiredCode, Assert.Single(missingSpawn.Diagnostics).Code);
    }

    [Fact]
    public void UpsertPortal_ValidatesDestinationAndRoundTripsThroughUndoRedo()
    {
        var initial = CreateState();
        var create = new CreateAreaHandler(new FixedClock(Timestamp)).Handle(
            initial,
            CreateAreaCommand(initial.Revision));
        var state = create.State;
        var destination = state.Project.Areas.Single(area => area.Id != EntryAreaId);
        var destinationAnchor = Assert.Single(destination.Entities);
        var portal = new PortalLink(
            PortalId.From(Guid.Parse("1a9db8c3-5178-46b3-899b-0b1649852bec")),
            "Vers Forgotten Forest",
            EntryAnchorId,
            destination.Id,
            destinationAnchor.Id,
            bidirectional: true,
            preloadDestination: true);
        var command = new UpsertPortalLinkCommand(
            CommandId.From(Guid.Parse("25bd5055-a438-4de5-9e19-80aafbb605a8")),
            state.Revision,
            EntryAreaId,
            portal,
            DryRun: false);
        var handler = new UpsertPortalLinkHandler(new FixedClock(Timestamp));

        var applied = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(applied.Succeeded);
        Assert.Equal(portal, Assert.Single(applied.State.Project.Areas.Single(area => area.Id == EntryAreaId).Portals));
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Areas.Single(area => area.Id == EntryAreaId).Portals);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Areas.Single(area => area.Id == EntryAreaId).Portals);

        var invalid = handler.Handle(
            state,
            command with
            {
                Portal = new PortalLink(
                    PortalId.From(Guid.Parse("4983c113-5d19-47fa-995f-0d4d0ca6f651")),
                    "Broken",
                    EntryAnchorId,
                    AreaId.From(Guid.Parse("8489266b-d93e-4d9a-a736-75fcf79dd2c9")),
                    destinationAnchor.Id,
                    false,
                    false),
            });
        Assert.Equal(
            Forge.Domain.Validation.ProjectValidator.MissingPortalDestinationAreaCode,
            Assert.Single(invalid.Diagnostics).Code);
    }

    private static ProjectState CreateState()
    {
        var blueprint = new BlueprintDefinition(
            SpawnBlueprintId,
            "player-spawn",
            "Player spawn",
            BlueprintKind.PlayerSpawn);
        var anchor = new EntityInstance(EntryAnchorId, blueprint.Id, TransformValue.Identity, ["area-anchor"]);
        var entry = new AreaDefinition(EntryAreaId, "entry-area", "Entry area", [anchor]);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("8a333eb2-27d4-443d-a9bb-cdd6dc743b81")),
            "Area workflow",
            [blueprint],
            [entry]);
        return new ProjectState(project, new ProjectRevision(3));
    }

    private static CreateAreaCommand CreateAreaCommand(ProjectRevision revision) =>
        new(
            CommandId.From(Guid.Parse("860e1eec-73b3-4be9-8c60-708088132cd7")),
            revision,
            AreaId.From(Guid.Parse("e7947f26-f6d7-4acf-8973-c15cb523ea6c")),
            "Forgotten Forest",
            AreaKind.Exterior,
            new AreaDimensions(128, 96),
            AreaTemplateKind.Playable,
            SpawnBlueprintId,
            EntityId.From(Guid.Parse("eb00a97f-4417-483e-a6a0-fd6efe4bf0dd")),
            DryRun: false);
}
