using Forge.Application.Commands;
using Forge.Domain.Quality;

namespace Forge.Mcp.Quality;

public sealed record QualityFoundationResource(
    int SchemaVersion,
    long Revision,
    QualityFoundationProfile Profile,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record QualityFoundationPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    QualityFoundationProfile Profile);

public sealed record QualityFoundationPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool DryRun);

public sealed record CanonicalValidationDiagnosticResource(
    string Id,
    string Category,
    string Code,
    string Message,
    string Path,
    string? SubjectUuid,
    string Provenance);

public sealed record CanonicalValidationResource(
    int SchemaVersion,
    bool Passed,
    string Fingerprint,
    IReadOnlyDictionary<string, int> CategoryCounts,
    IReadOnlyList<CanonicalValidationDiagnosticResource> Diagnostics);
