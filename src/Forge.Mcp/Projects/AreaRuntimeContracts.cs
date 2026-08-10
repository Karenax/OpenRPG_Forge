using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Domain.Areas;

namespace Forge.Mcp.Projects;

public sealed record AreaRuntimeResource(
    int SchemaVersion,
    long Revision,
    string AreaId,
    string? DisplayName,
    AreaNavigationScriptSnapshot? Navigation,
    IReadOnlyList<AreaPortalScriptSnapshot> Portals,
    AreaPlaytestProfile? Playtest,
    AreaReadinessReport? Readiness,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AreaPlaytestPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    AreaPlaytestProfile Profile);

public sealed record AreaPlaytestPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<AreaPlaytestAuditEntry> Audit);

public sealed record AreaPlaytestAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
