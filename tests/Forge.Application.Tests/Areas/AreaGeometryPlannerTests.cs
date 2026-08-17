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

public sealed class AreaGeometryPlannerTests
{
    [Fact]
    public void Group_replace_autofit_and_resize_are_deterministic()
    {
        var context = new PlaceEntityTestContext();
        var ids = new[] { EntityId.From(Guid.NewGuid()), EntityId.From(Guid.NewGuid()) };
        var group = AreaGeometryPlanner.ComposeGroup(Vector3Value.Zero,
            [new(context.BlueprintId, Vector3Value.Zero), new(context.BlueprintId, new Vector3Value(2, 0, 0))], ids);
        Assert.Equal(2, group.Count);
        Assert.Equal(context.BlueprintId, AreaGeometryPlanner.ChooseCompatible(context.State.Project, context.BlueprintId, 42, false));
        var replacement = BlueprintId.From(Guid.NewGuid());
        Assert.Equal(replacement, AreaGeometryPlanner.Replace(group, new HashSet<EntityId> { ids[0] }, replacement)[0].BlueprintId);
        var area = new AreaDefinition(context.AreaId, "test", "Test", group, dimensions: new AreaDimensions(8, 8));
        var resize = AreaGeometryPlanner.Resize(area, new AreaDimensions(4, 4), AreaResizeAnchor.Center);
        Assert.Single(resize.Kept);
        Assert.Single(resize.Removed);
    }

    [Fact]
    public void Geometry_edit_is_atomic_and_supports_dry_run()
    {
        var context = new PlaceEntityTestContext();
        var handler = new EditAreaGeometryHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var area = context.State.Project.Areas.Single();
        var command = new EditAreaGeometryCommand(CommandId.From(Guid.NewGuid()), context.State.Revision, area.Id,
            new AreaDimensions(12, 12), area.Entities, "Resize area", DryRun: true);
        var preview = handler.Handle(context.State, command);
        Assert.True(preview.Succeeded);
        Assert.False(preview.Audit.Applied);
        var applied = handler.Handle(context.State, command with { DryRun = false });
        Assert.True(applied.Succeeded);
        var resized = applied.State.Project.Areas.Single();
        Assert.Equal(12, resized.Dimensions.Width);
        Assert.Equal(area.Environment, resized.Environment);
        Assert.Equal(area.Authoring, resized.Authoring);
        Assert.Equal(area.World.Id, resized.World.Id);
        Assert.Equal(12, resized.World.Terrain.Width);
        Assert.Equal(area.World.Terrain.Heights, resized.World.Terrain.Heights);
    }

    [Fact]
    public void R053TileToolsReplaceEraseFillAndPreserveAttachmentsThroughOneCanonicalCommand()
    {
        var context = new PlaceEntityTestContext();
        var replacementBlueprintId = BlueprintId.From(Guid.Parse("a0531000-0000-4000-8000-000000000001"));
        var tileEntityId = EntityId.From(Guid.Parse("a0531000-0000-4000-8000-000000000002"));
        var attachedEntityId = EntityId.From(Guid.Parse("a0531000-0000-4000-8000-000000000003"));
        var definitionId = Guid.Parse("a0531000-0000-4000-8000-000000000004");
        var replacementDefinitionId = Guid.Parse("a0531000-0000-4000-8000-000000000005");
        var placementId = Guid.Parse("a0531000-0000-4000-8000-000000000006");
        var definitions = new[]
        {
            new ModularTileDefinition(definitionId, "floor", context.BlueprintId,
                ModularTileRotationMode.Orthogonal, true, true, [], []),
            new ModularTileDefinition(replacementDefinitionId, "wall", replacementBlueprintId,
                ModularTileRotationMode.Orthogonal, true, true, [], []),
        };
        var placement = new ModularTilePlacement(placementId, definitionId, tileEntityId, 0, 0, 0,
            ModularTileMirrorMode.None, string.Empty, null);
        var editing = AreaTileEditingProfile.CreateDefault(context.AreaId);
        var kit = new ModularTileKitProfile(Guid.Parse("a0531000-0000-4000-8000-000000000007"), 1,
            "test:r053", 1, definitions, [], [placement], [], [], editing);
        var foundation = new AreaFoundationProfile(Guid.Parse("a0531000-0000-4000-8000-000000000008"), 1,
            "test:r053", AreaCreationSettings.CreateDefault(AreaKind.Interior), kit,
            AreaLifecycleBindingProfile.Optional, [], []);
        var area = new AreaDefinition(context.AreaId, "r053", "R053",
            [new(tileEntityId, context.BlueprintId, TransformValue.Identity, ["tile"]),
             new(attachedEntityId, context.BlueprintId, TransformValue.Identity,
                 [$"attached-to:{tileEntityId}"])], foundation: foundation);
        var project = new ProjectDefinition(context.State.Project.Id, "R053",
            [context.State.Project.Blueprints.Single(),
             new BlueprintDefinition(replacementBlueprintId, "wall", "Wall", BlueprintKind.Placeable)], [area]);
        var state = new ProjectState(project, context.State.Revision);

        var replacement = AreaGeometryPlanner.Edit(area, new(AreaTileEditOperationKind.Replace, [placementId],
            replacementDefinitionId, PreserveAttachedObjects: true));
        Assert.Empty(replacement.Diagnostics);
        Assert.Equal(replacementBlueprintId,
            replacement.Entities.Single(item => item.Id == tileEntityId).BlueprintId);
        Assert.Contains(attachedEntityId, replacement.PreservedAttachedEntityIds);

        var handler = new EditAreaGeometryHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new EditAreaGeometryCommand(CommandId.From(Guid.NewGuid()), state.Revision, area.Id,
            area.Dimensions, replacement.Entities, "Replace tile", true, replacement.Foundation);
        Assert.True(handler.Handle(state, command).Succeeded);
        var applied = handler.Handle(state, command with { DryRun = false });
        Assert.True(applied.Succeeded);
        Assert.Equal(replacementBlueprintId,
            applied.State.Project.Areas.Single().Entities.Single(item => item.Id == tileEntityId).BlueprintId);

        var replacedArea = applied.State.Project.Areas.Single();
        var erased = AreaGeometryPlanner.Edit(replacedArea,
            new(AreaTileEditOperationKind.Erase, [placementId], PreserveAttachedObjects: true));
        Assert.DoesNotContain(erased.Entities, item => item.Id == tileEntityId);
        Assert.Contains(erased.Entities, item => item.Id == attachedEntityId);
        Assert.Empty(erased.Foundation.TileKit.Placements);

        var fillPlacement = Guid.Parse("a0531000-0000-4000-8000-000000000009");
        var fillEntity = EntityId.From(Guid.Parse("a0531000-0000-4000-8000-000000000010"));
        var filled = AreaGeometryPlanner.Edit(area, new(AreaTileEditOperationKind.Fill, [],
            FillCells: [new(fillPlacement, fillEntity, definitionId, 1, 0)]));
        Assert.Empty(filled.Diagnostics);
        Assert.Contains(filled.Foundation.TileKit.Placements, item => item.Id == fillPlacement);
        Assert.Contains(filled.Entities, item => item.Id == fillEntity && item.Transform.Position.X == 1);
    }

    [Fact]
    public void R053ResizeCanRepositionPredictedLossesBeforeAtomicApplication()
    {
        var context = new PlaceEntityTestContext();
        var entityId = EntityId.From(Guid.Parse("a0532000-0000-4000-8000-000000000001"));
        var area = new AreaDefinition(context.AreaId, "r053-resize", "R053 resize",
            [new(entityId, context.BlueprintId, new TransformValue(new(3.5f, 0, 0),
                QuaternionValue.Identity, Vector3Value.One))], dimensions: new(8, 8));

        var loss = AreaGeometryPlanner.Resize(area, new(4, 4), AreaResizeAnchor.Center);
        var recovered = AreaGeometryPlanner.Resize(area, new(4, 4), AreaResizeAnchor.Center,
            new Dictionary<EntityId, Vector3Value> { [entityId] = new(1, 0, 0) });

        Assert.Equal(entityId, Assert.Single(loss.Removed));
        Assert.Empty(recovered.Removed);
        Assert.Equal(entityId, Assert.Single(recovered.Repositioned!));
        Assert.Equal(new Vector3Value(1, 0, 0), Assert.Single(recovered.Kept).Transform.Position);
    }

    [Fact]
    public void StudioTilePlannerPlacesTransformsGroupsAutofitsAndEditsPropertiesWithoutLosingNeighbours()
    {
        var context = new PlaceEntityTestContext();
        var variantBlueprintId = BlueprintId.From(Guid.Parse("a0533000-0000-4000-8000-000000000001"));
        var floorDefinitionId = Guid.Parse("a0533000-0000-4000-8000-000000000002");
        var wallDefinitionId = Guid.Parse("a0533000-0000-4000-8000-000000000003");
        var floorSocketId = Guid.Parse("a0533000-0000-4000-8000-000000000004");
        var wallSocketId = Guid.Parse("a0533000-0000-4000-8000-000000000005");
        var variantId = Guid.Parse("a0533000-0000-4000-8000-000000000006");
        var ruleId = Guid.Parse("a0533000-0000-4000-8000-000000000007");
        var firstPlacementId = Guid.Parse("a0533000-0000-4000-8000-000000000008");
        var secondPlacementId = Guid.Parse("a0533000-0000-4000-8000-000000000009");
        var firstEntityId = EntityId.From(Guid.Parse("a0533000-0000-4000-8000-000000000010"));
        var secondEntityId = EntityId.From(Guid.Parse("a0533000-0000-4000-8000-000000000011"));
        var groupId = Guid.Parse("a0533000-0000-4000-8000-000000000012");
        var propertyId = Guid.Parse("a0533000-0000-4000-8000-000000000013");
        var definitions = new[]
        {
            new ModularTileDefinition(floorDefinitionId, "floor", context.BlueprintId,
                ModularTileRotationMode.Orthogonal, true, true,
                [new(floorSocketId, "East", "passage", TransformValue.Identity,
                    ModularTileSocketDirection.East)],
                [new(variantId, "moss", "mossy", variantBlueprintId)]),
            new ModularTileDefinition(wallDefinitionId, "wall", context.BlueprintId,
                ModularTileRotationMode.Orthogonal, false, false,
                [new(wallSocketId, "West", "passage", TransformValue.Identity,
                    ModularTileSocketDirection.West)], []),
        };
        var kit = new ModularTileKitProfile(Guid.Parse("a0533000-0000-4000-8000-000000000014"), 1,
            "test:studio-tiles", 2, definitions,
            [new(ruleId, "passage", "passage", true)], [], [], [],
            AreaTileEditingProfile.CreateDefault(context.AreaId));
        var foundation = new AreaFoundationProfile(Guid.Parse("a0533000-0000-4000-8000-000000000015"), 1,
            "test:studio-tiles", AreaCreationSettings.CreateDefault(AreaKind.Interior), kit,
            AreaLifecycleBindingProfile.Optional, [], []);
        var untouchedEntity = new EntityInstance(EntityId.From(Guid.Parse("a0533000-0000-4000-8000-000000000016")),
            context.BlueprintId, TransformValue.Identity, ["untouched"],
            new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion,
                [new("custom", EntityPropertyValueKind.String, "preserved")]));
        var area = new AreaDefinition(context.AreaId, "studio-tiles", "Studio tiles", [untouchedEntity],
            foundation: foundation);

        var first = AreaTileAuthoringPlanner.UpsertPlacement(area,
            new(firstPlacementId, firstEntityId, floorDefinitionId, 0, 0, 0,
                ModularTileMirrorMode.None, "moss", null));
        Assert.True(first.CanApply);
        var afterFirst = AreaWith(area, first);
        var second = AreaTileAuthoringPlanner.UpsertPlacement(afterFirst,
            new(secondPlacementId, secondEntityId, wallDefinitionId, 1, 0, 0,
                ModularTileMirrorMode.None, string.Empty, null));
        Assert.True(second.CanApply);
        var afterSecond = AreaWith(afterFirst, second);

        var transformed = AreaTileAuthoringPlanner.TransformPlacement(afterSecond, firstPlacementId, 90,
            ModularTileMirrorMode.X, "moss");
        Assert.True(transformed.CanApply);
        Assert.Equal(untouchedEntity,
            transformed.Entities.Single(item => item.Id == untouchedEntity.Id));
        var afterTransform = AreaWith(afterSecond, transformed);

        var group = new ModularTileGroup(groupId, "Passage", [firstPlacementId, secondPlacementId]);
        var editable = new EditableTileGroupProfile(Guid.Parse("a0533000-0000-4000-8000-000000000017"), groupId,
            [new("width", "int", "2")], new(2, 1, 3), [],
            [new("default", [floorDefinitionId, wallDefinitionId])], false);
        var grouped = AreaTileAuthoringPlanner.UpsertGroup(afterTransform, group, editable);
        Assert.True(grouped.CanApply);
        Assert.All(grouped.Foundation.TileKit.Placements, item => Assert.Equal(groupId, item.GroupId));
        var afterGroup = AreaWith(afterTransform, grouped);

        var property = new TileInstancePropertyProfile(propertyId, firstPlacementId, "lit", "idle", true,
            new Dictionary<string, string> { ["surface"] = "stone" },
            new(true, "#fff4cc", 2.5f, 8), true, true, ["north"],
            new Dictionary<string, string> { ["collision.profile"] = "stone" });
        var properties = AreaTileAuthoringPlanner.UpsertProperty(afterGroup, property);
        Assert.True(properties.CanApply);
        Assert.Equal(property,
            Assert.Single(properties.Foundation.TileKit.Editing!.Properties));

        var autoFit = AreaTileAuthoringPlanner.ApplyAutoFit(AreaWith(afterGroup, properties), firstPlacementId);
        Assert.True(autoFit.CanApply);
        Assert.NotNull(autoFit.AutoFit);
        Assert.NotEqual(Guid.Empty, autoFit.AutoFit!.SelectedDefinitionId);

        var removedGroup = AreaTileAuthoringPlanner.RemoveGroup(AreaWith(afterGroup, properties), groupId);
        Assert.True(removedGroup.CanApply);
        Assert.Empty(removedGroup.Foundation.TileKit.Groups);
        Assert.All(removedGroup.Foundation.TileKit.Placements, item => Assert.Null(item.GroupId));
        Assert.Empty(removedGroup.Foundation.TileKit.Editing!.Groups);
    }

    [Fact]
    public void StudioTilePlannerRejectsInvalidRotationMirrorVariantAndMissingReferences()
    {
        var context = new PlaceEntityTestContext();
        var definitionId = Guid.Parse("a0534000-0000-4000-8000-000000000001");
        var placementId = Guid.Parse("a0534000-0000-4000-8000-000000000002");
        var entityId = EntityId.From(Guid.Parse("a0534000-0000-4000-8000-000000000003"));
        var definition = new ModularTileDefinition(definitionId, "locked", context.BlueprintId,
            ModularTileRotationMode.Orthogonal, false, false, [], []);
        var kit = new ModularTileKitProfile(Guid.Parse("a0534000-0000-4000-8000-000000000004"), 1,
            "test:studio-tiles", 1, [definition], [], [], [], [],
            AreaTileEditingProfile.CreateDefault(context.AreaId));
        var foundation = new AreaFoundationProfile(Guid.Parse("a0534000-0000-4000-8000-000000000005"), 1,
            "test:studio-tiles", AreaCreationSettings.CreateDefault(AreaKind.Interior), kit,
            AreaLifecycleBindingProfile.Optional, [], []);
        var area = new AreaDefinition(context.AreaId, "invalid-tiles", "Invalid tiles", [], foundation: foundation);

        var missing = AreaTileAuthoringPlanner.UpsertPlacement(area,
            new(placementId, entityId, Guid.NewGuid(), 0, 0, 0, ModularTileMirrorMode.None, string.Empty, null));
        Assert.False(missing.CanApply);
        Assert.Contains(missing.Diagnostics, item => item.Code == AreaTileAuthoringPlanner.DefinitionMissingCode);

        var invalid = AreaTileAuthoringPlanner.UpsertPlacement(area,
            new(placementId, entityId, definitionId, 0, 0, 45, ModularTileMirrorMode.X, "unknown", null));
        Assert.False(invalid.CanApply);
        Assert.Contains(invalid.Diagnostics, item => item.Code == AreaFoundationValidator.TileCode);
    }

    private static AreaDefinition AreaWith(AreaDefinition source, AreaTileAuthoringPreview preview) => new(
        source.Id, source.Slug, source.DisplayName, preview.Entities, source.Kind, source.Dimensions,
        source.Portals, source.SchemaVersion, source.Navigation, source.Environment, source.Authoring,
        source.World, source.RuntimePolicies, preview.Foundation, source.Playtest);
}
