using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Rendering;

namespace Forge.EngineAdapter.Tests.Rendering;

public sealed class AreaExperienceRenderPlanBuilderTests
{
    [Fact]
    public void R055BuildsGeneratedMapVisibilityAndMeasurableLodPlan()
    {
        var areaId = AreaId.From(Guid.Parse("a0558000-0000-4000-8000-000000000001"));
        var room = new AreaVisibilityRoomProfile(Guid.Parse("a0558000-0000-4000-8000-000000000002"),
            "Room", TransformValue.Identity, false, 96, new Dictionary<string, string>());
        var experience = new AreaExperienceAuthoringProfile(
            Guid.Parse("a0558000-0000-4000-8000-000000000003"), 1, "test:r055", [],
            new([room], [],
                [new(Guid.Parse("a0558000-0000-4000-8000-000000000004"), "Wall",
                    TransformValue.Identity, false, "asset:wall", new Dictionary<string, string>())],
                true, true, true, 16, 128),
            new(AreaLodSourceMode.Hybrid, [20, 50, 100], 1_500_000, 96, 24, .15f, true, true,
                [new(Guid.Parse("a0558000-0000-4000-8000-000000000005"), "asset:column",
                    AreaLodSourceMode.Generated, [1, .5f], [12000, 6000], .2f, "test:r055")]), true);
        var authoring = new AreaAuthoringProfile(1, [],
            new(true, true, "hybrid-2d-3d", 2,
                [new(Guid.Parse("a0558000-0000-4000-8000-000000000006"), "Shrine", 16, 0, 0, false)],
                Guid.Parse("a0558000-0000-4000-8000-000000000007"), 1, "test:r055", true, true,
                AreaMapExplorationScope.Party, 8, "area.map.reveal"), [], experience);
        var area = new AreaDefinition(areaId, "render-r055", "Render R055", [], AreaKind.Interior,
            new(32, 24), authoring: authoring);

        var plan = AreaExperienceRenderPlanBuilder.Build(area);

        Assert.Empty(plan.Diagnostics);
        Assert.Equal(2, plan.MapFloors.Count);
        Assert.All(plan.MapFloors, floor => Assert.Equal(12, floor.ExplorationCellCount));
        Assert.Single(plan.Rooms);
        Assert.Single(plan.Occluders);
        Assert.Equal([20, 50, 100], plan.LodDistanceThresholds);
        Assert.Single(plan.LodOverrides);
        Assert.True(plan.CellVisualizer);
    }
}
