using Forge.Application.Commands;
using Forge.Domain.Entities;

namespace Forge.Mcp.Projects;

public sealed record EntityOverridesPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    string EntityId,
    EntityPropertyOverrides Overrides)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record EntityOverridesPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<EntityOverridesAuditEntry> Audit);

public sealed record EntityOverridesAuditEntry(
    string PlanId,
    string Operation,
    long Revision,
    DateTimeOffset Timestamp,
    bool Succeeded);
