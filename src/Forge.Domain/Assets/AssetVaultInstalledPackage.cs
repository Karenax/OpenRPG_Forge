using System.Text.RegularExpressions;

namespace Forge.Domain.Assets;

public sealed partial record AssetVaultInstalledPackage
{
    public string PackageId { get; }

    public string Version { get; }

    public IReadOnlyList<AssetVaultInstalledAsset> Assets { get; }

    public AssetVaultInstalledPackage(
        string packageId,
        string version,
        IReadOnlyList<AssetVaultInstalledAsset> assets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(assets);
        var normalizedId = NormalizeId(packageId);

        var ordered = assets
            .OrderBy(asset => asset.ExternalId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0 ||
            ordered.Select(asset => asset.AssetId).Distinct().Count() != ordered.Length ||
            ordered.Select(asset => asset.ExternalId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != ordered.Length)
        {
            throw new ArgumentException("An asset package requires unique assets.", nameof(assets));
        }

        PackageId = normalizedId;
        Version = version.Trim();
        Assets = ordered;
    }

    public static string NormalizeId(string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        var normalizedId = packageId.Trim().ToLowerInvariant();
        if (!PackageIdPattern().IsMatch(normalizedId))
        {
            throw new ArgumentException(
                "Asset package IDs may contain lowercase letters, numbers, dots, underscores and hyphens.",
                nameof(packageId));
        }

        return normalizedId;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageIdPattern();
}
