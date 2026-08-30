using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;
using Forge.Mcp.Projects;

namespace Forge.Mcp.Tests.Projects;

public sealed class EntityMetadataAuthoringTests
{
    [Fact]
    public void TransformAndTagPlansAreDryRunAppliedPersistedAndReverted()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var entity = area.Entities[0];
        var transform = new TransformValue(
            new Vector3Value(4, 2, -3),
            new QuaternionValue(0, .70710677f, 0, .70710677f),
            new Vector3Value(1, 2, 1));

        var transformPlan = workspace.Project.PlanEntityTransform(new EntityTransformPlanRequest(
            1, workspace.Project.GetCurrentRevision(), area.Id.ToString(), entity.Id.ToString(), transform));

        Assert.True(transformPlan.Succeeded);
        Assert.False(transformPlan.Applied);
        var transformed = workspace.Project.ApplyEntityTransformPlan(transformPlan.PlanId!);
        Assert.True(transformed.Applied);
        Assert.Equal(transform, ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0]
            .Entities.Single(item => item.Id == entity.Id).Transform);
        var revertedTransform = workspace.Project.RevertEntityTransformPlan(transformPlan.PlanId!);
        Assert.True(revertedTransform.Reverted);

        var tagsPlan = workspace.Project.PlanEntityTags(new EntityTagsPlanRequest(
            1, workspace.Project.GetCurrentRevision(), area.Id.ToString(), entity.Id.ToString(),
            ["entrance", "interactive-door"]));
        Assert.True(tagsPlan.Succeeded);
        var tagged = workspace.Project.ApplyEntityTagsPlan(tagsPlan.PlanId!);
        Assert.True(tagged.Applied);
        Assert.Equal(["entrance", "interactive-door"], ProjectSession.Open(workspace.RootDirectory).Session!
            .Project.Areas[0].Entities.Single(item => item.Id == entity.Id).Tags);
        var revertedTags = workspace.Project.RevertEntityTagsPlan(tagsPlan.PlanId!);
        Assert.True(revertedTags.Reverted);
    }
}
