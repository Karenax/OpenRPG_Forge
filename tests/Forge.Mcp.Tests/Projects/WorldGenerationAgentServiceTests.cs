using Forge.Application.WorldGeneration;
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
        Assert.Equal(preview.Plan.PlanFingerprint, job.PlanFingerprint);
        Assert.Equal("queued", Assert.Single(job.Audit).Action);

        var reopened = ForgeProjectAgentService.Open(workspace.RootDirectory).Service!;
        Assert.Equal(applied.NewRecipeRevision.Value,
            reopened.GetWorldGenerationRecipes().RecipeRevision);
        var reopenedJob = Assert.Single(reopened.GetWorldGenerationJobs());
        Assert.Equal(job.JobId, reopenedJob.JobId);
        Assert.Equal(job.PlanFingerprint, reopenedJob.PlanFingerprint);
        var idempotentQueue = reopened.QueueWorldGenerationExecutionPlan(preview.Plan.PlanId.ToString("D"));
        Assert.True(idempotentQueue.Succeeded);
        Assert.True(idempotentQueue.Queued);
        Assert.Equal(WorldGenerationJobStatus.Queued, idempotentQueue.JobStatus);

        var cancelled = workspace.Project.CancelWorldGenerationJob(preview.Plan.PlanId.ToString("D"));
        Assert.True(cancelled.Succeeded);
        Assert.Equal(WorldGenerationJobStatus.Cancelled, cancelled.JobStatus);

        var reverted = workspace.Project.RevertWorldGenerationRecipePlan(planned.PlanId!);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Empty(workspace.Project.GetWorldGenerationRecipes().Catalog.Recipes);
    }

    [Fact]
    public void SuccessfulJobCanBePreviewedPromotedReopenedAndReverted()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var source = workspace.Project.GetWorldGenerationRecipes();
        var recipe = WorldGenerationRecipe.CreateDefault(project.Areas[0].Id.Value,
                "Canonical MCP valley", 20260827)
            .WithApproval(true, "test:mcp-promotion-approved");
        var catalog = new WorldGenerationRecipeCatalog(
            Guid.Parse("f0530000-0000-4000-8000-000000000011"), 1, [recipe],
            "test:mcp-promotion");
        var recipePlan = workspace.Project.PlanWorldGenerationRecipes(new(
            1, source.RecipeRevision, source.ProjectRevision, catalog,
            AssistantPermissionLevel.ApplyApprovedPlan));
        var recipeApplied = workspace.Project.ApplyWorldGenerationRecipePlan(recipePlan.PlanId!);
        var execution = workspace.Project.PreviewWorldGenerationExecution(new(
            1, source.ProjectRevision, recipeApplied.NewRecipeRevision!.Value, recipe.Id.ToString("D")));
        Assert.True(workspace.Project.QueueWorldGenerationExecutionPlan(
            execution.Plan!.PlanId.ToString("D")).Succeeded);
        var job = WorldGenerationJobStore.Load(workspace.RootDirectory, execution.Plan.PlanId).Job!
            .WithStatus(WorldGenerationJobStatus.Succeeded, DateTimeOffset.UtcNow);
        Assert.True(WorldGenerationJobStore.Save(workspace.RootDirectory, job).Succeeded);

        var scene = "[gd_scene format=3]\n\n[node name=\"OpenRPGBakedWorld\" type=\"Node3D\"]\n";
        var sceneRelative = WorldGenerationPromotionStore.DerivedSceneRelativePath(job.PlanId);
        var scenePath = Path.Combine(workspace.RootDirectory,
            sceneRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);
        File.WriteAllText(scenePath, scene);
        var sceneSha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(scene))).ToLowerInvariant();
        var providers = job.Plan.Steps.Select(step => new WorldGenerationProviderEvidence(
            step.Stage, step.Backend,
            step.Backend == WorldGenerationBackend.PcgGodot ? "godot-flow" :
                step.Backend == WorldGenerationBackend.Terrain3D ? "terrain3d" : "openrpg-native",
            "test-version", new string('a', 64))).ToArray();
        var report = new WorldGenerationValidationReport(
            1, job.JobId, job.PlanId, recipe.Id, recipe.AreaId, job.ProjectRevision, job.RecipeRevision,
            job.RecipeFingerprint, job.Plan.PlanFingerprint, sceneSha, new string('b', 64),
            new string('c', 64), true, true, true, true, providers, []);
        Assert.True(WorldGenerationPromotionStore.SaveValidationReport(
            workspace.RootDirectory, report).Succeeded);

        var planned = workspace.Project.PlanWorldGenerationPromotion(new(
            1,
            source.ProjectRevision,
            recipeApplied.NewRecipeRevision.Value,
            job.PlanId.ToString("D"),
            null,
            "human-review:mcp-test",
            AssistantPermissionLevel.ApplyApprovedPlan));
        Assert.True(planned.Succeeded,
            string.Join(" | ", planned.Diagnostics.Select(item => item.Message)));
        Assert.False(planned.Applied);
        Assert.NotNull(planned.Diff);
        Assert.NotNull(planned.Inverse);
        Assert.False(File.Exists(Path.Combine(workspace.RootDirectory,
            planned.CanonicalSceneRelativePath!.Replace('/', Path.DirectorySeparatorChar))));

        var applied = workspace.Project.ApplyWorldGenerationPromotion(planned.PromotionPlanId!);

        Assert.True(applied.Succeeded,
            string.Join(" | ", applied.Diagnostics.Select(item => item.Message)));
        Assert.True(applied.Applied);
        Assert.Equal(source.ProjectRevision + 1, applied.NewProjectRevision);
        var loaded = workspace.Project.GetCanonicalWorldGenerationPromotion(recipe.AreaId.ToString("D"));
        Assert.True(loaded.Succeeded);
        Assert.Equal(sceneSha, loaded.Promotion!.CanonicalSceneSha256);
        var reopenedProject = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        Assert.Contains(applied.CanonicalSceneRelativePath,
            reopenedProject.Areas.Single(item => item.Id.Value == recipe.AreaId)
                .World.Loading.RequiredResources);

        var reverted = workspace.Project.RevertWorldGenerationPromotion(planned.PromotionPlanId!);

        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.False(File.Exists(Path.Combine(workspace.RootDirectory,
            applied.CanonicalSceneRelativePath!.Replace('/', Path.DirectorySeparatorChar))));
        var reopenedAfterRevert = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        Assert.DoesNotContain(applied.CanonicalSceneRelativePath,
            reopenedAfterRevert.Areas.Single(item => item.Id.Value == recipe.AreaId)
                .World.Loading.RequiredResources);
    }
}
