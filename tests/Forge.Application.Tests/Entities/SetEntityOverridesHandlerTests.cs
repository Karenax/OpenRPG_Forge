using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;

namespace Forge.Application.Tests.Entities;

public sealed class SetEntityOverridesHandlerTests
{
    [Fact]
    public void Handle_AppliesOverridesAndAdvancesRevision()
    {
        var context = new EntityMutationTestContext();

        var result = context.OverridesHandler.Handle(context.State, context.CreateOverridesCommand());

        Assert.True(result.Succeeded);
        Assert.Equal(new ProjectRevision(4), result.State.Revision);
        var entity = Assert.Single(Assert.Single(result.State.Project.Areas).Entities);
        Assert.Equal(2, entity.Overrides.Properties.Count);
        Assert.Equal("true", entity.Overrides.Properties.Single(property => property.Key == "locked").Value);
        Assert.Equal("replace", result.Patch?.Operation);
        Assert.Equal("replace", result.Inverse?.Operation);
        Assert.True(result.Audit.Applied);
        Assert.Equal(EntityMutationTestContext.Timestamp, result.Audit.Timestamp);
    }

    [Fact]
    public void Handle_DryRunProducesPreviewWithoutMutation()
    {
        var context = new EntityMutationTestContext();

        var result = context.OverridesHandler.Handle(
            context.State,
            context.CreateOverridesCommand(dryRun: true));

        Assert.True(result.Succeeded);
        Assert.Same(context.State, result.State);
        Assert.Null(result.NewRevision);
        Assert.NotNull(result.Patch);
        Assert.False(result.Audit.Applied);
    }

    [Fact]
    public void Handle_RejectsRevisionConflict()
    {
        var context = new EntityMutationTestContext();

        var result = context.OverridesHandler.Handle(
            context.State,
            context.CreateOverridesCommand(revision: new ProjectRevision(2)));

        Assert.False(result.Succeeded);
        Assert.Equal(SetEntityOverridesHandler.RevisionConflictCode, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Handle_RejectsMissingArea()
    {
        var context = new EntityMutationTestContext();

        var result = context.OverridesHandler.Handle(
            context.State,
            context.CreateOverridesCommand(
                areaId: AreaId.From(Guid.Parse("44dc0c16-f2cb-4199-af4a-8f0311225986"))));

        Assert.False(result.Succeeded);
        Assert.Equal(SetEntityOverridesHandler.MissingAreaCode, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Handle_RejectsMissingEntity()
    {
        var context = new EntityMutationTestContext();

        var result = context.OverridesHandler.Handle(
            context.State,
            context.CreateOverridesCommand(
                entityId: EntityId.From(Guid.Parse("787c94ba-9418-40cf-b765-f722e3308ae9"))));

        Assert.False(result.Succeeded);
        Assert.Equal(SetEntityOverridesHandler.MissingEntityCode, Assert.Single(result.Diagnostics).Code);
    }
}
