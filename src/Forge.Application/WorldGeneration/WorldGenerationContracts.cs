using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Domain.WorldGeneration;

namespace Forge.Application.WorldGeneration;

public readonly record struct WorldGenerationRecipeRevision(long Value)
{
    public static WorldGenerationRecipeRevision Zero => new(0);

    public WorldGenerationRecipeRevision Next()
    {
        if (Value == long.MaxValue)
            throw new InvalidOperationException("World-generation recipe revision overflow.");
        return new(Value + 1);
    }
}

public sealed record WorldGenerationRecipeState
{
    public WorldGenerationRecipeCatalog Catalog { get; }
    public WorldGenerationRecipeRevision Revision { get; }

    public WorldGenerationRecipeState(
        WorldGenerationRecipeCatalog? catalog = null,
        WorldGenerationRecipeRevision? revision = null)
    {
        Catalog = catalog ?? WorldGenerationRecipeCatalog.CreateDefault();
        Revision = revision ?? WorldGenerationRecipeRevision.Zero;
    }
}

public sealed record WorldGenerationExecutionStep(
    int Order,
    WorldGenerationStage Stage,
    WorldGenerationBackend Backend,
    string Operation,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<int> DependsOn);

public sealed record WorldGenerationExecutionPlan(
    Guid PlanId,
    Guid TransactionId,
    long ProjectRevision,
    WorldGenerationRecipeRevision RecipeRevision,
    Guid RecipeId,
    Guid AreaId,
    int Seed,
    string RecipeFingerprint,
    string PlanFingerprint,
    IReadOnlyList<WorldGenerationExecutionStep> Steps,
    IReadOnlyList<WorldGenerationValidationIssue> Diagnostics,
    bool ReadyToApply);

public sealed record WorldGenerationExecutionContext(
    string WorkspaceDirectory,
    WorldGenerationRecipe Recipe,
    WorldGenerationExecutionPlan Plan,
    bool DryRun,
    IWorldGenerationArtifactSink ArtifactSink);

public sealed record WorldGenerationStepResult(
    bool Succeeded,
    WorldGenerationStage Stage,
    WorldGenerationBackend Backend,
    IReadOnlyList<string> Artifacts,
    IReadOnlyList<WorldGenerationValidationIssue> Diagnostics);

public sealed record WorldGenerationExecutionResult(
    bool Succeeded,
    WorldGenerationExecutionPlan Plan,
    IReadOnlyList<WorldGenerationStepResult> Steps,
    IReadOnlyList<WorldGenerationValidationIssue> Diagnostics);

public interface IWorldGenerationArtifactSink
{
    ValueTask WriteTextAsync(string relativePath, string content, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}

public interface IWorldGenerationAdapter
{
    WorldGenerationBackend Backend { get; }
    bool CanExecute(WorldGenerationExecutionStep step);
    ValueTask<WorldGenerationStepResult> ExecuteAsync(
        WorldGenerationExecutionContext context,
        WorldGenerationExecutionStep step,
        CancellationToken cancellationToken = default);
}

public static class WorldGenerationOperations
{
    public static string For(WorldGenerationStage stage, WorldGenerationBackend backend) =>
        (backend, stage) switch
        {
            (WorldGenerationBackend.Terrain3D, WorldGenerationStage.Terrain) => "terrain3d.generate_heightfield",
            (WorldGenerationBackend.Terrain3D, WorldGenerationStage.Hydrology) => "terrain3d.carve_hydrology",
            (WorldGenerationBackend.PcgGodot, WorldGenerationStage.Biomes) => "pcgodot.generate_biomes",
            (WorldGenerationBackend.PcgGodot, WorldGenerationStage.Roads) => "pcgodot.generate_road_network",
            (WorldGenerationBackend.PcgGodot, WorldGenerationStage.Settlements) => "pcgodot.generate_settlements",
            (WorldGenerationBackend.PcgGodot, WorldGenerationStage.Scatter) => "pcgodot.scatter_environment",
            (_, WorldGenerationStage.Gameplay) => "godot.generate_gameplay_anchors",
            (_, WorldGenerationStage.Navigation) => "godot.bake_navigation",
            (_, WorldGenerationStage.Validation) => "godot.validate_generated_world",
            (_, _) when backend != WorldGenerationBackend.Auto =>
                $"{backend.ToString().ToLowerInvariant()}.{stage.ToString().ToLowerInvariant()}",
            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend,
                "Execution plans cannot contain the unresolved Auto backend."),
        };
}

public static class WorldGenerationExecutionPlanFingerprint
{
    public static string Compute(
        long projectRevision,
        WorldGenerationRecipeRevision recipeRevision,
        Guid recipeId,
        Guid areaId,
        int seed,
        string recipeFingerprint,
        bool readyToApply,
        IEnumerable<WorldGenerationExecutionStep> steps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeFingerprint);
        ArgumentNullException.ThrowIfNull(steps);
        var canonical = new StringBuilder();
        Append(canonical, projectRevision.ToString(CultureInfo.InvariantCulture));
        Append(canonical, recipeRevision.Value.ToString(CultureInfo.InvariantCulture));
        Append(canonical, recipeId.ToString("D"));
        Append(canonical, areaId.ToString("D"));
        Append(canonical, seed.ToString(CultureInfo.InvariantCulture));
        Append(canonical, recipeFingerprint);
        Append(canonical, readyToApply.ToString(CultureInfo.InvariantCulture));
        var materializedSteps = steps.ToArray();
        if (materializedSteps.Any(step => step is null || step.Parameters is null || step.DependsOn is null))
            throw new ArgumentException("World-generation execution steps cannot contain null values.", nameof(steps));
        foreach (var step in materializedSteps.OrderBy(item => item.Order))
        {
            Append(canonical, step.Order.ToString(CultureInfo.InvariantCulture));
            Append(canonical, step.Stage.ToString());
            Append(canonical, step.Backend.ToString());
            Append(canonical, step.Operation);
            foreach (var parameter in step.Parameters.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                Append(canonical, parameter.Key);
                Append(canonical, parameter.Value);
            }
            Append(canonical, string.Join(',', step.DependsOn.Order()));
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    public static string Compute(WorldGenerationExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return Compute(plan.ProjectRevision, plan.RecipeRevision, plan.RecipeId, plan.AreaId, plan.Seed,
            plan.RecipeFingerprint, plan.ReadyToApply, plan.Steps);
    }

    private static void Append(StringBuilder target, string value) =>
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append(';');
}
