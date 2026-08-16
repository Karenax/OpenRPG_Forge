using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Creator;

public sealed class SemanticClipboardWorkflowTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 2, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CrossAreaCopyUsesCanonicalPasteAndProtectsTheSourceDocument()
    {
        var context = CreateContext();
        var request = new CrossAreaCopyRequest(Guid.NewGuid(), CrossAreaCopyRequest.CurrentSchemaVersion,
            "test:r059-cross-area", context.SourceArea.Id, context.TargetArea.Id,
            [context.SourceArea.Entities[0].Id], new Vector3Value(8, 0, 8));
        var service = new CrossAreaCopyService(new FixedClock(Timestamp));

        var dryRun = service.Copy(context.State, request with { DryRun = true });
        var applied = service.Copy(context.State, request);

        Assert.True(dryRun.Succeeded);
        Assert.Equal(context.SourceArea.Entities.Count,
            dryRun.State.Project.Areas.Single(area => area.Id == context.SourceArea.Id).Entities.Count);
        Assert.True(applied.Succeeded);
        Assert.Equal(context.SourceArea.Entities.Count,
            applied.State.Project.Areas.Single(area => area.Id == context.SourceArea.Id).Entities.Count);
        Assert.Equal(context.TargetArea.Entities.Count + 1,
            applied.State.Project.Areas.Single(area => area.Id == context.TargetArea.Id).Entities.Count);
        Assert.Equal(CrossAreaCopyService.SameAreaCode,
            Assert.Single(service.Copy(context.State, request with { TargetAreaId = context.SourceArea.Id }).Diagnostics).Code);
    }

    [Fact]
    public void SemanticClipboard_CutIsAtomicVersionedAndUndoable()
    {
        var context = CreateContext();
        var source = context.SourceArea.WithPortals([]);
        var project = new ProjectDefinition(context.State.Project.Id, context.State.Project.Title,
            context.State.Project.Module, context.State.Project.Blueprints,
            [source, context.TargetArea], context.State.Project.Dialogues, context.State.Project.Quests);
        var state = new ProjectState(project, context.State.Revision);
        var command = new CutEntitiesCommand(CommandId.From(Guid.NewGuid()), CutEntitiesHandler.CurrentSchemaVersion,
            "test:r034-cut", state.Revision, source.Id, source.Entities.Select(item => item.Id).ToArray(), DryRun: false);
        var handler = new CutEntitiesHandler(new FixedClock(Timestamp));

        var dryRun = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied.Command);

        Assert.True(dryRun.Command.Succeeded);
        Assert.Equal(2, dryRun.Payload!.Entities.Count);
        Assert.Equal(2, dryRun.Command.State.Project.Areas.Single(item => item.Id == source.Id).Entities.Count);
        Assert.True(applied.Command.Succeeded);
        Assert.True(applied.Payload!.IsCut);
        Assert.NotEqual(Guid.Empty, applied.Payload.Id);
        Assert.Equal("test:r034-cut", applied.Payload.Provenance);
        Assert.Empty(applied.Command.State.Project.Areas.Single(item => item.Id == source.Id).Entities);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(2, history.State.Project.Areas.Single(item => item.Id == source.Id).Entities.Count);
        Assert.True(history.Redo().Succeeded);
        Assert.Empty(history.State.Project.Areas.Single(item => item.Id == source.Id).Entities);
    }

    [Fact]
    public void SemanticClipboard_IndependentCopyRemapsBlueprintUuid()
    {
        var context = CreateContext();
        var payload = new SemanticClipboard().CopyEntities(context.State.Project, context.SourceArea.Id,
            context.SourceArea.Entities.Select(item => item.Id).ToArray(), "test:r034-copy");
        var entityMap = payload.Entities.ToDictionary(item => item.Id, _ => EntityId.From(Guid.NewGuid()));
        var blueprintMap = payload.Entities.Select(item => item.BlueprintId).Distinct()
            .ToDictionary(id => id, _ => BlueprintId.From(Guid.NewGuid()));
        var command = new PasteEntitiesCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.TargetArea.Id, payload, entityMap, new Vector3Value(8, 0, 8), DryRun: false,
            Mode: SemanticPasteMode.IndependentCopy, BlueprintIdMap: blueprintMap);

        var result = new PasteEntitiesHandler(new FixedClock(Timestamp)).Handle(context.State, command);

        Assert.True(result.Succeeded);
        var copiedBlueprint = Assert.Single(result.State.Project.Blueprints,
            item => blueprintMap.Values.Contains(item.Id));
        Assert.Equal(BlueprintPaletteOrigin.ProjectContent, copiedBlueprint.PaletteOrigin);
        Assert.Contains("Independent clipboard copy", copiedBlueprint.Provenance.Modifications.Single());
        Assert.All(result.State.Project.Areas.Single(item => item.Id == context.TargetArea.Id).Entities
            .Where(item => entityMap.Values.Contains(item.Id)), item => Assert.Equal(copiedBlueprint.Id, item.BlueprintId));
    }

    [Fact]
    public void EntityClipboard_PastesSemanticCopiesAndSupportsDryRunUndoRedo()
    {
        var context = CreateContext();
        var clipboard = new SemanticClipboard();
        var payload = clipboard.CopyEntities(
            context.State.Project,
            context.SourceArea.Id,
            context.SourceArea.Entities.Select(entity => entity.Id).ToArray());
        var mappings = payload.Entities.ToDictionary(
            entity => entity.Id,
            entity => EntityId.From(Guid.NewGuid()));
        var command = new PasteEntitiesCommand(
            CommandId.From(Guid.Parse("57c5e154-6539-427e-a145-88c2eb89a023")),
            context.State.Revision,
            context.TargetArea.Id,
            payload,
            mappings,
            new Vector3Value(8, 0, 8),
            DryRun: false);
        var handler = new PasteEntitiesHandler(new FixedClock(Timestamp));

        var dryRun = handler.Handle(context.State, command with { DryRun = true });
        var applied = handler.Handle(context.State, command);
        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);

        Assert.True(clipboard.HasContent);
        Assert.Equal(SemanticClipboardKind.Entities, payload.Kind);
        Assert.True(dryRun.Succeeded);
        Assert.False(dryRun.Audit.Applied);
        Assert.Single(dryRun.State.Project.Areas.Single(area => area.Id == context.TargetArea.Id).Entities);
        Assert.True(applied.Succeeded);
        var pasted = applied.State.Project.Areas.Single(area => area.Id == context.TargetArea.Id).Entities
            .Where(entity => mappings.Values.Contains(entity.Id))
            .ToArray();
        Assert.Equal(2, pasted.Length);
        Assert.All(pasted, entity => Assert.Contains(entity.Id, mappings.Values));
        Assert.Equal(
            context.SourceArea.Entities[0].Overrides,
            pasted.Single(entity => entity.Id == mappings[context.SourceArea.Entities[0].Id]).Overrides);
        Assert.Equal(
            context.SourceArea.Entities[0].Tags,
            pasted.Single(entity => entity.Id == mappings[context.SourceArea.Entities[0].Id]).Tags);
        Assert.True(history.Undo().Succeeded);
        Assert.Single(history.State.Project.Areas.Single(area => area.Id == context.TargetArea.Id).Entities);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(3, history.State.Project.Areas.Single(area => area.Id == context.TargetArea.Id).Entities.Count);
    }

    [Fact]
    public void EntityClipboard_RejectsIncompleteMappingAndOccupiedDestination()
    {
        var context = CreateContext();
        var payload = new SemanticClipboard().CopyEntities(
            context.State.Project,
            context.SourceArea.Id,
            context.SourceArea.Entities.Select(entity => entity.Id).ToArray());
        var handler = new PasteEntitiesHandler(new FixedClock(Timestamp));
        var incomplete = new PasteEntitiesCommand(
            CommandId.From(Guid.Parse("2412ab55-c80f-4ee4-a9d8-aa23fd63939f")),
            context.State.Revision,
            context.TargetArea.Id,
            payload,
            new Dictionary<EntityId, EntityId>
            {
                [payload.Entities[0].Id] = EntityId.From(Guid.NewGuid()),
            },
            Vector3Value.Zero,
            DryRun: false);
        var occupied = incomplete with
        {
            EntityIdMap = payload.Entities.ToDictionary(
                entity => entity.Id,
                entity => EntityId.From(Guid.NewGuid())),
        };

        Assert.Equal(
            PasteEntitiesHandler.InvalidMappingCode,
            Assert.Single(handler.Handle(context.State, incomplete).Diagnostics).Code);
        Assert.Equal(
            Forge.Application.Spatial.PlacementValidator.OccupiedCode,
            Assert.Single(handler.Handle(context.State, occupied).Diagnostics).Code);
    }

    [Fact]
    public void EntityClipboard_RejectsMissingSocketAndCumulativeBudgetWithoutMutation()
    {
        var context = CreateContext();
        var payload = new SemanticClipboard().CopyEntities(
            context.State.Project,
            context.SourceArea.Id,
            context.SourceArea.Entities.Select(entity => entity.Id).ToArray());
        var mappings = payload.Entities.ToDictionary(
            entity => entity.Id,
            _ => EntityId.From(Guid.NewGuid()));
        var baseCommand = new PasteEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            context.State.Revision,
            context.TargetArea.Id,
            payload,
            mappings,
            new Vector3Value(8, 0, 8),
            DryRun: false);
        var handler = new PasteEntitiesHandler(new FixedClock(Timestamp));

        var missingSocket = handler.Handle(context.State, baseCommand with
        {
            RequiredSocket = "wall-north",
            AvailableSockets = ["floor"],
        });
        var exhaustedBudget = handler.Handle(context.State, baseCommand with
        {
            AvailableBudgetBytes = 3_000,
            BlueprintEstimatedBytes = 2_000,
        });

        Assert.Equal(Forge.Application.Spatial.PlacementValidator.SocketMissingCode,
            Assert.Single(missingSocket.Diagnostics).Code);
        Assert.Equal(Forge.Application.Spatial.PlacementValidator.BudgetExceededCode,
            Assert.Single(exhaustedBudget.Diagnostics).Code);
        Assert.Single(missingSocket.State.Project.Areas.Single(area => area.Id == context.TargetArea.Id).Entities);
        Assert.Single(exhaustedBudget.State.Project.Areas.Single(area => area.Id == context.TargetArea.Id).Entities);
    }

    [Fact]
    public void DuplicateArea_FullCloneRemapsInternalReferencesAndPreservesExternalReferences()
    {
        var context = CreateContext();
        var destinationAreaId = AreaId.From(Guid.Parse("e730d32b-7bd6-4f20-976a-12273dd6a7d9"));
        var entityMap = context.SourceArea.Entities.Select(entity =>
            new EntityIdRemap(entity.Id, EntityId.From(Guid.NewGuid()))).ToArray();
        var portalMap = context.SourceArea.Portals.Select(portal =>
            new PortalIdRemap(portal.Id, PortalId.From(Guid.NewGuid()))).ToArray();
        var command = new DuplicateAreaCommand(
            CommandId.From(Guid.Parse("75b31a5e-1f8b-4cf5-a37a-e3530a5cdd24")),
            context.State.Revision,
            context.SourceArea.Id,
            destinationAreaId,
            "Source copy",
            AreaCloneMode.Full,
            ExternalPortalPolicy.Preserve,
            entityMap,
            portalMap,
            DryRun: false);
        var handler = new DuplicateAreaHandler(new FixedClock(Timestamp));

        var dryRun = handler.Handle(context.State, command with { DryRun = true });
        var applied = handler.Handle(context.State, command);
        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);

        Assert.True(dryRun.Succeeded);
        Assert.False(dryRun.Audit.Applied);
        var clone = Assert.Single(applied.State.Project.Areas, area => area.Id == destinationAreaId);
        Assert.Equal(2, clone.Entities.Count);
        Assert.Equal(2, clone.Portals.Count);
        var internalPortal = Assert.Single(clone.Portals, portal => portal.DestinationAreaId == clone.Id);
        var externalPortal = Assert.Single(clone.Portals, portal => portal.DestinationAreaId == context.TargetArea.Id);
        Assert.Contains(internalPortal.SourceEntityId, entityMap.Select(item => item.DestinationId));
        Assert.Contains(internalPortal.DestinationEntityId, entityMap.Select(item => item.DestinationId));
        Assert.Equal(context.TargetArea.Entities[0].Id, externalPortal.DestinationEntityId);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(2, history.State.Project.Areas.Count);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(3, history.State.Project.Areas.Count);
    }

    [Fact]
    public void DuplicateArea_TemplateDropsContentAndInvalidMappingIsRejected()
    {
        var context = CreateContext();
        var command = new DuplicateAreaCommand(
            CommandId.From(Guid.Parse("69d52111-6000-4fbf-a686-cbf0aaffb23b")),
            context.State.Revision,
            context.SourceArea.Id,
            AreaId.From(Guid.Parse("3ebcb282-8031-4e2d-854d-889deff6d27f")),
            "Source template",
            AreaCloneMode.Template,
            ExternalPortalPolicy.Drop,
            [],
            [],
            DryRun: false);
        var handler = new DuplicateAreaHandler(new FixedClock(Timestamp));

        var result = handler.Handle(context.State, command);
        var invalid = handler.Handle(
            context.State,
            command with
            {
                DestinationAreaId = AreaId.From(Guid.NewGuid()),
                DisplayName = "Invalid full copy",
                Mode = AreaCloneMode.Full,
            });

        var template = Assert.Single(result.State.Project.Areas, area => area.Id == command.DestinationAreaId);
        Assert.Empty(template.Entities);
        Assert.Empty(template.Portals);
        Assert.Equal(context.SourceArea.Kind, template.Kind);
        Assert.Equal(context.SourceArea.Dimensions, template.Dimensions);
        Assert.Equal(
            DuplicateAreaHandler.InvalidEntityMappingCode,
            Assert.Single(invalid.Diagnostics).Code);
    }

    [Fact]
    public void AreaDuplicationPreview_ReportsOnlyContentThatWillBeCloned()
    {
        var source = CreateContext().SourceArea;

        var full = AreaDuplicationPreview.Create(source, AreaCloneMode.Full, ExternalPortalPolicy.Drop);
        var template = AreaDuplicationPreview.Create(source, AreaCloneMode.Template, ExternalPortalPolicy.Preserve);

        Assert.Equal(2, full.EntityCount);
        Assert.Equal(1, full.InternalPortalCount);
        Assert.Equal(0, full.ExternalPortalCount);
        Assert.Equal(0, template.EntityCount);
        Assert.Equal(0, template.InternalPortalCount);
        Assert.Equal(0, template.ExternalPortalCount);
    }

    [Fact]
    public void R054AreaCloneCopiesTerrainNavigationAndEnvironmentWithNewCanonicalIdentity()
    {
        var context = CreateContext();
        var destination = AreaId.From(Guid.Parse("a0542000-0000-4000-8000-000000000001"));
        var entityMap = context.SourceArea.Entities.Select(entity =>
            new EntityIdRemap(entity.Id, EntityId.From(Guid.NewGuid()))).ToArray();
        var portalMap = context.SourceArea.Portals.Select(portal =>
            new PortalIdRemap(portal.Id, PortalId.From(Guid.NewGuid()))).ToArray();
        var command = new DuplicateAreaCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.SourceArea.Id, destination, "R054 complete copy", AreaCloneMode.Full,
            ExternalPortalPolicy.Preserve, entityMap, portalMap, DryRun: false);

        var result = new DuplicateAreaHandler(new FixedClock(Timestamp)).Handle(context.State, command);
        var clone = result.State.Project.Areas.Single(item => item.Id == destination);
        var preview = AreaDuplicationPreview.Create(context.SourceArea, AreaCloneMode.Full,
            ExternalPortalPolicy.Preserve);

        Assert.True(result.Succeeded);
        Assert.True(preview.TerrainCopied);
        Assert.True(preview.NavigationCopied);
        Assert.True(preview.EnvironmentCopied);
        Assert.NotEqual(context.SourceArea.World.Id, clone.World.Id);
        Assert.NotEqual(context.SourceArea.World.Terrain.Id, clone.World.Terrain.Id);
        Assert.NotEqual(context.SourceArea.Navigation.Id, clone.Navigation.Id);
        Assert.NotEqual(context.SourceArea.Environment.Authoring.Id, clone.Environment.Authoring.Id);
        Assert.Equal(context.SourceArea.Environment.Lighting, clone.Environment.Lighting);
        Assert.Contains("terrain=True", result.Patch!.Summary, StringComparison.Ordinal);
        Assert.Contains("shared-assets=", result.Patch.Summary, StringComparison.Ordinal);
    }

    private static ClipboardTestContext CreateContext()
    {
        var blueprintId = BlueprintId.From(Guid.Parse("5516632c-c30a-41a3-bb24-54fb653f4b6f"));
        var blueprint = new BlueprintDefinition(blueprintId, "stone-marker", "Stone marker", BlueprintKind.Placeable);
        var sourceAreaId = AreaId.From(Guid.Parse("fb370a1a-191c-464b-a767-a75e6873889a"));
        var first = new EntityInstance(
            EntityId.From(Guid.Parse("3bd3d942-a9ef-4649-99f6-eb4b41230f56")),
            blueprintId,
            new TransformValue(new Vector3Value(-2, 0, -2), QuaternionValue.Identity, Vector3Value.One),
            ["marker"],
            new EntityPropertyOverrides(
                EntityPropertyOverrides.CurrentSchemaVersion,
                [new EntityPropertyOverride("label", EntityPropertyValueKind.String, "A")]));
        var second = new EntityInstance(
            EntityId.From(Guid.Parse("01ee43a9-f6b4-4304-a9d0-35036a83fa42")),
            blueprintId,
            new TransformValue(new Vector3Value(2, 0, -2), QuaternionValue.Identity, Vector3Value.One));
        var targetAreaId = AreaId.From(Guid.Parse("7d85ae50-2f17-4440-871d-e00bd7574d97"));
        var targetAnchor = new EntityInstance(
            EntityId.From(Guid.Parse("c886e32c-7ee2-4d74-a49a-c3649364e105")),
            blueprintId,
            new TransformValue(new Vector3Value(-2, 0, -2), QuaternionValue.Identity, Vector3Value.One));
        var internalPortal = new PortalLink(
            PortalId.From(Guid.Parse("0eb0807d-37f9-4e7c-9c9b-f08a889f958e")),
            "Internal",
            first.Id,
            sourceAreaId,
            second.Id,
            true,
            false);
        var externalPortal = new PortalLink(
            PortalId.From(Guid.Parse("53174703-38e1-4d1e-8f80-b9b38df8d0e6")),
            "External",
            second.Id,
            targetAreaId,
            targetAnchor.Id,
            false,
            true);
        var source = new AreaDefinition(
            sourceAreaId,
            "source",
            "Source",
            [first, second],
            AreaKind.Exterior,
            new AreaDimensions(40, 40),
            [internalPortal, externalPortal]);
        var target = new AreaDefinition(
            targetAreaId,
            "target",
            "Target",
            [targetAnchor],
            AreaKind.Interior,
            new AreaDimensions(40, 40));
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("1e39e49f-e4c9-4513-9a78-b20f602ca0be")),
            "Clipboard project",
            [blueprint],
            [source, target]);
        return new ClipboardTestContext(new ProjectState(project, new ProjectRevision(4)), source, target);
    }

    private sealed record ClipboardTestContext(ProjectState State, AreaDefinition SourceArea, AreaDefinition TargetArea);
}
