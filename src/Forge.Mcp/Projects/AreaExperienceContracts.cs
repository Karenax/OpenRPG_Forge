using Forge.Application.Commands;
using Forge.Domain.RuntimePolicies;

namespace Forge.Mcp.Projects;

public sealed record AreaExperienceResource(int SchemaVersion, long Revision, string AreaId,
    AreaExperienceProfile Profile);

public sealed record AreaExperiencePlanRequest(int SchemaVersion, long BaseRevision, string AreaId,
    AreaExperienceProfile Profile);

public sealed record AreaExperiencePlanResult(bool Succeeded, string? PlanId, long BaseRevision,
    ProjectPatch? Diff, ProjectPatch? Inverse, IReadOnlyList<AgentDiagnostic> Diagnostics, bool Applied,
    bool Reverted, IReadOnlyList<AreaExperienceAuditEntry> Audit);

public sealed record AreaExperienceAuditEntry(string PlanId, string Action, long Revision,
    DateTimeOffset OccurredAt, bool Succeeded);
