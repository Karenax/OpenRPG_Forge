using Forge.Application.Commands;
using Forge.Domain.Audio;

namespace Forge.Mcp.Projects;

public sealed record AudioEmitterResource(
    int SchemaVersion,
    long Revision,
    string AreaId,
    AudioEmitterCatalog Catalog);

public sealed record AudioEmitterPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    AudioEmitterCatalog Catalog);

public sealed record AudioEmitterPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<AudioEmitterAuditEntry> Audit);

public sealed record AudioEmitterAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
