using Forge.Application.Commands;

namespace Forge.Mcp.Projects;

public sealed record EntityTagsPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    string EntityId,
    IReadOnlyList<string> Tags)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record EntityTagsPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<EntityTagsAuditEntry> Audit);

public sealed record EntityTagsAuditEntry(
    string PlanId,
    string Operation,
    long Revision,
    DateTimeOffset Timestamp,
    bool Succeeded);
