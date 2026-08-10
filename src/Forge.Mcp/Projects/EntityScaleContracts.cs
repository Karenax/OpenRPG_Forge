using Forge.Application.Commands;
using Forge.Domain.Spatial;

namespace Forge.Mcp.Projects;

public sealed record EntityScaleResource(
    int SchemaVersion,
    long Revision,
    string? AreaId,
    IReadOnlyList<EntityScaleItem> Entities,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record EntityScaleItem(
    string EntityId,
    Vector3Value Scale,
    string DocumentPath);

public sealed record EntityScalePlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    IReadOnlyList<string> EntityIds,
    Vector3Value Scale,
    bool Multiply = false);

public sealed record EntityScalePlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<EntityScaleAuditEntry> Audit);

public sealed record EntityScaleAuditEntry(
    string PlanId,
    string Operation,
    long Revision,
    DateTimeOffset Timestamp,
    bool Succeeded);
