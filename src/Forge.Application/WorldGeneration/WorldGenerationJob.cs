using Forge.Domain.WorldGeneration;

namespace Forge.Application.WorldGeneration;

public enum WorldGenerationJobStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
}

public sealed record WorldGenerationJob(
    int SchemaVersion,
    Guid JobId,
    Guid PlanId,
    long ProjectRevision,
    WorldGenerationRecipeRevision RecipeRevision,
    string RecipeFingerprint,
    WorldGenerationJobStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    WorldGenerationExecutionPlan Plan,
    WorldGenerationRecipe Recipe,
    IReadOnlyList<WorldGenerationValidationIssue> Diagnostics)
{
    public const int CurrentSchemaVersion = 1;

    public static WorldGenerationJob Queue(
        WorldGenerationExecutionPlan plan,
        WorldGenerationRecipe recipe,
        DateTimeOffset now) => new(
        CurrentSchemaVersion,
        Guid.NewGuid(),
        plan.PlanId,
        plan.ProjectRevision,
        plan.RecipeRevision,
        plan.RecipeFingerprint,
        WorldGenerationJobStatus.Queued,
        now,
        now,
        plan,
        recipe,
        []);

    public WorldGenerationJob WithStatus(
        WorldGenerationJobStatus status,
        DateTimeOffset now,
        IReadOnlyList<WorldGenerationValidationIssue>? diagnostics = null) =>
        this with
        {
            Status = status,
            UpdatedAtUtc = now,
            Diagnostics = diagnostics ?? Diagnostics,
        };
}

public static class WorldGenerationJobValidator
{
    public static IReadOnlyList<WorldGenerationValidationIssue> Validate(WorldGenerationJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        var diagnostics = new List<WorldGenerationValidationIssue>();
        if (job.SchemaVersion != WorldGenerationJob.CurrentSchemaVersion)
            diagnostics.Add(new("world_generation.job_schema_unsupported", "World-generation job schema is unsupported.", "world-generation/job/schemaVersion"));
        if (job.JobId == Guid.Empty || job.PlanId == Guid.Empty || job.Plan.PlanId != job.PlanId)
            diagnostics.Add(new("world_generation.job_id_invalid", "World-generation job identifiers are invalid.", "world-generation/job"));
        if (job.ProjectRevision < 0 || job.ProjectRevision != job.Plan.ProjectRevision ||
            job.RecipeRevision != job.Plan.RecipeRevision)
            diagnostics.Add(new("world_generation.job_revision_invalid", "World-generation job revisions do not match its execution plan.", "world-generation/job"));
        if (!string.Equals(job.RecipeFingerprint, job.Recipe.Fingerprint(), StringComparison.Ordinal) ||
            !string.Equals(job.RecipeFingerprint, job.Plan.RecipeFingerprint, StringComparison.Ordinal))
            diagnostics.Add(new("world_generation.job_fingerprint_invalid", "World-generation job fingerprint does not match its recipe and plan.", "world-generation/job"));
        if (job.Plan.RecipeId != job.Recipe.Id || job.Plan.AreaId != job.Recipe.AreaId)
            diagnostics.Add(new("world_generation.job_recipe_invalid", "World-generation job recipe does not match its execution plan.", "world-generation/job"));
        if (!job.Plan.ReadyToApply)
            diagnostics.Add(new("world_generation.job_not_ready", "World-generation plan is not approved and ready to apply.", "world-generation/job/plan"));
        diagnostics.AddRange(WorldGenerationRecipeValidator.Validate(job.Recipe));
        return diagnostics;
    }
}
