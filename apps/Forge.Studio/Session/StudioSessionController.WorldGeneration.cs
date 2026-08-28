using System.Diagnostics;
using Forge.Domain.Assistant;
using Forge.Domain.WorldGeneration;
using Forge.Mcp.Projects;
using Godot;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal WorldGenerationRecipeResource GetWorldGenerationState() =>
        WorldGenerationAgent.GetWorldGenerationRecipes();

    internal WorldGenerationRecipePlanResult PreviewWorldGenerationRecipe(
        Guid areaId,
        string title,
        int seed,
        double widthMeters,
        double depthMeters,
        double maxElevationMeters,
        double roughness,
        bool nativeOnly)
    {
        var state = GetWorldGenerationState();
        var baseline = WorldGenerationRecipe.CreateDefault(areaId, title, seed);
        var backends = nativeOnly
            ? Enum.GetValues<WorldGenerationStage>().ToDictionary(stage => stage,
                _ => WorldGenerationBackend.GodotNative)
            : baseline.Backends;
        var recipe = new WorldGenerationRecipe(
            baseline.Id,
            baseline.SchemaVersion,
            baseline.AreaId,
            baseline.Title,
            baseline.Seed,
            new WorldGenerationBounds(widthMeters, depthMeters, maxElevationMeters),
            baseline.Terrain with { Roughness = roughness },
            baseline.Rivers,
            baseline.Biomes,
            baseline.Roads,
            baseline.Settlements,
            baseline.Scatter,
            baseline.Gameplay,
            baseline.Navigation,
            baseline.Quality,
            backends,
            approvedForGeneration: true,
            provenance: "studio:world-generation-v1",
            baseline.Regeneration);
        var recipes = state.Catalog.Recipes.Where(item => item.AreaId != areaId).Append(recipe).ToArray();
        var catalog = new WorldGenerationRecipeCatalog(
            state.Catalog.Id,
            state.Catalog.SchemaVersion,
            recipes,
            "studio:world-generation-v1");
        return WorldGenerationAgent.PlanWorldGenerationRecipes(new WorldGenerationRecipePlanRequest(
            1,
            state.RecipeRevision,
            state.ProjectRevision,
            catalog,
            AssistantPermissionLevel.ApplyApprovedPlan));
    }

    internal WorldGenerationRecipePlanResult ApplyWorldGenerationRecipe(string planId) =>
        WorldGenerationAgent.ApplyWorldGenerationRecipePlan(planId);

    internal WorldGenerationExecutionPlanResource PreviewWorldGenerationExecution(Guid recipeId)
    {
        var state = GetWorldGenerationState();
        return WorldGenerationAgent.PreviewWorldGenerationExecution(
            new WorldGenerationExecutionPreviewRequest(
                1,
                state.ProjectRevision,
                state.RecipeRevision,
                recipeId.ToString("D"),
                Enum.GetValues<WorldGenerationStage>()));
    }

    internal WorldGenerationExecutionPlanResource QueueWorldGenerationExecution(Guid planId) =>
        WorldGenerationAgent.QueueWorldGenerationExecutionPlan(planId.ToString("D"));

    internal IReadOnlyList<WorldGenerationJobResource> GetWorldGenerationJobs(Guid areaId)
    {
        var canonicalAreaId = areaId.ToString("D");
        return WorldGenerationAgent.GetWorldGenerationJobs()
            .Where(job => string.Equals(job.AreaId, canonicalAreaId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    internal async Task<WorldGenerationWorkerResult> RunWorldGenerationWorkerAsync(
        string graphProvider,
        CancellationToken cancellationToken = default)
    {
        if (graphProvider is not ("godot-flow" or "gf-pcgodot"))
            throw new ArgumentOutOfRangeException(nameof(graphProvider), graphProvider, "Unknown graph provider.");
        var workspace = Session?.RootDirectory ??
            throw new InvalidOperationException("Aucun module n’est ouvert pour WorldGeneration.");
        var script = ResolveWorldGenerationWorkerScript();
        if (script is null)
            throw new FileNotFoundException(
                "Le worker WorldGeneration n’est présent ni dans le dépôt ni dans la distribution Studio.");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = Path.GetDirectoryName(script)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (var argument in new[]
                 {
                     "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script,
                     "-WorkspaceRoot", workspace, "-GraphProvider", graphProvider,
                 })
            process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start())
            return new(false, -1, string.Empty, "Le processus PowerShell n’a pas démarré.", script);

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new(
            process.ExitCode == 0,
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false),
            script);
    }

    internal WorldGenerationPromotionPlanResult PreviewWorldGenerationPromotion(Guid jobPlanId, Guid expectedAreaId)
    {
        var state = GetWorldGenerationState();
        var job = WorldGenerationAgent.GetWorldGenerationJob(jobPlanId.ToString("D"));
        if (job is null)
            throw new InvalidOperationException($"Le job WorldGeneration '{jobPlanId:D}' n’existe plus.");
        if (!Guid.TryParse(job.AreaId, out var jobAreaId) || jobAreaId != expectedAreaId)
            throw new InvalidOperationException(
                $"Le job WorldGeneration '{jobPlanId:D}' cible une autre zone que la zone active.");
        var canonical = WorldGenerationAgent.GetCanonicalWorldGenerationPromotion(job.AreaId);
        return WorldGenerationAgent.PlanWorldGenerationPromotion(new WorldGenerationPromotionPlanRequest(
            1,
            state.ProjectRevision,
            state.RecipeRevision,
            jobPlanId.ToString("D"),
            canonical.SceneSha256,
            "studio:human-reviewed-world-generation",
            AssistantPermissionLevel.ApplyApprovedPlan));
    }

    internal WorldGenerationPromotionPlanResult ApplyWorldGenerationPromotion(string promotionPlanId) =>
        WorldGenerationAgent.ApplyWorldGenerationPromotion(promotionPlanId);

    private ForgeProjectAgentService WorldGenerationAgent => _worldGenerationAgent ??
        throw new InvalidOperationException("Aucun module n’est ouvert pour WorldGeneration.");

    private static string? ResolveWorldGenerationWorkerScript()
    {
        var resourceRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        var candidates = new List<string>
        {
            Path.Combine(resourceRoot, "world-generation", "Invoke-WorldGenerationWorkspaceWorker.ps1"),
            Path.GetFullPath(Path.Combine(resourceRoot, "..", "..", "scripts",
                "Invoke-WorldGenerationWorkspaceWorker.ps1")),
            Path.Combine(System.Environment.CurrentDirectory, "world-generation",
                "Invoke-WorldGenerationWorkspaceWorker.ps1"),
        };
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 6 && current is not null; depth++, current = current.Parent)
        {
            candidates.Add(Path.Combine(current.FullName, "world-generation",
                "Invoke-WorldGenerationWorkspaceWorker.ps1"));
            candidates.Add(Path.Combine(current.FullName, "scripts",
                "Invoke-WorldGenerationWorkspaceWorker.ps1"));
        }
        return candidates.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }
}

internal sealed record WorldGenerationWorkerResult(
    bool Succeeded,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string ScriptPath);
