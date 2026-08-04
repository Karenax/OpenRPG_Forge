using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed record AssetExternalEditState
{
    public IReadOnlyList<AssetExternalEditConfiguration> Configurations { get; }

    public AssetExternalEditRevision Revision { get; }

    public AssetExternalEditState(
        IReadOnlyList<AssetExternalEditConfiguration>? configurations = null,
        AssetExternalEditRevision revision = default)
    {
        var materialized = (configurations ?? []).ToArray();
        if (materialized.GroupBy(configuration => configuration.AssetId).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("External edit configurations must be unique by asset.", nameof(configurations));
        }

        Configurations = materialized
            .OrderBy(configuration => configuration.AssetId.ToString(), StringComparer.Ordinal)
            .ToArray();
        Revision = revision;
    }

    public AssetExternalEditConfiguration? Find(AssetCatalogItemId assetId) =>
        Configurations.FirstOrDefault(configuration => configuration.AssetId == assetId);
}
