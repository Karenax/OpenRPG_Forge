using Forge.Application.Entities;
using Forge.Application.Projects;

namespace Forge.Application.Tests.Entities;

public sealed class SetEntityTagsHandlerTests
{
    [Fact]
    public void Handle_AppliesNormalizedTagsAndPreservesOtherEntityData()
    {
        var context = new EntityMutationTestContext();

        var result = context.TagsHandler.Handle(
            context.State,
            context.CreateTagsCommand(tags: [" solid ", "interactive", "solid"]));

        Assert.True(result.Succeeded);
        Assert.Equal(new ProjectRevision(4), result.State.Revision);
        var entity = Assert.Single(Assert.Single(result.State.Project.Areas).Entities);
        Assert.Equal(["interactive", "solid"], entity.Tags);
        Assert.Equal(context.BlueprintId, entity.BlueprintId);
        Assert.Equal(context.State.Project.Areas[0].Entities[0].Transform, entity.Transform);
        Assert.Equal("false", Assert.Single(entity.Overrides.Properties).Value);
        Assert.True(result.Audit.Applied);
    }

    [Fact]
    public void Handle_DryRunPreviewsWithoutMutatingState()
    {
        var context = new EntityMutationTestContext();

        var result = context.TagsHandler.Handle(context.State, context.CreateTagsCommand(dryRun: true));

        Assert.True(result.Succeeded);
        Assert.Same(context.State, result.State);
        Assert.Null(result.NewRevision);
        Assert.NotNull(result.Patch);
        Assert.False(result.Audit.Applied);
    }

    [Fact]
    public void Handle_RejectsEmptyTag()
    {
        var context = new EntityMutationTestContext();

        var result = context.TagsHandler.Handle(
            context.State,
            context.CreateTagsCommand(tags: ["solid", " "]));

        Assert.False(result.Succeeded);
        Assert.Equal(SetEntityTagsHandler.InvalidTagCode, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Handle_RejectsRevisionConflict()
    {
        var context = new EntityMutationTestContext();

        var result = context.TagsHandler.Handle(
            context.State,
            context.CreateTagsCommand(revision: new ProjectRevision(2)));

        Assert.False(result.Succeeded);
        Assert.Equal(SetEntityTagsHandler.RevisionConflictCode, Assert.Single(result.Diagnostics).Code);
    }
}
