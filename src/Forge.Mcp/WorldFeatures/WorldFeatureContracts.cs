using Forge.Application.Commands;
using Forge.Domain.WorldFeatures;

namespace Forge.Mcp.WorldFeatures;

public sealed record WorldFeatureResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Kind,
    string? IdOrTarget,
    IReadOnlyList<WorldFeatureResourceItem> Items);

public sealed record WorldFeatureResourceItem(
    string Kind,
    string Id,
    string TargetId,
    string Provenance,
    IReadOnlyDictionary<string, string> Properties);

public sealed record WorldFeaturePlanRequest(
    int SchemaVersion,
    long BaseRevision,
    WorldFeatureCatalog Catalog);

public sealed record WorldFeaturePlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
