using Forge.Application.Commands;
using Forge.Domain.Spatial;

namespace Forge.Mcp.Projects;

public sealed record EntityTransformPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    string EntityId,
    TransformValue Transform)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record EntityTransformPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<EntityTransformAuditEntry> Audit);

public sealed record EntityTransformAuditEntry(
    string PlanId,
    string Operation,
    long Revision,
    DateTimeOffset Timestamp,
    bool Succeeded);
