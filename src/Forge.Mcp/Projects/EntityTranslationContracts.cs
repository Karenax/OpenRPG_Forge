using Forge.Application.Commands;
using Forge.Domain.Spatial;

namespace Forge.Mcp.Projects;

public sealed record EntityTranslationPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    IReadOnlyList<string> EntityIds,
    Vector3Value Delta);

public sealed record EntityTranslationPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<EntityTranslationAuditEntry> Audit);

public sealed record EntityTranslationAuditEntry(
    string PlanId,
    string Operation,
    long Revision,
    DateTimeOffset Timestamp,
    bool Succeeded);
