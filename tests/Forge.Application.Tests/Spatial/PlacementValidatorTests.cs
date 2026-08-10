using Forge.Application.Spatial;
using Forge.Application.Tests.Entities;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Spatial;

public sealed class PlacementValidatorTests
{
    [Fact]
    public void ValidPositionIsSnappedWithoutMutatingProject()
    {
        var context = new EntityMutationTestContext();
        var validator = new PlacementValidator();
        var request = new PlacementValidationRequest(
            context.AreaId,
            context.BlueprintId,
            new Vector3Value(4.4f, 0.5f, -2.6f),
            1);

        var result = validator.Validate(context.State.Project, request);

        Assert.True(result.IsValid);
        Assert.Equal(PlacementValidationResult.ValidCode, result.Code);
        Assert.Equal(new Vector3Value(4, 0.5f, -3), result.CorrectedPosition);
        Assert.Single(context.State.Project.Areas.Single().Entities);
    }

    [Fact]
    public void OccupiedCellIsRejectedWithStableDiagnostic()
    {
        var context = new EntityMutationTestContext();
        var result = new PlacementValidator().Validate(
            context.State.Project,
            new PlacementValidationRequest(
                context.AreaId,
                context.BlueprintId,
                Vector3Value.Zero,
                1));

        Assert.False(result.IsValid);
        Assert.Equal(PlacementValidator.OccupiedCode, result.Code);
        Assert.Contains(context.EntityId.ToString()[..8], result.Message);
    }

    [Fact]
    public void MovingEntityMayReuseItsOwnCell()
    {
        var context = new EntityMutationTestContext();
        var result = new PlacementValidator().Validate(
            context.State.Project,
            new PlacementValidationRequest(
                context.AreaId,
                context.BlueprintId,
                Vector3Value.Zero,
                1,
                context.EntityId));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void OutsidePositionReturnsBoundedCorrection()
    {
        var context = new EntityMutationTestContext();
        var result = new PlacementValidator().Validate(
            context.State.Project,
            new PlacementValidationRequest(
                context.AreaId,
                context.BlueprintId,
                new Vector3Value(14, 0, -12),
                1));

        Assert.False(result.IsValid);
        Assert.Equal(PlacementValidator.OutsideBoundsCode, result.Code);
        Assert.Equal(new Vector3Value(10, 0, -10), result.CorrectedPosition);
    }

    [Fact]
    public void MissingBlueprintIsRejectedBeforePlacement()
    {
        var context = new EntityMutationTestContext();
        var result = new PlacementValidator().Validate(
            context.State.Project,
            new PlacementValidationRequest(
                context.AreaId,
                BlueprintId.From(Guid.Parse("f3000000-0000-4000-8000-000000000001")),
                new Vector3Value(2, 0, 2),
                1));

        Assert.False(result.IsValid);
        Assert.Equal(PlacementValidator.MissingBlueprintCode, result.Code);
    }

    [Fact]
    public void SocketAndBudgetFailuresExposeStableCauses()
    {
        var context = new EntityMutationTestContext();
        var socket = new PlacementValidator().Validate(context.State.Project,
            new PlacementValidationRequest(context.AreaId, context.BlueprintId, new Vector3Value(2, 0, 2), 1,
                RequiredSocket: "wall-north", AvailableSockets: ["floor"]));
        var budget = new PlacementValidator().Validate(context.State.Project,
            new PlacementValidationRequest(context.AreaId, context.BlueprintId, new Vector3Value(2, 0, 2), 1,
                AvailableBudgetBytes: 1024, BlueprintEstimatedBytes: 2048));

        Assert.Equal(PlacementValidator.SocketMissingCode, socket.Code);
        Assert.Contains("wall-north", socket.Message);
        Assert.Equal(PlacementValidator.BudgetExceededCode, budget.Code);
        Assert.Contains("2048", budget.Message);
    }

    [Fact]
    public void AutoCorrectionSuggestsNearestFreeGridCellWithoutMutation()
    {
        var context = new EntityMutationTestContext();
        var result = new PlacementValidator().Validate(context.State.Project,
            new PlacementValidationRequest(context.AreaId, context.BlueprintId, Vector3Value.Zero, 1,
                AutoCorrect: true));

        Assert.False(result.IsValid);
        Assert.Equal(PlacementValidator.OccupiedCode, result.Code);
        Assert.NotEqual(Vector3Value.Zero, result.CorrectedPosition);
        Assert.Contains(result.Corrections!, correction => correction.Contains("proposée", StringComparison.OrdinalIgnoreCase));
        Assert.Single(context.State.Project.Areas.Single().Entities);
    }
}
