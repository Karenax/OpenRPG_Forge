using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
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
}
