namespace Forge.Domain.Assets;

public sealed record AssetVaultInstallationState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }

    public IReadOnlyList<AssetVaultInstalledPackage> Packages { get; }

    public AssetVaultInstallationState(
        IReadOnlyList<AssetVaultInstalledPackage>? packages = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Asset Vault installation schema '{schemaVersion}' is not supported.");
        }

        var ordered = (packages ?? [])
            .OrderBy(package => package.PackageId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Select(package => package.PackageId)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != ordered.Length)
        {
            throw new ArgumentException("Installed Asset Vault package IDs must be unique.", nameof(packages));
        }

        SchemaVersion = schemaVersion;
        Packages = ordered;
    }

    public IReadOnlyList<string> FindPackageIds(AssetCatalogItemId assetId)
    {
        ArgumentNullException.ThrowIfNull(assetId);
        return Packages
            .Where(package => package.Assets.Any(asset => asset.AssetId == assetId))
            .Select(package => package.PackageId)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public AssetVaultInstallationState WithPackage(AssetVaultInstalledPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return new AssetVaultInstallationState(
            Packages.Where(candidate => !string.Equals(
                    candidate.PackageId,
                    package.PackageId,
                    StringComparison.OrdinalIgnoreCase))
                .Append(package)
                .ToArray());
    }

    public AssetVaultInstallationState WithoutPackage(string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        return new AssetVaultInstallationState(Packages.Where(candidate => !string.Equals(
                candidate.PackageId,
                packageId.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToArray());
    }

    public IReadOnlyDictionary<string, int> BlobReferenceCounts => Packages
        .SelectMany(package => package.Assets)
        .SelectMany(asset => asset.Files)
        .GroupBy(file => file.ContentSha256, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
}
