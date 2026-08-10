using Forge.Application.Commands;
using Forge.Domain.Content;

namespace Forge.Mcp.Projects;

public sealed record ContentPackageResource(
    int SchemaVersion,
    long Revision,
    Guid CatalogId,
    IReadOnlyList<ContentPackageDefinition> Packages);

public sealed record ContentPackagePlanRequest(
    int SchemaVersion,
    long BaseRevision,
    ContentPackageCatalog Catalog);

public sealed record ContentPackagePlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool DryRun);
