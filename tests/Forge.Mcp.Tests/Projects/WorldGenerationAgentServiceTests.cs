using Forge.Domain.Assistant;
using Forge.Domain.WorldGeneration;
using Forge.Mcp.Projects;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class WorldGenerationAgentServiceTests
{
    [Fact]
    public void RecipePlanApplyExecutionQueueCancelAndRevertAreGovernedAndPersistent()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var source = workspace.Project.GetWorldGenerationRecipes();
        var recipe = WorldGenerationRecipe.CreateDefault(project.Areas[0].Id.Value, "MCP valley", 20260826)
            .WithApproval(true, "test:mcp-approved");
        var catalog = new WorldGenerationRecipeCatalog(
            Guid.Parse("f0530000-0000-4000-8000-000000000001"), 1, [recipe], "test:mcp");

        var planned = workspace.Project.PlanWorldGenerationRecipes(new(
            1,
            source.RecipeRevision,
            source.ProjectRevision,
            catalog,
            AssistantPermissionLevel.ApplyApprovedPlan));
        Assert.True(planned.Succeeded, string.Join(" | ", planned.Diagnostics.Select(item => item.Message)));
        Assert.False(planned.Applied);
        Assert.False(File.Exists(Path.Combine(workspace.RootDirectory, WorldGenerationRecipeStore.RelativePath)));

        var applied = workspace.Project.ApplyWorldGenerationRecipePlan(planned.PlanId!);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.True(File.Exists(Path.Combine(workspace.RootDirectory, WorldGenerationRecipeStore.RelativePath)));

        var preview = workspace.Project.PreviewWorldGenerationExecution(new(
            1,
            source.ProjectRevision,
            applied.NewRecipeRevision!.Value,
            recipe.Id.ToString("D")));
        Assert.True(preview.Succeeded, string.Join(" | ", preview.Diagnostics.Select(item => item.Message)));
        Assert.True(preview.Plan!.ReadyToApply);
        Assert.False(preview.Queued);

        var queued = workspace.Project.QueueWorldGenerationExecutionPlan(preview.Plan.PlanId.ToString("D"));
        Assert.True(queued.Succeeded);
        Assert.True(queued.Queued);
        Assert.Equal(WorldGenerationJobStatus.Queued, queued.JobStatus);
        var job = Assert.Single(workspace.Project.GetWorldGenerationJobs());
        Assert.Equal(preview.Plan.PlanId.ToString("D"), job.PlanId);

        var cancelled = workspace.Project.CancelWorldGenerationJob(preview.Plan.PlanId.ToString("D"));
        Assert.True(cancelled.Succeeded);
        Assert.Equal(WorldGenerationJobStatus.Cancelled, cancelled.JobStatus);

        var reverted = workspace.Project.RevertWorldGenerationRecipePlan(planned.PlanId!);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Empty(workspace.Project.GetWorldGenerationRecipes().Catalog.Recipes);
    }
}
