using System.Globalization;
using System.Text;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed class AssetCatalogBrowser
{
    private readonly AssetCatalog _catalog;

    public AssetCatalogBrowser(AssetCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public AssetCatalog Catalog => _catalog;

    public IReadOnlyList<string> Sources => _catalog.Items.Select(item => item.Source)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<string> Categories => _catalog.Items.SelectMany(item => item.Categories)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<string> Licenses => _catalog.Items.Select(item => item.LicenseSpdx)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<AssetCatalogItem> Search(AssetCatalogQuery? query = null)
    {
        query ??= new AssetCatalogQuery();
        if (query.MaximumResults is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Maximum results must be between 1 and 10000.");
        }

        var normalizedText = Normalize(query.Text);
        return _catalog.Items
            .Where(item => string.IsNullOrEmpty(normalizedText) || MatchesText(item, normalizedText))
            .Where(item => Matches(query.Source, item.Source))
            .Where(item => string.IsNullOrWhiteSpace(query.Category) ||
                item.Categories.Contains(query.Category.Trim(), StringComparer.OrdinalIgnoreCase))
            .Where(item => Matches(query.LicenseSpdx, item.LicenseSpdx))
            .Where(item => query.Availability is null || item.Availability == query.Availability)
            .Where(item => query.Installed is null || item.Installed == query.Installed)
            .Take(query.MaximumResults)
            .ToArray();
    }

    private static bool MatchesText(AssetCatalogItem item, string query)
    {
        var haystack = Normalize(string.Join(' ',
            item.DisplayName,
            item.ExternalId,
            item.Source,
            item.SourceAssetId,
            string.Join(' ', item.Tags),
            string.Join(' ', item.Categories),
            item.LicenseSpdx,
            item.CatalogStatus));
        return haystack.Contains(query, StringComparison.Ordinal);
    }

    private static bool Matches(string? expected, string actual) => string.IsNullOrWhiteSpace(expected) ||
        string.Equals(expected.Trim(), actual, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
