using Forge.Application.Commands;
using Forge.Domain.Lifecycle;
using Forge.Domain.Modules;

namespace Forge.Mcp.Projects;

public sealed record ProjectWorldResource(
    bool Succeeded,
    long Revision,
    ProjectWorldProfile Profile,
    IReadOnlyList<CustomEventSchema> CustomEvents,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record ProjectDocumentationResource(
    bool Succeeded,
    long Revision,
    string DefaultLocale,
    IReadOnlyList<ProjectLocalizedMetadata> Localizations,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record ProjectDocumentationPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string DefaultLocale,
    IReadOnlyList<ProjectLocalizedMetadata> Localizations);

public sealed record ProjectDocumentationPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<ProjectDocumentationAuditEntry> Audit);

public sealed record ProjectDocumentationAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
