namespace Forge.Mcp.Assets;

using Forge.Application.Commands;

public sealed record AgentAssetCatalogQuery(
    string? Text = null,
    string? Source = null,
    string? Category = null,
    string? LicenseSpdx = null,
    string? Availability = null,
    int MaximumResults = 100,
    bool? Installed = null,
    string? Tag = null,
    double? MaximumWidth = null,
    double? MaximumHeight = null,
    double? MaximumDepth = null,
    string? Style = null,
    string? Skeleton = null,
    string? Material = null,
    long? MaximumTriangleCount = null,
    string? Author = null,
    string? Compatibility = null,
    string? Variant = null);

public sealed record AgentAssetTechnicalMetadata(
    double? Width,
    double? Height,
    double? Depth,
    string Style,
    string Skeleton,
    IReadOnlyList<string> Materials,
    long? TriangleCount,
    IReadOnlyList<string> Variants,
    IReadOnlyList<string> Animations,
    IReadOnlyList<string> Compatibility);

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
    IReadOnlyList<string> InstalledPackIds,
    AgentAssetTechnicalMetadata TechnicalMetadata);

public sealed record AgentAssetCatalogResult(
    int SchemaVersion,
    int CatalogCount,
    int LocalCount,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> Categories,
    IReadOnlyList<AgentAssetCatalogItem> Items,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AgentAssetInstallPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AssetId);

public sealed record AgentAssetInstallPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    string? AssetId,
    string? LicenseSpdx,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<AgentAssetInstallAuditEntry> Audit,
    bool Reused = false);

public sealed record AgentAssetInstallAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded,
    string AssetId,
    string LicenseSpdx);
