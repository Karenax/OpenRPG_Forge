using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Domain.Localization;

namespace Forge.Mcp.Projects;

public sealed record LocalizationResource(
    int SchemaVersion,
    long Revision,
    Guid CatalogId,
    string DefaultLocale,
    string PackageNamespace,
    string LicenseSpdx,
    string ContentSha256,
    IReadOnlyList<LocalizationMessage> Messages);

public sealed record LocalizationPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    LocalizationCatalog Catalog);

public sealed record LocalizationPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool DryRun);

public sealed record UnifiedContentResourceQuery(
    string? Text = null,
    string? PackageNamespace = null,
    string? Kind = null,
    string? LicenseSpdx = null,
    string? Status = null,
    int MaximumResults = 300);

public sealed record UnifiedContentResourceResult(
    int SchemaVersion,
    long Revision,
    int Total,
    IReadOnlyList<string> PackageNamespaces,
    IReadOnlyList<string> Kinds,
    IReadOnlyList<string> Licenses,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<UnifiedContentBrowserItem> Items,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record ContentResourceAliasPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    Guid ResourceId,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> References);

public sealed record ContentResourceAliasPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool DryRun);
