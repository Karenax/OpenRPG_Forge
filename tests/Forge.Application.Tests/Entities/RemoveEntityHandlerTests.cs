using Forge.Application.Entities;
using Forge.Domain.Entities;

namespace Forge.Application.Tests.Entities;

public sealed class RemoveEntityHandlerTests
{
    [Fact]
    public void Handle_RemovesEntityAndReturnsInversePatch()
    {
        var context = new EntityMutationTestContext();

        var result = context.RemoveHandler.Handle(context.State, context.CreateRemoveCommand());

        Assert.True(result.Succeeded);
        Assert.Empty(Assert.Single(result.State.Project.Areas).Entities);
        Assert.Equal("remove", result.Patch?.Operation);
        Assert.Equal("add", result.Inverse?.Operation);
        Assert.True(result.Audit.Applied);
    }

    [Fact]
    public void Handle_DryRunDoesNotRemoveEntity()
    {
        var context = new EntityMutationTestContext();

        var result = context.RemoveHandler.Handle(
            context.State,
            context.CreateRemoveCommand(dryRun: true));

        Assert.True(result.Succeeded);
        Assert.Single(Assert.Single(result.State.Project.Areas).Entities);
        Assert.Null(result.NewRevision);
        Assert.False(result.Audit.Applied);
    }

    [Fact]
    public void Handle_RejectsMissingEntity()
    {
        var context = new EntityMutationTestContext();

        var result = context.RemoveHandler.Handle(
            context.State,
            context.CreateRemoveCommand(
                entityId: EntityId.From(Guid.Parse("4c0cfb79-f374-46a5-963a-8819836b0138"))));

        Assert.False(result.Succeeded);
        Assert.Equal(RemoveEntityHandler.MissingEntityCode, Assert.Single(result.Diagnostics).Code);
    }
}
