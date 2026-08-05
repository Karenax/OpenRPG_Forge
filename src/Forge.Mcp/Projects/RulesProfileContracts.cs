using Forge.Application.Commands;
using Forge.Domain.Rules;

namespace Forge.Mcp.Projects;

public sealed record RulesProfileResource(int SchemaVersion, long Revision, string AreaId,
    ModuleRulesProfile Module, AreaRulesProfile Area);

public sealed record RulesProfilePlanRequest(int SchemaVersion, long BaseRevision, string AreaId,
    ModuleRulesProfile Module, AreaRulesProfile Area);

public sealed record RulesProfilePlanResult(bool Succeeded, string? PlanId, long BaseRevision,
    ProjectPatch? Diff, ProjectPatch? Inverse, IReadOnlyList<AgentDiagnostic> Diagnostics, bool Applied,
    bool Reverted, IReadOnlyList<RulesProfileAuditEntry> Audit);

public sealed record RulesProfileAuditEntry(string PlanId, string Action, long Revision,
    DateTimeOffset OccurredAt, bool Succeeded);
