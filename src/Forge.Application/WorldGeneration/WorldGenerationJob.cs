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

public sealed record WorldGenerationJobAuditEntry(
    string Action,
    DateTimeOffset OccurredAtUtc,
    bool Succeeded,
    string Detail);

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
    IReadOnlyList<WorldGenerationValidationIssue> Diagnostics,
    IReadOnlyList<WorldGenerationJobAuditEntry> Audit)
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
        [],
        [new("queued", now, true, "Execution plan persisted for the Godot worker.")]);

    public WorldGenerationJob WithStatus(
        WorldGenerationJobStatus status,
        DateTimeOffset now,
        IReadOnlyList<WorldGenerationValidationIssue>? diagnostics = null) =>
        this with
        {
            Status = status,
            UpdatedAtUtc = now,
            Diagnostics = diagnostics ?? Diagnostics,
            Audit = Audit.Concat([new WorldGenerationJobAuditEntry(
                status.ToString().ToLowerInvariant(),
                now,
                status is not WorldGenerationJobStatus.Failed,
                diagnostics is { Count: > 0 }
                    ? string.Join(" | ", diagnostics.Select(item => $"{item.Code}: {item.Message}"))
                    : $"Job transitioned to {status}.")]).ToArray(),
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
        if (!Enum.IsDefined(job.Status))
            diagnostics.Add(new("world_generation.job_status_invalid", "World-generation job status is invalid.", "world-generation/job/status"));
        if (job.Plan is null || job.Recipe is null)
        {
            diagnostics.Add(new("world_generation.job_payload_missing",
                "World-generation job plan or recipe is missing.", "world-generation/job"));
            return diagnostics;
        }
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
        if (!string.Equals(job.Plan.PlanFingerprint,
                WorldGenerationExecutionPlanFingerprint.Compute(job.Plan), StringComparison.Ordinal))
            diagnostics.Add(new("world_generation.plan_fingerprint_invalid",
                "World-generation plan fingerprint does not match its execution steps.",
                "world-generation/job/planFingerprint"));
        if (job.Diagnostics is null || job.Diagnostics.Any(item => item is null ||
                string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Message) ||
                string.IsNullOrWhiteSpace(item.Path)))
            diagnostics.Add(new("world_generation.job_diagnostics_invalid",
                "World-generation job diagnostics are invalid.", "world-generation/job/diagnostics"));
        if (job.Audit is null || job.Audit.Count == 0 || job.Audit.Any(item => item is null ||
                string.IsNullOrWhiteSpace(item.Action) || item.Action.Length > 64 ||
                string.IsNullOrWhiteSpace(item.Detail) || item.Detail.Length > 4096 ||
                item.OccurredAtUtc == default) ||
            job.Audit.Zip(job.Audit.Skip(1), (left, right) => left.OccurredAtUtc <= right.OccurredAtUtc).Any(valid => !valid))
            diagnostics.Add(new("world_generation.job_audit_invalid",
                "World-generation job audit entries are missing or not chronological.", "world-generation/job/audit"));
        diagnostics.AddRange(WorldGenerationRecipeValidator.Validate(job.Recipe));
        return diagnostics;
    }
}
