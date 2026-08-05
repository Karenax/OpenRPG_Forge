using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;

namespace Forge.Application.Tests.Areas;

public sealed class SetAreaWorldProfileHandlerTests
{
    [Fact]
    public void AppliesProfileThroughHistoryAndRestoresItWithUndoRedo()
    {
        var context = new PlaceEntityTestContext();
        var area = context.State.Project.Areas.Single();
        var source = area.World;
        var loading = new AreaLoadingProfile(source.Loading.Id, 1, true, true, true, 1024, 6, 3, 1500,
            ["assets/terrain/grass.glb"], "test");
        var edited = new AreaWorldProfile(source.Id, source.SchemaVersion, source.Terrain, source.Biomes,
            source.Features, source.VolumetricNavigation, source.FlightRoutes, loading, "test");
        var command = new SetAreaWorldProfileCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.AreaId, edited, false);
        var handler = new SetAreaWorldProfileHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var history = new ProjectCommandHistory(context.State);

        history.Record(handler.Handle(history.State, command));

        Assert.Equal(1024, history.State.Project.Areas.Single().World.Loading.CacheBudgetMegabytes);
        Assert.Equal("assets/terrain/grass.glb", Assert.Single(history.State.Project.Areas.Single().World.Loading.RequiredResources));
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(512, history.State.Project.Areas.Single().World.Loading.CacheBudgetMegabytes);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(1024, history.State.Project.Areas.Single().World.Loading.CacheBudgetMegabytes);
    }

    [Fact]
    public void DryRunProducesSemanticPatchWithoutMutation()
    {
        var context = new PlaceEntityTestContext();
        var area = context.State.Project.Areas.Single();
        var command = new SetAreaWorldProfileCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.AreaId, area.World, true);
        var handler = new SetAreaWorldProfileHandler(new FixedClock(PlaceEntityTestContext.Timestamp));

        var result = handler.Handle(context.State, command);

        Assert.True(result.Succeeded);
        Assert.Equal(context.State, result.State);
        Assert.Null(result.NewRevision);
        Assert.Equal($"areas/{context.AreaId}/world", result.Patch!.Path);
        Assert.False(result.Audit.Applied);
    }

    [Fact]
    public void RejectsTerrainWhoseDimensionsDoNotMatchTheArea()
    {
        var context = new PlaceEntityTestContext();
        var area = context.State.Project.Areas.Single();
        var invalid = AreaWorldProfile.CreateDefault(context.AreaId, AreaKind.Exterior, new AreaDimensions(64, 64));
        var command = new SetAreaWorldProfileCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.AreaId, invalid, false);
        var handler = new SetAreaWorldProfileHandler(new FixedClock(PlaceEntityTestContext.Timestamp));

        var result = handler.Handle(context.State, command);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, item => item.Code == "area.world.terrain_dimensions_mismatch");
        Assert.Equal(context.State, result.State);
    }
}
