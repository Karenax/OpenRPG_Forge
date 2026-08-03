namespace Forge.Domain.Assets;

public sealed record AssetCatalogItem
{
    public const int CurrentSchemaVersion = 1;

    public AssetCatalogItemId Id { get; }

    public int SchemaVersion { get; }

    public string ExternalId { get; }

    public string DisplayName { get; }

    public string Source { get; }

    public string SourceAssetId { get; }

    public string SourcePage { get; }

    public string LicenseSpdx { get; }

    public string LicenseUrl { get; }

    public bool LicenseVerified { get; }

    public string ContentSha256 { get; }

    public long TotalBytes { get; }

    public IReadOnlyList<string> Categories { get; }

    public IReadOnlyList<string> Tags { get; }

    public string CatalogStatus { get; }

    public AssetAvailability Availability { get; }

    public string? RelativeModelPath { get; }

    public string? RelativePreviewPath { get; }

    public string? ThumbnailUrl { get; }

    public string Provenance { get; }

    public bool CanPreviewLocally => Availability != AssetAvailability.CatalogOnly &&
        !string.IsNullOrWhiteSpace(RelativePreviewPath);

    public bool CanPlanImport => LicenseVerified && Availability != AssetAvailability.CatalogOnly &&
        !string.IsNullOrWhiteSpace(RelativeModelPath);

    public AssetCatalogItem(
        AssetCatalogItemId id,
        string externalId,
        string displayName,
        string source,
        string sourceAssetId,
        string sourcePage,
        string licenseSpdx,
        string licenseUrl,
        bool licenseVerified,
        string contentSha256,
        long totalBytes,
        IReadOnlyList<string>? categories,
        IReadOnlyList<string>? tags,
        string catalogStatus,
        AssetAvailability availability,
        string? relativeModelPath,
        string? relativePreviewPath,
        string? thumbnailUrl,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAssetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePage);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseSpdx);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentOutOfRangeException.ThrowIfNegative(totalBytes);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion),
                $"Asset catalog item schema '{schemaVersion}' is not supported.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        ExternalId = externalId.Trim();
        DisplayName = displayName.Trim();
        Source = source.Trim().ToLowerInvariant();
        SourceAssetId = sourceAssetId.Trim();
        SourcePage = sourcePage.Trim();
        LicenseSpdx = licenseSpdx.Trim();
        LicenseUrl = licenseUrl.Trim();
        LicenseVerified = licenseVerified;
        ContentSha256 = contentSha256.Trim().ToLowerInvariant();
        TotalBytes = totalBytes;
        Categories = Normalize(categories);
        Tags = Normalize(tags);
        CatalogStatus = catalogStatus.Trim();
        Availability = availability;
        RelativeModelPath = NormalizePath(relativeModelPath);
        RelativePreviewPath = NormalizePath(relativePreviewPath);
        ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl.Trim();
        Provenance = provenance.Trim();
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values) => (values ?? [])
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim().ToLowerInvariant())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static string? NormalizePath(string? path) => string.IsNullOrWhiteSpace(path)
        ? null
        : path.Replace('\\', '/').Trim();
}
