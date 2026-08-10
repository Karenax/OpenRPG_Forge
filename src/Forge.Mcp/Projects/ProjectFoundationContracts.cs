using Forge.Application.Commands;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Mcp.Projects;

public sealed record ProjectFoundationResource(
    int SchemaVersion,
    long Revision,
    ProjectMetadata Metadata,
    ProjectFoundationProfile? Foundation,
    ProjectTechnicalIdentity? Identity);

public sealed record ProjectFoundationPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    ProjectFoundationProfile Foundation);

public sealed record ProjectFoundationPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<ProjectFoundationAuditEntry> Audit);

public sealed record ProjectFoundationAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
