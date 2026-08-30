namespace Forge.Domain.Assets;

public sealed record AssetVaultInstalledAsset
{
    public AssetCatalogItemId AssetId { get; }

    public string ExternalId { get; }

    public string DisplayName { get; }

    public string Source { get; }

    public string SourcePage { get; }

    public string Author { get; }

    public string LicenseSpdx { get; }

    public string LicenseUrl { get; }

    public bool AttributionRequired { get; }

    public string AttributionText { get; }

    public IReadOnlyList<AssetVaultInstalledFile> Files { get; }

    public AssetIntelligenceProfile? Intelligence { get; }

    public AssetVaultInstalledAsset(
        AssetCatalogItemId assetId,
        string externalId,
        string displayName,
        string source,
        string sourcePage,
        string author,
        string licenseSpdx,
        string licenseUrl,
        bool attributionRequired,
        string attributionText,
        IReadOnlyList<AssetVaultInstalledFile> files,
        AssetIntelligenceProfile? intelligence = null)
    {
        ArgumentNullException.ThrowIfNull(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePage);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseSpdx);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseUrl);
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0 || files.All(file => file.Role != AssetVaultFileRole.Model))
        {
            throw new ArgumentException("An installed asset requires at least one model file.", nameof(files));
        }

        if (files.GroupBy(file => file.Role).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("An installed asset cannot contain duplicate file roles.", nameof(files));
        }

        AssetId = assetId;
        ExternalId = externalId.Trim();
        DisplayName = displayName.Trim();
        Source = source.Trim().ToLowerInvariant();
        SourcePage = sourcePage.Trim();
        Author = author.Trim();
        LicenseSpdx = licenseSpdx.Trim();
        LicenseUrl = licenseUrl.Trim();
        AttributionRequired = attributionRequired;
        AttributionText = attributionText?.Trim() ?? string.Empty;
        Files = files.OrderBy(file => file.Role).ToArray();
        if (intelligence is not null)
        {
            if (!string.Equals(intelligence.ExternalId, ExternalId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Installed asset intelligence external ID does not match the asset.", nameof(intelligence));
            }

            var diagnostic = AssetIntelligenceProfileValidator.Validate(intelligence).FirstOrDefault();
            if (diagnostic is not null)
            {
                throw new ArgumentException(diagnostic.Message, nameof(intelligence));
            }
        }

        Intelligence = intelligence;
    }
}
