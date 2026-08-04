using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed record AssetPackageManifestState
{
    public IReadOnlyList<AssetPackageManifest> Manifests { get; }

    public AssetPackageManifestRevision Revision { get; }

    public AssetPackageManifestState(
        IReadOnlyList<AssetPackageManifest>? manifests = null,
        AssetPackageManifestRevision revision = default)
    {
        var materialized = (manifests ?? []).ToArray();
        if (materialized.Select(manifest => manifest.Id).Distinct().Count() != materialized.Length ||
            materialized.Select(manifest => manifest.AssetId).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException(
                "Asset package manifests must have unique manifest and asset IDs.",
                nameof(manifests));
        }

        Manifests = materialized
            .OrderBy(manifest => manifest.AssetId.ToString(), StringComparer.Ordinal)
            .ToArray();
        Revision = revision;
    }

    public AssetPackageManifest? Find(AssetCatalogItemId assetId) =>
        Manifests.FirstOrDefault(manifest => manifest.AssetId == assetId);
}
