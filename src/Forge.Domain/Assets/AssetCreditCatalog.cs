namespace Forge.Domain.Assets;

public static class AssetCreditCatalog
{
    public static IReadOnlyList<AssetCreditEntry> Aggregate(IReadOnlyList<AssetCatalogItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return AggregateCore(items
            .Where(item => item.Installed)
            .Select(item => new CreditSource(
                item.Source,
                item.Author,
                item.LicenseSpdx,
                item.LicenseUrl,
                item.AttributionRequired,
                item.AttributionText,
                item.ExternalId,
                item.SourcePage)));
    }

    public static IReadOnlyList<AssetCreditEntry> Aggregate(AssetVaultInstallationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return AggregateCore(state.Packages
            .SelectMany(package => package.Assets)
            .Select(asset => new CreditSource(
                asset.Source,
                asset.Author,
                asset.LicenseSpdx,
                asset.LicenseUrl,
                asset.AttributionRequired,
                asset.AttributionText,
                asset.ExternalId,
                asset.SourcePage)));
    }

    private static IReadOnlyList<AssetCreditEntry> AggregateCore(IEnumerable<CreditSource> assets) => assets
            .GroupBy(
                asset => new
                {
                    asset.Source,
                    asset.Author,
                    asset.LicenseSpdx,
                    asset.LicenseUrl,
                    asset.AttributionRequired,
                    asset.AttributionText,
                })
            .Select(group => new AssetCreditEntry(
                group.Key.Source,
                group.Key.Author,
                group.Key.LicenseSpdx,
                group.Key.LicenseUrl,
                group.Key.AttributionRequired,
                group.Key.AttributionText,
                group.Select(asset => asset.ExternalId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                group.Select(asset => asset.SourcePage)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(entry => entry.Source, StringComparer.Ordinal)
            .ThenBy(entry => entry.Author, StringComparer.Ordinal)
            .ThenBy(entry => entry.LicenseSpdx, StringComparer.Ordinal)
            .ThenBy(entry => entry.AttributionText, StringComparer.Ordinal)
            .ToArray();

    private sealed record CreditSource(
        string Source,
        string Author,
        string LicenseSpdx,
        string LicenseUrl,
        bool AttributionRequired,
        string AttributionText,
        string ExternalId,
        string SourcePage);
}
