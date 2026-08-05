using Forge.Application.Commands;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Mcp.Projects;

public sealed record RuntimeExtensionResource(int SchemaVersion, long Revision,
    ModuleRuntimeExtensionProfile Profile);

public sealed record RuntimeExtensionPlanRequest(int SchemaVersion, long BaseRevision,
    ModuleRuntimeExtensionProfile Profile);

public sealed record RuntimeExtensionPlanResult(bool Succeeded, string? PlanId, long BaseRevision,
    ProjectPatch? Diff, ProjectPatch? Inverse, IReadOnlyList<AgentDiagnostic> Diagnostics, bool Applied,
    bool Reverted, IReadOnlyList<RuntimeExtensionAuditEntry> Audit);

public sealed record RuntimeExtensionAuditEntry(string PlanId, string Action, long Revision,
    DateTimeOffset OccurredAt, bool Succeeded);
