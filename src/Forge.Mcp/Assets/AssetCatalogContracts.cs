namespace Forge.Mcp.Assets;

public sealed record AgentAssetCatalogQuery(
    string? Text = null,
    string? Source = null,
    string? Category = null,
    string? LicenseSpdx = null,
    string? Availability = null,
    int MaximumResults = 100,
    bool? Installed = null);

public sealed record AgentAssetCatalogItem(
    string Id,
    int SchemaVersion,
    string ExternalId,
    string DisplayName,
    string Source,
    string SourceAssetId,
    string SourcePage,
    string LicenseSpdx,
    bool LicenseVerified,
    string ContentSha256,
    long TotalBytes,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Tags,
    string CatalogStatus,
    string Availability,
    string? RelativeModelPath,
    string? RelativePreviewPath,
    string? ThumbnailUrl,
    bool CanPlanImport,
    string Provenance,
    string Author,
    bool AttributionRequired,
    string AttributionText,
    bool Installed,
    IReadOnlyList<string> InstalledPackIds);

public sealed record AgentAssetCatalogResult(
    int SchemaVersion,
    int CatalogCount,
    int LocalCount,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> Categories,
    IReadOnlyList<AgentAssetCatalogItem> Items,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
