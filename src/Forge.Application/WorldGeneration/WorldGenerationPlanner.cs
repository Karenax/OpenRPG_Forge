using System.Globalization;
using Forge.Domain.WorldGeneration;

namespace Forge.Application.WorldGeneration;

public sealed class WorldGenerationPlanner
{
    public WorldGenerationExecutionPlan Build(
        WorldGenerationRecipe recipe,
        long projectRevision,
        WorldGenerationRecipeRevision recipeRevision,
        Guid transactionId,
        Guid planId,
        IReadOnlyCollection<WorldGenerationStage>? stages = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var diagnostics = WorldGenerationRecipeValidator.Validate(recipe).ToList();
        if (projectRevision < 0)
            diagnostics.Add(new("world_generation.project_revision_invalid", "Project revision cannot be negative.", "world-generation/projectRevision"));
        if (transactionId == Guid.Empty)
            diagnostics.Add(new("world_generation.transaction_required", "A transaction ID is required.", "world-generation/transactionId"));
        if (planId == Guid.Empty)
            diagnostics.Add(new("world_generation.plan_required", "A plan ID is required.", "world-generation/planId"));

        var selectedStages = stages is null
            ? Enum.GetValues<WorldGenerationStage>()
            : stages.Distinct().Where(Enum.IsDefined).Order().ToArray();
        if (stages is { Count: 0 } || selectedStages.Length == 0)
            diagnostics.Add(new("world_generation.stages_required",
                "At least one world-generation stage must be selected.", "world-generation/stages"));
        if (stages is not null && stages.Any(stage => !Enum.IsDefined(stage)))
            diagnostics.Add(new("world_generation.stage_invalid",
                "The execution request contains an unknown world-generation stage.", "world-generation/stages"));

        var steps = selectedStages
            .Select((stage, index) => BuildStep(recipe, stage, index))
            .ToArray();
        var recipeFingerprint = recipe.Fingerprint();
        var readyToApply = diagnostics.Count == 0 && recipe.ApprovedForGeneration;
        var planFingerprint = WorldGenerationExecutionPlanFingerprint.Compute(
            projectRevision,
            recipeRevision,
            recipe.Id,
            recipe.AreaId,
            recipe.Seed,
            recipeFingerprint,
            readyToApply,
            steps);
        return new(
            planId,
            transactionId,
            projectRevision,
            recipeRevision,
            recipe.Id,
            recipe.AreaId,
            recipe.Seed,
            recipeFingerprint,
            planFingerprint,
            steps,
            diagnostics,
            readyToApply);
    }

    private static WorldGenerationExecutionStep BuildStep(
        WorldGenerationRecipe recipe,
        WorldGenerationStage stage,
        int order)
    {
        var backend = ResolveBackend(stage, recipe.BackendFor(stage));
        var parameters = BuildParameters(recipe, stage);
        return new(
            order,
            stage,
            backend,
            WorldGenerationOperations.For(stage, backend),
            parameters,
            order == 0 ? [] : [order - 1]);
    }

    private static WorldGenerationBackend ResolveBackend(
        WorldGenerationStage stage,
        WorldGenerationBackend requested)
    {
        if (requested != WorldGenerationBackend.Auto) return requested;
        return stage switch
        {
            WorldGenerationStage.Terrain or WorldGenerationStage.Hydrology => WorldGenerationBackend.Terrain3D,
            WorldGenerationStage.Biomes or WorldGenerationStage.Roads or WorldGenerationStage.Settlements or
                WorldGenerationStage.Scatter => WorldGenerationBackend.PcgGodot,
            _ => WorldGenerationBackend.GodotNative,
        };
    }

    private static IReadOnlyDictionary<string, string> BuildParameters(
        WorldGenerationRecipe recipe,
        WorldGenerationStage stage)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["areaId"] = recipe.AreaId.ToString("D"),
            ["recipeId"] = recipe.Id.ToString("D"),
            ["seed"] = recipe.Seed.ToString(CultureInfo.InvariantCulture),
        };
        switch (stage)
        {
            case WorldGenerationStage.Terrain:
                values["archetype"] = recipe.Terrain.Archetype;
                values["roughness"] = Number(recipe.Terrain.Roughness);
                values["erosionPasses"] = recipe.Terrain.ErosionPasses.ToString(CultureInfo.InvariantCulture);
                values["adaptToStructures"] = recipe.Terrain.AdaptToStructures.ToString(CultureInfo.InvariantCulture);
                values["widthMeters"] = Number(recipe.Bounds.WidthMeters);
                values["depthMeters"] = Number(recipe.Bounds.DepthMeters);
                values["maxElevationMeters"] = Number(recipe.Bounds.MaxElevationMeters);
                break;
            case WorldGenerationStage.Hydrology:
                values["riverCount"] = recipe.Rivers.Count.ToString(CultureInfo.InvariantCulture);
                values["riverIds"] = string.Join(',', recipe.Rivers.Select(item => item.Id.ToString("D")));
                break;
            case WorldGenerationStage.Biomes:
                values["biomeCount"] = recipe.Biomes.Count.ToString(CultureInfo.InvariantCulture);
                values["biomeIds"] = string.Join(',', recipe.Biomes.Select(item => item.Id.ToString("D")));
                break;
            case WorldGenerationStage.Roads:
                values["roadCount"] = recipe.Roads.Count.ToString(CultureInfo.InvariantCulture);
                values["roadIds"] = string.Join(',', recipe.Roads.Select(item => item.Id.ToString("D")));
                break;
            case WorldGenerationStage.Settlements:
                values["settlementCount"] = recipe.Settlements.Count.ToString(CultureInfo.InvariantCulture);
                values["settlementIds"] = string.Join(',', recipe.Settlements.Select(item => item.Id.ToString("D")));
                values["adaptTerrain"] = recipe.Terrain.AdaptToStructures.ToString(CultureInfo.InvariantCulture);
                break;
            case WorldGenerationStage.Scatter:
                values["scatterRuleCount"] = recipe.Scatter.Count.ToString(CultureInfo.InvariantCulture);
                values["scatterRuleIds"] = string.Join(',', recipe.Scatter.Select(item => item.Id.ToString("D")));
                values["generateLod"] = recipe.Quality.GenerateLod.ToString(CultureInfo.InvariantCulture);
                break;
            case WorldGenerationStage.Gameplay:
                values["encounterTables"] = string.Join(',', recipe.Gameplay.EncounterTables);
                values["wildlifeTables"] = string.Join(',', recipe.Gameplay.WildlifeTables);
                values["lootTables"] = string.Join(',', recipe.Gameplay.LootTables);
                values["questAnchors"] = recipe.Gameplay.GenerateQuestAnchors.ToString(CultureInfo.InvariantCulture);
                break;
            case WorldGenerationStage.Navigation:
                values["bakeNavigation"] = recipe.Navigation.BakeNavigation.ToString(CultureInfo.InvariantCulture);
                values["validateReachability"] = recipe.Navigation.ValidateReachability.ToString(CultureInfo.InvariantCulture);
                values["agentRadiusMeters"] = Number(recipe.Navigation.AgentRadiusMeters);
                break;
            case WorldGenerationStage.Validation:
                values["streamingCellSizeMeters"] = recipe.Quality.StreamingCellSizeMeters.ToString(CultureInfo.InvariantCulture);
                values["maxObjectsPerCell"] = recipe.Quality.MaxObjectsPerCell.ToString(CultureInfo.InvariantCulture);
                values["runAutomatedPlaytest"] = recipe.Quality.RunAutomatedPlaytest.ToString(CultureInfo.InvariantCulture);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown world-generation stage.");
        }
        return values;
    }

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}

public sealed class WorldGenerationOrchestrator
{
    private readonly IReadOnlyDictionary<WorldGenerationBackend, IWorldGenerationAdapter> _adapters;

    public WorldGenerationOrchestrator(IEnumerable<IWorldGenerationAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        _adapters = adapters.GroupBy(item => item.Backend).ToDictionary(group => group.Key, group => group.Single());
    }

    public async ValueTask<WorldGenerationExecutionResult> ExecuteAsync(
        WorldGenerationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Plan.ReadyToApply)
            return new(false, context.Plan, [], context.Plan.Diagnostics);

        var results = new List<WorldGenerationStepResult>();
        var diagnostics = new List<WorldGenerationValidationIssue>();
        foreach (var step in context.Plan.Steps.OrderBy(item => item.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var adapter = ResolveAdapter(step);
            if (adapter is null)
            {
                diagnostics.Add(new("world_generation.adapter_missing", $"No adapter can execute '{step.Operation}'.", $"world-generation/steps/{step.Order}"));
                break;
            }
            var result = await adapter.ExecuteAsync(context, step, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            diagnostics.AddRange(result.Diagnostics);
            if (!result.Succeeded) break;
        }
        return new(diagnostics.Count == 0 && results.Count == context.Plan.Steps.Count, context.Plan, results, diagnostics);
    }

    private IWorldGenerationAdapter? ResolveAdapter(WorldGenerationExecutionStep step)
    {
        if (_adapters.TryGetValue(step.Backend, out var requested) && requested.CanExecute(step)) return requested;
        return _adapters.TryGetValue(WorldGenerationBackend.GodotNative, out var fallback) && fallback.CanExecute(step)
            ? fallback
            : null;
    }
}
