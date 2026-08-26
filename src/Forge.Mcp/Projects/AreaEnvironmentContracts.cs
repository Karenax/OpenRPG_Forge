using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Domain.Areas;

namespace Forge.Mcp.Projects;

public sealed record AreaEnvironmentResource(
    int SchemaVersion,
    long Revision,
    string AreaId,
    string? DisplayName,
    AreaEnvironmentProfile? Environment,
    AreaEnvironmentScriptSnapshot? RuntimeView,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AreaEnvironmentPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    AreaEnvironmentProfile? Environment = null,
    AreaEnvironmentPatch? Patch = null);

public sealed record AreaEnvironmentPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<AreaEnvironmentAuditEntry> Audit)
{
    public IReadOnlyList<string> ChangedPaths { get; init; } = [];
}

public sealed record AreaEnvironmentAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
