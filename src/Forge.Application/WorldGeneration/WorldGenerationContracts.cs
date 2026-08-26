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
