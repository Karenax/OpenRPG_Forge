using Forge.Application.Commands;
using Forge.Domain.Sequences;

namespace Forge.Mcp.Projects;

public sealed record CinematicResource(int SchemaVersion, long Revision, string AreaId,
    ModuleCinematicProfile Module, AreaCinematicProfile Area);

public sealed record CinematicPlanRequest(int SchemaVersion, long BaseRevision, string AreaId,
    ModuleCinematicProfile Module, AreaCinematicProfile Area);

public sealed record CinematicPlanResult(bool Succeeded, string? PlanId, long BaseRevision,
    ProjectPatch? Diff, ProjectPatch? Inverse, IReadOnlyList<AgentDiagnostic> Diagnostics, bool Applied,
    bool Reverted, IReadOnlyList<CinematicAuditEntry> Audit);

public sealed record CinematicAuditEntry(string PlanId, string Action, long Revision, DateTimeOffset OccurredAt,
    bool Succeeded);
