using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Areas;

public sealed class EditTerrainBrushHandlerTests
{
    private static readonly Guid RaiseId = Guid.Parse("d1400000-0000-4000-8000-000000000001");

    [Fact]
    public void RaiseBrushEditsCanonicalSamplesDeterministically()
    {
        var source = ExteriorState().Project.Areas.Single().World.Terrain;
        var stroke = new TerrainBrushStroke(TerrainBrushOperation.Raise, 0, 0, 8, .2f);

        var first = TerrainBrushEditor.Apply(source, stroke, RaiseId);
        var second = TerrainBrushEditor.Apply(source, stroke, RaiseId);

        Assert.True(first.ChangedSamples > 0);
        Assert.Equal(first.Terrain.Heights, second.Terrain.Heights);
        Assert.True(first.Terrain.HeightAt(0, 0) > source.HeightAt(0, 0));
        Assert.Equal(source.Heights[0], first.Terrain.Heights[0]);
    }

    [Theory]
    [InlineData(TerrainBrushOperation.Lower)]
    [InlineData(TerrainBrushOperation.Flatten)]
    [InlineData(TerrainBrushOperation.Smooth)]
    public void SculptModesProduceBoundedNormalizedHeights(TerrainBrushOperation operation)
    {
        var source = ExteriorState().Project.Areas.Single().World.Terrain;
        var stroke = new TerrainBrushStroke(operation, 0, 0, 12, .5f, 2);

        var result = TerrainBrushEditor.Apply(source, stroke, RaiseId);

        Assert.True(result.ChangedSamples > 0);
        Assert.All(result.Terrain.Heights, value => Assert.InRange(value, -1, 1));
        Assert.Equal(source.SamplesX * source.SamplesZ, result.Terrain.Heights.Count);
    }

    [Fact]
    public void HoleBrushOpensAndClosesOnlyTouchedCells()
    {
        var source = ExteriorState().Project.Areas.Single().World.Terrain;
        var open = TerrainBrushEditor.Apply(source,
            new TerrainBrushStroke(TerrainBrushOperation.OpenHole, 0, 0, 5, 1), RaiseId);
        var close = TerrainBrushEditor.Apply(open.Terrain,
            new TerrainBrushStroke(TerrainBrushOperation.CloseHole, 0, 0, 5, 1), Guid.Parse("d1400000-0000-4000-8000-000000000002"));

        Assert.True(open.ChangedHoleCells > 0);
        Assert.NotEmpty(open.Terrain.Holes);
        Assert.Equal(open.ChangedHoleCells, close.ChangedHoleCells);
        Assert.Empty(close.Terrain.Holes);
    }

    [Fact]
    public void CommandSupportsDryRunHistoryAndUndoRedo()
    {
        var state = ExteriorState();
        var area = state.Project.Areas.Single();
        var handler = new EditTerrainBrushHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var dryRun = new EditTerrainBrushCommand(CommandId.From(RaiseId), state.Revision, area.Id,
            new TerrainBrushStroke(TerrainBrushOperation.Raise, 0, 0, 8, .2f), true);

        var preview = handler.Handle(state, dryRun);

        Assert.True(preview.Succeeded);
        Assert.Equal(state, preview.State);
        Assert.False(preview.Audit.Applied);
        Assert.Equal($"areas/{area.Id}/world/terrain", preview.Patch!.Path);

        var history = new ProjectCommandHistory(state);
        history.Record(handler.Handle(state, dryRun with { DryRun = false }));
        Assert.NotEqual(area.World.Terrain.Heights, history.State.Project.Areas.Single().World.Terrain.Heights);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(area.World.Terrain.Heights, history.State.Project.Areas.Single().World.Terrain.Heights);
        Assert.True(history.Redo().Succeeded);
    }

    [Fact]
    public void CommandRejectsBrushOutsideTerrainAndDisabledTerrain()
    {
        var exterior = ExteriorState();
        var area = exterior.Project.Areas.Single();
        var handler = new EditTerrainBrushHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var outside = handler.Handle(exterior, new EditTerrainBrushCommand(CommandId.From(RaiseId), exterior.Revision,
            area.Id, new TerrainBrushStroke(TerrainBrushOperation.Raise, 1000, 1000, 1, .2f), false));
        var interior = new PlaceEntityTestContext();
        var disabled = handler.Handle(interior.State, new EditTerrainBrushCommand(CommandId.From(RaiseId),
            interior.State.Revision, interior.AreaId,
            new TerrainBrushStroke(TerrainBrushOperation.Raise, 0, 0, 4, .2f), false));

        Assert.Contains(outside.Diagnostics, item => item.Code == "area.terrain.brush_no_change");
        Assert.Contains(disabled.Diagnostics, item => item.Code == "area.terrain.brush_invalid");
    }

    private static ProjectState ExteriorState()
    {
        var context = new PlaceEntityTestContext();
        var source = context.State.Project.Areas.Single();
        var area = new AreaDefinition(source.Id, source.Slug, source.DisplayName, source.Entities, AreaKind.Exterior,
            source.Dimensions, source.Portals, source.SchemaVersion, source.Navigation, source.Environment,
            source.Authoring, AreaWorldProfile.CreateDefault(source.Id, AreaKind.Exterior, source.Dimensions),
            source.RuntimePolicies);
        var project = new ProjectDefinition(context.State.Project.Id, context.State.Project.Title,
            context.State.Project.Module, context.State.Project.Blueprints, [area], context.State.Project.Dialogues,
            context.State.Project.Quests);
        return new ProjectState(project, context.State.Revision);
    }
}
