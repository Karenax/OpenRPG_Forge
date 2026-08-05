using Forge.Application.Commands;
using Forge.Domain.RuntimePolicies;

namespace Forge.Mcp.Projects;

public sealed record RuntimePolicyResource(
    int SchemaVersion,
    long Revision,
    string AreaId,
    ModuleRuntimePolicyProfile Module,
    AreaRuntimePolicyProfile Area);

public sealed record RuntimePolicyPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    ModuleRuntimePolicyProfile Module,
    AreaRuntimePolicyProfile Area);

public sealed record RuntimePolicyPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<RuntimePolicyAuditEntry> Audit);

public sealed record RuntimePolicyAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
