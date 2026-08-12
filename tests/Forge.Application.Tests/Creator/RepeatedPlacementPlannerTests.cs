using Forge.Application.Creator;
using Forge.Domain.Spatial;
using Forge.Domain.Authoring;

namespace Forge.Application.Tests.Creator;

public sealed class RepeatedPlacementPlannerTests
{
    [Fact]
    public void Grid_preserves_origin_and_uses_requested_columns()
    {
        var positions = RepeatedPlacementPlanner.Plan(new(
            new Vector3Value(10, 2, 20), 6, 2, PlacementDistribution.Grid, Columns: 3));

        Assert.Equal(6, positions.Count);
        Assert.Equal(new Vector3Value(10, 2, 20), positions[0]);
        Assert.Equal(new Vector3Value(14, 2, 22), positions[5]);
    }

    [Fact]
    public void Scatter_is_repeatable_for_the_same_seed()
    {
        var request = new RepeatedPlacementRequest(
            new Vector3Value(0, 0, 0), 12, 1.5f, PlacementDistribution.DeterministicScatter, Seed: 42);

        Assert.Equal(RepeatedPlacementPlanner.Plan(request), RepeatedPlacementPlanner.Plan(request));
        Assert.NotEqual(
            RepeatedPlacementPlanner.Plan(request),
            RepeatedPlacementPlanner.Plan(request with { Seed = 43 }));
    }

    [Fact]
    public void AdvancedPlanAlignsToSurfaceAndBrushStrokeRespectsSpacing()
    {
        var profile = new EditorRepeatedPlacementProfile(Guid.NewGuid(), 1, "test", true,
            true, true, EditorPlacementDistribution.Line, EditorPlacementAlignment.SurfaceNormal,
            2, 4, 42, 0.25f);

        var plan = RepeatedPlacementPlanner.Plan(profile, new Vector3Value(0, 0, 0), 3,
            position => new PlacementSurfaceSample(new Vector3Value(position.X, 3, position.Z),
                new Vector3Value(0, 1, 0)));
        var brush = RepeatedPlacementPlanner.PlanBrushStroke(profile,
            [new(0, 0, 0), new(1, 0, 0), new(2.1f, 0, 0), new(4.2f, 0, 0)]);

        Assert.All(plan.Poses, pose => Assert.Equal(3.25f, pose.Position.Y));
        Assert.Equal(3, brush.Poses.Count);
        Assert.True(brush.ContinuousStroke);
    }

    [Fact]
    public void ContextualHelpSearchReturnsOnlySourcedLocalAnswers()
    {
        var profile = EditorContextualHelpProfile.CreateDefault(Guid.NewGuid(), true);
        var result = EditorContextualHelpService.Query(profile, "placement", assisted: true);

        Assert.Empty(result.Diagnostics);
        Assert.NotEmpty(result.Topics);
        Assert.NotEmpty(result.Citations);
        Assert.Contains("Placement", result.Answer, StringComparison.OrdinalIgnoreCase);
    }
}
