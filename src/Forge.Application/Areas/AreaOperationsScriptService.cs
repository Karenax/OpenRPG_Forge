using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Areas;

public sealed record AreaNavigationScriptSnapshot(
    AreaId AreaId,
    Guid NavigationId,
    int SchemaVersion,
    string Provenance,
    float CellSize,
    int AgentProfiles,
    int OffMeshLinks,
    int WalkableCells,
    int SurfaceEdges);

public sealed record AreaPortalScriptSnapshot(
    PortalId PortalId,
    AreaId SourceAreaId,
    AreaId DestinationAreaId,
    string DisplayName,
    bool Bidirectional,
    bool Preload,
    string Orientation,
    string Velocity);

public static class AreaOperationsScriptService
{
    public static AreaNavigationScriptSnapshot ReadNavigation(ProjectDefinition project, AreaId areaId)
    {
        var area = project.Areas.Single(item => item.Id == areaId);
        var agent = area.Navigation.AgentProfiles.First(item => !item.CanFly);
        var surface = NavigationSurfaceBuilder.Build(project, area, agent);
        return new(area.Id, area.Navigation.Id.Value, area.Navigation.SchemaVersion, area.Navigation.Provenance,
            area.Navigation.CellSize, area.Navigation.AgentProfiles.Count, area.Navigation.OffMeshLinks.Count,
            surface.WalkableCells.Count, surface.OffMeshEdges.Count);
    }

    public static IReadOnlyList<AreaPortalScriptSnapshot> QueryPortals(ProjectDefinition project,
        AreaId? areaId = null) => project.Areas.Where(area => areaId is null || area.Id == areaId)
        .SelectMany(area => area.Portals.Select(portal => new AreaPortalScriptSnapshot(portal.Id, area.Id,
            portal.DestinationAreaId, portal.DisplayName, portal.Bidirectional, portal.PreloadDestination,
            portal.Orientation.ToString(), portal.Velocity.ToString()))).ToArray();

    public static PortalLinkTravelResolution PreviewPortal(ProjectDefinition project, AreaId areaId,
        PortalId portalId, Vector3Value incomingVelocity, float incomingYawDegrees) =>
        PortalLinkRuntimeService.Resolve(project, areaId, portalId, incomingVelocity, incomingYawDegrees);

    public static AreaReadinessReport Validate(ProjectDefinition project, AreaId areaId) =>
        AreaReadinessService.Run(project, areaId);
}
