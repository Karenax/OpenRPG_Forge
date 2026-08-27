using System.ComponentModel;
using Forge.Mcp.Projects;
using ModelContextProtocol.Server;

namespace Forge.McpHost;

[McpServerToolType]
public sealed class ForgeWorldGenerationMcpTools
{
    private readonly ForgeProjectAgentService _project;

    public ForgeWorldGenerationMcpTools(ForgeProjectAgentService project) => _project = project;

    [McpServerTool]
    [Description("Returns the governed, engine-neutral world-generation recipe catalog and its recipe/project revisions without mutation.")]
    public WorldGenerationRecipeResource GetWorldGenerationRecipes() =>
        _project.GetWorldGenerationRecipes();

    [McpServerTool]
    [Description("Validates and dry-runs replacement of the world-generation recipe catalog. Approved recipes require ApplyApprovedPlan permission.")]
    public WorldGenerationRecipePlanResult PlanWorldGenerationRecipes(WorldGenerationRecipePlanRequest request) =>
        _project.PlanWorldGenerationRecipes(request);

    [McpServerTool]
    [Description("Applies a previously validated world-generation recipe plan when its transaction and project/recipe revisions still match.")]
    public WorldGenerationRecipePlanResult ApplyWorldGenerationRecipePlan(string planId) =>
        _project.ApplyWorldGenerationRecipePlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied world-generation recipe mutation through its persisted inverse.")]
    public WorldGenerationRecipePlanResult RevertWorldGenerationRecipePlan(string planId) =>
        _project.RevertWorldGenerationRecipePlan(planId);

    [McpServerTool]
    [Description("Builds and persists a deterministic, dependency-ordered execution preview for one approved world-generation recipe.")]
    public WorldGenerationExecutionPlanResource PreviewWorldGenerationExecution(
        WorldGenerationExecutionPreviewRequest request) =>
        _project.PreviewWorldGenerationExecution(request);

    [McpServerTool]
    [Description("Queues a previously previewed world-generation execution plan for the Godot worker when all fingerprints and revisions still match.")]
    public WorldGenerationExecutionPlanResource QueueWorldGenerationExecutionPlan(string planId) =>
        _project.QueueWorldGenerationExecutionPlan(planId);

    [McpServerTool]
    [Description("Returns durable world-generation jobs, including their current worker status and diagnostics.")]
    public IReadOnlyList<WorldGenerationJobResource> GetWorldGenerationJobs() =>
        _project.GetWorldGenerationJobs();

    [McpServerTool]
    [Description("Returns one durable world-generation job by execution plan ID, or null when it does not exist.")]
    public WorldGenerationJobResource? GetWorldGenerationJob(string planId) =>
        _project.GetWorldGenerationJob(planId);

    [McpServerTool]
    [Description("Cancels a queued or running world-generation job. Completed or already-cancelled jobs are immutable.")]
    public WorldGenerationExecutionPlanResource CancelWorldGenerationJob(string planId) =>
        _project.CancelWorldGenerationJob(planId);

    [McpServerTool]
    [Description("Validates and previews canonical promotion of one successful world-generation job. Requires accepted validation evidence, exact revisions, and an acknowledged canonical destination fingerprint when replacing a scene.")]
    public WorldGenerationPromotionPlanResult PlanWorldGenerationPromotion(
        WorldGenerationPromotionPlanRequest request) =>
        _project.PlanWorldGenerationPromotion(request);

    [McpServerTool]
    [Description("Atomically applies a previously previewed world-generation promotion, persists its provider-neutral scene and provenance manifest, and references it from the target area.")]
    public WorldGenerationPromotionPlanResult ApplyWorldGenerationPromotion(string promotionPlanId) =>
        _project.ApplyWorldGenerationPromotion(promotionPlanId);

    [McpServerTool]
    [Description("Reverts the most recently applied world-generation promotion through its persisted file and project inverse.")]
    public WorldGenerationPromotionPlanResult RevertWorldGenerationPromotion(string promotionPlanId) =>
        _project.RevertWorldGenerationPromotion(promotionPlanId);

    [McpServerTool]
    [Description("Returns the canonical world-generation promotion manifest and verified scene fingerprint for one area.")]
    public WorldGenerationCanonicalPromotionResource GetCanonicalWorldGenerationPromotion(string areaId) =>
        _project.GetCanonicalWorldGenerationPromotion(areaId);
}
