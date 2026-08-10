using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Domain.Areas;

namespace Forge.Mcp.Projects;

public sealed record AreaFoundationResource(
    int SchemaVersion,
    long Revision,
    string AreaId,
    string? DisplayName,
    AreaFoundationProfile? Foundation,
    AreaFoundationScriptSnapshot? RuntimeView,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AreaFoundationPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    AreaFoundationProfile Foundation);

public sealed record AreaFoundationPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<AreaFoundationAuditEntry> Audit);

public sealed record AreaFoundationAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
