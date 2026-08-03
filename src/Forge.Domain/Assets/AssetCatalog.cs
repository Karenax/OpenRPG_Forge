namespace Forge.Domain.Assets;

public sealed record AssetCatalog
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }

    public string CatalogDate { get; }

    public string LicensePolicy { get; }

    public string VaultRoot { get; }

    public IReadOnlyList<AssetCatalogItem> Items { get; }

    public string Provenance { get; }

    public AssetCatalog(
        string catalogDate,
        string licensePolicy,
        string vaultRoot,
        IReadOnlyList<AssetCatalogItem> items,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogDate);
        ArgumentException.ThrowIfNullOrWhiteSpace(licensePolicy);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion),
                $"Asset catalog schema '{schemaVersion}' is not supported.");
        }

        var ordered = items
            .OrderBy(item => item.Source, StringComparer.Ordinal)
            .ThenBy(item => item.DisplayName, StringComparer.Ordinal)
            .ThenBy(item => item.ExternalId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Select(item => item.Id).Distinct().Count() != ordered.Length ||
            ordered.Select(item => item.ExternalId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != ordered.Length)
        {
            throw new ArgumentException("Asset catalog IDs and external IDs must be unique.", nameof(items));
        }

        SchemaVersion = schemaVersion;
        CatalogDate = catalogDate.Trim();
        LicensePolicy = licensePolicy.Trim();
        VaultRoot = vaultRoot.Trim();
        Items = ordered;
        Provenance = provenance.Trim();
    }
}
