using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Domain.Entities;

namespace Forge.Application.Tests.Entities;

public sealed class TransformEntityHandlerTests
{
    [Fact]
    public void Handle_AppliesTransformAndPreservesEntityMetadata()
    {
        var context = new EntityMutationTestContext();

        var result = context.TransformHandler.Handle(context.State, context.CreateTransformCommand());

        Assert.True(result.Succeeded);
        Assert.Equal(new ProjectRevision(4), result.State.Revision);
        var entity = Assert.Single(Assert.Single(result.State.Project.Areas).Entities);
        Assert.Equal(new(4, 0, -2), entity.Transform.Position);
        Assert.Equal(["architecture", "solid"], entity.Tags);
        Assert.Equal("false", Assert.Single(entity.Overrides.Properties).Value);
        Assert.Equal("replace", result.Patch?.Operation);
        Assert.Equal("replace", result.Inverse?.Operation);
    }

    [Fact]
    public void Handle_DryRunDoesNotMutateState()
    {
        var context = new EntityMutationTestContext();

        var result = context.TransformHandler.Handle(
            context.State,
            context.CreateTransformCommand(dryRun: true));

        Assert.True(result.Succeeded);
        Assert.Same(context.State, result.State);
        Assert.Null(result.NewRevision);
        Assert.False(result.Audit.Applied);
    }

    [Fact]
    public void Handle_RejectsRevisionConflict()
    {
        var context = new EntityMutationTestContext();

        var result = context.TransformHandler.Handle(
            context.State,
            context.CreateTransformCommand(revision: new ProjectRevision(2)));

        Assert.False(result.Succeeded);
        Assert.Equal(
            TransformEntityHandler.RevisionConflictCode,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Handle_RejectsMissingEntity()
    {
        var context = new EntityMutationTestContext();

        var result = context.TransformHandler.Handle(
            context.State,
            context.CreateTransformCommand(
                entityId: EntityId.From(Guid.Parse("787c94ba-9418-40cf-b765-f722e3308ae9"))));

        Assert.False(result.Succeeded);
        Assert.Equal(TransformEntityHandler.MissingEntityCode, Assert.Single(result.Diagnostics).Code);
    }
}
