using Forge.Application.Commands;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.Mcp.Dialogues;

namespace Forge.Mcp.Assets;

public sealed record ExternalWorkerSecurityResource(
    int SchemaVersion,
    long Revision,
    ExternalWorkerSecurityPolicy Policy,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record ExternalWorkerSecurityPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    ExternalWorkerSecurityPolicy Policy,
    AssistantPermissionLevel Permission);

public sealed record ExternalWorkerSecurityPlanResult(
    bool Succeeded,
    string? PlanId,
    string? TransactionId,
    long BaseRevision,
    long? AppliedRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    decimal EstimatedCostUsd,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<PlanAuditEntry> Audit);
