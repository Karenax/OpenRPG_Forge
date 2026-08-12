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
}
