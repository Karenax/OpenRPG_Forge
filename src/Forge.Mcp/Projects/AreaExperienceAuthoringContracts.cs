using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Domain.Areas;

namespace Forge.Mcp.Projects;

public sealed record AreaExperienceAuthoringResource(
    int SchemaVersion,
    long Revision,
    string AreaId,
    string? DisplayName,
    AreaAuthoringProfile? Authoring,
    AreaExperienceAuthoringScriptSnapshot? RuntimeView,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AreaExperienceAuthoringPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    AreaAuthoringProfile Authoring);

public sealed record AreaExperienceAuthoringPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<AreaExperienceAuthoringAuditEntry> Audit);

public sealed record AreaExperienceAuthoringAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
