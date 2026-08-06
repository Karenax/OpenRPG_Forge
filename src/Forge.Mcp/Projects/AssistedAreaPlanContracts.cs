using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Domain.Assistant;

namespace Forge.Mcp.Projects;

public sealed record AssistedAreaPlanResource(
    int SchemaVersion,
    long Revision,
    AssistedAreaPlanCatalog Catalog,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AssistedAreaPlanProposalRequest(
    int SchemaVersion,
    AssistedAreaPlanBrief Brief);

public sealed record AssistedAreaPlanProposalResource(
    bool Succeeded,
    AssistedAreaPlanDefinition? Plan,
    string SemanticDiff,
    string PromptSha256,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    bool GeometryApplied,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AssistedAreaPlanPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    AssistedAreaPlanCatalog Catalog,
    AssistantPermissionLevel Permission);

public sealed record AssistedAreaPlanPlanResult(
    bool Succeeded,
    string? PlanId,
    string? TransactionId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<AssistedAreaPlanAuditEntry> Audit);

public sealed record AssistedAreaPlanAuditEntry(
    string PlanId,
    string TransactionId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
