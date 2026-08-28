using System.Security.Cryptography;
using System.Text;
using Forge.Application.WorldGeneration;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;
using Forge.Domain.WorldGeneration;
using Forge.Mcp;
using Forge.Mcp.Projects;

namespace Forge.WorkspaceMigrator;

internal static class WorldGenerationVerticalSliceOrchestrator
{
    private const string AreaSlug = "les-hautes-brumes-generees";
    private const string Provenance = "module-author:world-generation-vertical-slice-v1";

    internal static WorldGenerationVerticalSliceQueueResult Queue(
        string workspaceRoot,
        ProjectDefinition project,
        bool forceNewExecution = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(project);
        var area = project.Areas.SingleOrDefault(candidate =>
                       string.Equals(candidate.Slug, AreaSlug, StringComparison.Ordinal)) ??
                   throw new InvalidOperationException(
                       "Materialize the world-generation vertical slice before queuing it.");
        var opened = ForgeProjectAgentService.Open(workspaceRoot);
        var service = opened.Service ?? throw new InvalidOperationException(
            "The governed project agent could not open the workspace: " +
            string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        var source = service.GetWorldGenerationRecipes();
        EnsureNoDiagnostics(source.Diagnostics, "load recipes");
        var recipe = CreateRecipe(area.Id.Value);
        var recipeRevision = source.RecipeRevision;
        var persistedRecipe = source.Catalog.Find(recipe.Id);

        if (persistedRecipe is null ||
            !string.Equals(persistedRecipe.Fingerprint(), recipe.Fingerprint(), StringComparison.Ordinal))
        {
            var recipes = source.Catalog.Recipes
                .Where(candidate => candidate.Id != recipe.Id)
                .Append(recipe)
                .ToArray();
            var catalog = new WorldGenerationRecipeCatalog(
                source.Catalog.Id,
                WorldGenerationRecipeCatalog.CurrentSchemaVersion,
                recipes,
                Provenance);
            var planned = service.PlanWorldGenerationRecipes(new(
                1,
                source.RecipeRevision,
                source.ProjectRevision,
                catalog,
                AssistantPermissionLevel.ApplyApprovedPlan));
            EnsureSucceeded(planned.Succeeded, planned.Diagnostics, "plan recipe");
            var applied = service.ApplyWorldGenerationRecipePlan(planned.PlanId!);
            EnsureSucceeded(applied.Succeeded && applied.Applied, applied.Diagnostics, "apply recipe");
            recipeRevision = applied.NewRecipeRevision ?? throw new InvalidOperationException(
                "The recipe was applied without a new persistent revision.");
        }

        var current = service.GetWorldGenerationRecipes();
        EnsureNoDiagnostics(current.Diagnostics, "reload recipes");
        recipeRevision = current.RecipeRevision;
        var existing = service.GetWorldGenerationJobs()
            .FirstOrDefault(job =>
                string.Equals(job.RecipeId, recipe.Id.ToString("D"), StringComparison.Ordinal) &&
                string.Equals(job.RecipeFingerprint, recipe.Fingerprint(), StringComparison.Ordinal) &&
                job.Status is not WorldGenerationJobStatus.Cancelled and not WorldGenerationJobStatus.Failed);
        if (existing is not null && !forceNewExecution)
            return new(existing.PlanId, existing.JobId, current.ProjectRevision, recipeRevision,
                existing.Status, Reused: true);

        var preview = service.PreviewWorldGenerationExecution(new(
            1,
            current.ProjectRevision,
            recipeRevision,
            recipe.Id.ToString("D")));
        EnsureSucceeded(preview.Succeeded && preview.Plan is { ReadyToApply: true },
            preview.Diagnostics, "preview execution");
        var queued = service.QueueWorldGenerationExecutionPlan(preview.Plan!.PlanId.ToString("D"));
        EnsureSucceeded(queued.Succeeded && queued.Queued, queued.Diagnostics, "queue execution");
        var job = service.GetWorldGenerationJob(preview.Plan.PlanId.ToString("D")) ??
                  throw new InvalidOperationException("The queued world-generation job was not persisted.");
        return new(job.PlanId, job.JobId, current.ProjectRevision, recipeRevision, job.Status, Reused: false);
    }

    internal static WorldGenerationVerticalSlicePromotionResult PromoteLatest(
        string workspaceRoot,
        ProjectDefinition project,
        string acceptanceProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptanceProvenance);
        var area = project.Areas.SingleOrDefault(candidate =>
                       string.Equals(candidate.Slug, AreaSlug, StringComparison.Ordinal)) ??
                   throw new InvalidOperationException(
                       "Materialize the world-generation vertical slice before promoting it.");
        var opened = ForgeProjectAgentService.Open(workspaceRoot);
        var service = opened.Service ?? throw new InvalidOperationException(
            "The governed project agent could not open the workspace: " +
            string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        var source = service.GetWorldGenerationRecipes();
        EnsureNoDiagnostics(source.Diagnostics, "load recipes for promotion");
        var job = service.GetWorldGenerationJobs()
            .Where(candidate =>
                string.Equals(candidate.AreaId, area.Id.Value.ToString("D"), StringComparison.Ordinal) &&
                candidate.Status == WorldGenerationJobStatus.Succeeded)
            .OrderByDescending(candidate => candidate.UpdatedAtUtc)
            .FirstOrDefault() ?? throw new InvalidOperationException(
                "No successful world-generation job exists for the vertical-slice area.");
        var canonical = service.GetCanonicalWorldGenerationPromotion(area.Id.Value.ToString("D"));
        var expectedCanonicalSha = canonical.Succeeded ? canonical.SceneSha256 : null;
        var planned = service.PlanWorldGenerationPromotion(new(
            1,
            source.ProjectRevision,
            source.RecipeRevision,
            job.PlanId,
            expectedCanonicalSha,
            acceptanceProvenance,
            AssistantPermissionLevel.ApplyApprovedPlan));
        EnsureSucceeded(planned.Succeeded && !planned.Applied, planned.Diagnostics, "plan promotion");
        var applied = service.ApplyWorldGenerationPromotion(planned.PromotionPlanId!);
        EnsureSucceeded(applied.Succeeded && applied.Applied, applied.Diagnostics, "apply promotion");
        return new(
            applied.PromotionPlanId!,
            job.PlanId,
            applied.CanonicalSceneRelativePath!,
            applied.CanonicalSceneSha256!,
            applied.NewProjectRevision ?? throw new InvalidOperationException(
                "The canonical promotion did not persist a project revision."));
    }

    private static WorldGenerationRecipe CreateRecipe(Guid areaId)
    {
        var baseline = WorldGenerationRecipe.CreateDefault(
            areaId,
            "Les Hautes Brumes — tranche verticale",
            20260828);
        return new WorldGenerationRecipe(
            baseline.Id,
            baseline.SchemaVersion,
            baseline.AreaId,
            baseline.Title,
            baseline.Seed,
            baseline.Bounds,
            baseline.Terrain,
            [new RiverGenerationRecipe(Stable(areaId, "river"), "Ruisseau des Brumes", 12, 2,
                "highlands-to-valley")],
            baseline.Biomes,
            [new RoadGenerationRecipe(Stable(areaId, "road"), "mountain-path", "entry-spawn",
                "settlement-center", 5)],
            [new SettlementGenerationRecipe(Stable(areaId, "settlement"), "hamlet", "free-valleys",
                "sheltered-ridge-near-road", 8, false, ["inn", "watchtower"])],
            baseline.Scatter,
            new GameplayGenerationRecipe(
                ["highlands-encounters"],
                ["highlands-wildlife"],
                ["highlands-supplies"],
                true),
            baseline.Navigation,
            baseline.Quality,
            baseline.Backends,
            true,
            Provenance,
            baseline.Regeneration);
    }

    private static Guid Stable(Guid areaId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"openrpg-forge:{areaId:D}:world-generation:{discriminator}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }

    private static void EnsureNoDiagnostics(
        IReadOnlyList<AgentDiagnostic> diagnostics,
        string operation)
    {
        if (diagnostics.Count > 0) throw Failure(operation, diagnostics);
    }

    private static void EnsureSucceeded(
        bool succeeded,
        IReadOnlyList<AgentDiagnostic> diagnostics,
        string operation)
    {
        if (!succeeded) throw Failure(operation, diagnostics);
    }

    private static InvalidOperationException Failure(
        string operation,
        IReadOnlyList<AgentDiagnostic> diagnostics) =>
        new($"World-generation {operation} failed: " +
            (diagnostics.Count == 0
                ? "no diagnostic was returned"
                : string.Join(" | ", diagnostics.Select(item => $"{item.Code}: {item.Message}"))));
}

internal sealed record WorldGenerationVerticalSliceQueueResult(
    string PlanId,
    string JobId,
    long ProjectRevision,
    long RecipeRevision,
    WorldGenerationJobStatus Status,
    bool Reused);

internal sealed record WorldGenerationVerticalSlicePromotionResult(
    string PromotionPlanId,
    string JobPlanId,
    string CanonicalSceneRelativePath,
    string CanonicalSceneSha256,
    long ProjectRevision);
