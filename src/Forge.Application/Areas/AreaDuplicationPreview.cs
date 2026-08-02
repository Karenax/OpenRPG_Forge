using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record AreaDuplicationPreview(
    AreaId SourceAreaId,
    string SourceDisplayName,
    AreaCloneMode Mode,
    ExternalPortalPolicy ExternalPortalPolicy,
    int EntityCount,
    int InternalPortalCount,
    int ExternalPortalCount)
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
                0);
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
                : 0);
    }
}
