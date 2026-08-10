using Forge.Application.Commands;
using Forge.Domain.Navigation;

namespace Forge.Mcp.Navigation;

public sealed record AnchorResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    IReadOnlyList<AnchorResourceItem> Items);

public sealed record AnchorResourceItem(
    string AreaId,
    string CatalogId,
    string InstanceId,
    string DefinitionId,
    string Slug,
    string LabelKey,
    string Label,
    string Kind,
    string Scope,
    IReadOnlyList<string> Tags,
    float X,
    float Y,
    float Z,
    bool IsSpawn,
    bool IsPortal,
    bool RequiredAtRuntime);

public sealed record AnchorPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    AnchorCatalog Catalog);

public sealed record AnchorPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
