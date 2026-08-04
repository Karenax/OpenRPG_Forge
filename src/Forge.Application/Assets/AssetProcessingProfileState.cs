using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed record AssetProcessingProfileState
{
    public IReadOnlyList<AssetProcessingProfile> Profiles { get; }

    public AssetProcessingProfileRevision Revision { get; }

    public AssetProcessingProfileState(
        IReadOnlyList<AssetProcessingProfile>? profiles = null,
        AssetProcessingProfileRevision revision = default)
    {
        var materialized = (profiles ?? []).ToArray();
        if (materialized
            .GroupBy(profile => (profile.AssetId, profile.Kind))
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Asset processing profiles must be unique by asset and processing kind.",
                nameof(profiles));
        }

        Profiles = materialized
            .OrderBy(profile => profile.AssetId.ToString(), StringComparer.Ordinal)
            .ThenBy(profile => profile.Kind)
            .ToArray();
        Revision = revision;
    }

    public AssetProcessingProfile? Find(AssetCatalogItemId assetId, AssetProcessingKind kind) =>
        Profiles.FirstOrDefault(profile => profile.AssetId == assetId && profile.Kind == kind);
}
