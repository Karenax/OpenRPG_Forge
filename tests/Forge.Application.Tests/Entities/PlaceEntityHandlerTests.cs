using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;

namespace Forge.Application.Tests.Entities;

public sealed class PlaceEntityHandlerTests
{
    [Fact]
    public void Handle_AppliesEntityAndAdvancesRevision()
    {
        var context = new PlaceEntityTestContext();

        var result = context.Handler.Handle(context.State, context.CreateCommand());

        Assert.True(result.Succeeded);
        Assert.Equal(new ProjectRevision(8), result.NewRevision);
        var entity = Assert.Single(Assert.Single(result.State.Project.Areas).Entities);
        Assert.Equal(context.EntityId, entity.Id);
        Assert.Equal(context.BlueprintId, entity.BlueprintId);
        Assert.Equal("add", result.Patch?.Operation);
        Assert.Equal("remove", result.Inverse?.Operation);
        Assert.True(result.Audit.Applied);
        Assert.Equal(PlaceEntityTestContext.Timestamp, result.Audit.Timestamp);
    }

    [Fact]
    public void Handle_DryRunReturnsPreviewWithoutMutation()
    {
        var context = new PlaceEntityTestContext();

        var result = context.Handler.Handle(context.State, context.CreateCommand(dryRun: true));

        Assert.True(result.Succeeded);
        Assert.Null(result.NewRevision);
        Assert.Same(context.State, result.State);
        Assert.Empty(Assert.Single(result.State.Project.Areas).Entities);
        Assert.NotNull(result.Patch);
        Assert.NotNull(result.Inverse);
        Assert.False(result.Audit.Applied);
    }

    [Fact]
    public void Handle_RejectsRevisionConflict()
    {
        var context = new PlaceEntityTestContext();

        var result = context.Handler.Handle(
            context.State,
            context.CreateCommand(revision: new ProjectRevision(6)));

        Assert.False(result.Succeeded);
        Assert.Equal(PlaceEntityHandler.RevisionConflictCode, Assert.Single(result.Diagnostics).Code);
        Assert.Same(context.State, result.State);
    }

    [Fact]
    public void Handle_RejectsMissingBlueprint()
    {
        var context = new PlaceEntityTestContext();
        var original = context.CreateCommand();
        var command = new PlaceEntityCommand(
            original.CommandId,
            original.BaseRevision,
            original.AreaId,
            original.EntityId,
            BlueprintId.From(Guid.Parse("899aa19d-f0f1-4354-af17-48e0d06ed588")),
            original.Transform,
            original.DryRun);

        var result = context.Handler.Handle(context.State, command);

        Assert.False(result.Succeeded);
        Assert.Equal(PlaceEntityHandler.MissingBlueprintCode, Assert.Single(result.Diagnostics).Code);
    }
}
