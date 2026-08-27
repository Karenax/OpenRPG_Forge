using Forge.Application.Commands;
using Forge.Application.WorldGeneration;
using Forge.Domain.Assistant;
using Forge.Domain.WorldGeneration;

namespace Forge.Mcp.Projects;

public sealed record WorldGenerationRecipeResource(
    int SchemaVersion,
    long RecipeRevision,
    long ProjectRevision,
    WorldGenerationRecipeCatalog Catalog,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record WorldGenerationRecipePlanRequest(
    int SchemaVersion,
    long BaseRecipeRevision,
    long BaseProjectRevision,
    WorldGenerationRecipeCatalog Catalog,
    AssistantPermissionLevel Permission);

public sealed record WorldGenerationRecipePlanResult(
    bool Succeeded,
    string? PlanId,
    string? TransactionId,
    long BaseRecipeRevision,
    long? NewRecipeRevision,
    long ProjectRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<WorldGenerationAuditEntry> Audit);

public sealed record WorldGenerationExecutionPreviewRequest(
    int SchemaVersion,
    long BaseProjectRevision,
    long BaseRecipeRevision,
    string RecipeId,
    IReadOnlyList<WorldGenerationStage>? Stages = null);

public sealed record WorldGenerationExecutionPlanResource(
    bool Succeeded,
    WorldGenerationExecutionPlan? Plan,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Queued,
    WorldGenerationJobStatus? JobStatus);

public sealed record WorldGenerationJobResource(
    string JobId,
    string PlanId,
    string RecipeId,
    string AreaId,
    string RecipeFingerprint,
    string PlanFingerprint,
    long ProjectRevision,
    long RecipeRevision,
    WorldGenerationJobStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    IReadOnlyList<WorldGenerationJobAuditEntry> Audit);

public sealed record WorldGenerationPromotionPlanRequest(
    int SchemaVersion,
    long BaseProjectRevision,
    long BaseRecipeRevision,
    string JobPlanId,
    string? ExpectedCanonicalSceneSha256,
    string AcceptanceProvenance,
    AssistantPermissionLevel Permission);

public sealed record WorldGenerationPromotionPlanResult(
    bool Succeeded,
    string? PromotionPlanId,
    string? TransactionId,
    string? JobPlanId,
    long BaseProjectRevision,
    long? NewProjectRevision,
    long BaseRecipeRevision,
    string? CanonicalSceneRelativePath,
    string? CanonicalSceneSha256,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<WorldGenerationAuditEntry> Audit);

public sealed record WorldGenerationCanonicalPromotionResource(
    bool Succeeded,
    WorldGenerationCanonicalPromotion? Promotion,
    string? SceneSha256,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record WorldGenerationAuditEntry(
    string PlanId,
    string TransactionId,
    string Action,
    long RecipeRevision,
    long ProjectRevision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
