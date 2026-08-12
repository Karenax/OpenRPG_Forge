using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record AreaDuplicationPreview(
    AreaId SourceAreaId,
    string SourceDisplayName,
    AreaCloneMode Mode,
    ExternalPortalPolicy ExternalPortalPolicy,
    int EntityCount,
    int InternalPortalCount,
    int ExternalPortalCount,
    bool TerrainCopied,
    bool NavigationCopied,
    bool EnvironmentCopied,
    int EnvironmentChildUuidCount,
    int SharedAssetReferences)
{
    public static AreaDuplicationPreview Create(
        AreaDefinition source,
        AreaCloneMode mode,
        ExternalPortalPolicy externalPortalPolicy)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (mode == AreaCloneMode.Template)
        {
            return new AreaDuplicationPreview(
                source.Id,
                source.DisplayName,
                mode,
                externalPortalPolicy,
                0,
                0,
                0,
                true,
                true,
                true,
                EnvironmentChildCount(source),
                SharedReferences(source));
        }

        return new AreaDuplicationPreview(
            source.Id,
            source.DisplayName,
            mode,
            externalPortalPolicy,
            source.Entities.Count,
            source.Portals.Count(portal => portal.DestinationAreaId == source.Id),
            externalPortalPolicy == ExternalPortalPolicy.Preserve
                ? source.Portals.Count(portal => portal.DestinationAreaId != source.Id)
                : 0,
            true,
            true,
            true,
            EnvironmentChildCount(source),
            SharedReferences(source));
    }

    private static int EnvironmentChildCount(AreaDefinition source) =>
        1 + source.Environment.Authoring.Lighting.Events.Count +
        source.Environment.Authoring.Lighting.Probes.Count +
        source.Environment.Authoring.Lighting.VolumeOverrides.Count +
        source.Environment.Authoring.Fog.LocalVolumes.Count;

    private static int SharedReferences(AreaDefinition source) =>
        source.Entities.Select(item => item.BlueprintId).Distinct().Count() +
        source.Environment.Authoring.Lighting.Probes.Count(item =>
            !string.IsNullOrWhiteSpace(item.AssetId) || item.BlueprintId is not null) +
        (source.Environment.Authoring.Fog.Noise is null ? 0 : 1);
}
