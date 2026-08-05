using Forge.Application.Commands;
using Forge.Domain.Gameplay;

namespace Forge.Mcp.Projects;

public sealed record GameplayControlResource(int SchemaVersion, long Revision,
    ModuleGameplayControlProfile Profile);

public sealed record GameplayControlPlanRequest(int SchemaVersion, long BaseRevision,
    ModuleGameplayControlProfile Profile);

public sealed record GameplayControlPlanResult(bool Succeeded, string? PlanId, long BaseRevision,
    ProjectPatch? Diff, ProjectPatch? Inverse, IReadOnlyList<AgentDiagnostic> Diagnostics, bool Applied,
    bool Reverted, IReadOnlyList<GameplayControlAuditEntry> Audit);

public sealed record GameplayControlAuditEntry(string PlanId, string Action, long Revision,
    DateTimeOffset OccurredAt, bool Succeeded);
