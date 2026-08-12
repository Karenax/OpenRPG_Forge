using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;

namespace Forge.Application.Navigation;

public readonly record struct WaypointAuthoringHandle(AreaId AreaId, Guid ProfileId, int Generation);
public readonly record struct WaypointRef(AreaId AreaId, Guid WaypointId, int Generation);
public readonly record struct PatrolRouteRef(AreaId AreaId, Guid RouteId, int Generation);
public readonly record struct LocatorRef(AreaId AreaId, Guid LocatorId, int Generation);
public readonly record struct AnchorCollectionRef(AreaId AreaId, Guid CollectionId, int Generation);

public sealed record WaypointScriptSnapshot(Guid Id, string Name, float X, float Y, float Z,
    string Icon, string Color, string Layer, double ScreenSize, string? MapText,
    string? VisibilityPolicy, string? VisibilityScope, bool RequiredAtRuntime);

public sealed record PatrolRouteScriptSnapshot(Guid Id, string Traversal, int PointCount,
    int EdgeCount, string ScriptHandler, bool RequiredAtRuntime);

public sealed record LocatorScriptSnapshot(Guid Id, string Name, string Purpose, float X, float Y,
    float Z, IReadOnlyList<string> Tags, string? ArrivalFacing, string? AssetId,
    bool RequiredAtRuntime);

public sealed record AnchorCollectionScriptSnapshot(Guid Id, string Name, string Query,
    string? Purpose, string Selection, int MemberCount, int Seed, bool RequiredAtRuntime);

public sealed class WaypointAuthoringScriptService
{
    private readonly AreaId _areaId;
    private readonly WaypointAuthoringProfile _profile;
    private const int Generation = 1;

    public WaypointAuthoringScriptService(ProjectDefinition project, AreaId areaId)
    {
        ArgumentNullException.ThrowIfNull(project);
        _areaId = areaId;
        _profile = project.Areas.Single(item => item.Id == areaId).Navigation.Waypoints;
        Handle = new(areaId, _profile.Id, Generation);
    }

    public WaypointAuthoringHandle Handle { get; }

    public IReadOnlyList<WaypointScriptSnapshot> Query(WaypointAuthoringHandle handle,
        string? search = null, int limit = 100)
    {
        Validate(handle);
        if (limit is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(limit));
        return _profile.Waypoints.Where(item => string.IsNullOrWhiteSpace(search) ||
                item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Marker?.Text.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true ||
                item.Appearance.Layer.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Take(limit).Select(item => new WaypointScriptSnapshot(item.Id, item.Name,
                item.Position.X, item.Position.Y, item.Position.Z, item.Appearance.Icon,
                item.Appearance.Color, item.Appearance.Layer, item.Appearance.ScreenSize,
                item.Marker?.Text, item.Marker?.VisibilityPolicy.ToString(),
                item.Marker?.VisibilityScope.ToString(), item.RequiredAtRuntime)).ToArray();
    }

    public WaypointRef ResolveWaypoint(WaypointAuthoringHandle handle, Guid waypointId)
    {
        Validate(handle);
        if (_profile.Waypoints.All(item => item.Id != waypointId))
            throw new InvalidOperationException("WAYPOINT_HANDLE_TARGET_MISSING");
        return new(_areaId, waypointId, Generation);
    }

    public IReadOnlyList<PatrolRouteScriptSnapshot> QueryRoutes(WaypointAuthoringHandle handle)
    {
        Validate(handle);
        return _profile.PatrolRoutes.Select(item => new PatrolRouteScriptSnapshot(item.Id,
            item.Traversal.ToString(), item.Points.Count, item.Edges.Count, item.ScriptHandler,
            item.RequiredAtRuntime)).ToArray();
    }

    public PatrolRouteRef ResolveRoute(WaypointAuthoringHandle handle, Guid routeId)
    {
        Validate(handle);
        if (_profile.PatrolRoutes.All(item => item.Id != routeId))
            throw new InvalidOperationException("PATROL_ROUTE_HANDLE_TARGET_MISSING");
        return new(_areaId, routeId, Generation);
    }

    public IReadOnlyList<LocatorScriptSnapshot> QueryLocators(WaypointAuthoringHandle handle,
        string? search = null, LocatorPurpose? purpose = null, int limit = 100)
    {
        Validate(handle);
        if (limit is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(limit));
        return _profile.Locators.Where(item =>
                (purpose is null || item.Purpose == purpose) &&
                (string.IsNullOrWhiteSpace(search) ||
                 item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                 item.Tags.Any(tag => tag.Contains(search, StringComparison.OrdinalIgnoreCase))))
            .Take(limit).Select(item => new LocatorScriptSnapshot(item.Id, item.Name,
                item.Purpose.ToString(), item.Position.X, item.Position.Y, item.Position.Z,
                item.Tags.Order(StringComparer.Ordinal).ToArray(), item.Arrival?.Facing.ToString(),
                item.AssetId, item.RequiredAtRuntime)).ToArray();
    }

    public LocatorRef ResolveLocator(WaypointAuthoringHandle handle, Guid locatorId)
    {
        Validate(handle);
        if (_profile.Locators.All(item => item.Id != locatorId))
            throw new InvalidOperationException("LOCATOR_HANDLE_TARGET_MISSING");
        return new(_areaId, locatorId, Generation);
    }

    public IReadOnlyList<AnchorCollectionScriptSnapshot> QueryCollections(WaypointAuthoringHandle handle)
    {
        Validate(handle);
        return _profile.Collections.Select(item => new AnchorCollectionScriptSnapshot(item.Id,
            item.Name, item.Query, item.Purpose?.ToString(), item.Selection.ToString(),
            item.LocatorIds.Count, item.Seed, item.RequiredAtRuntime)).ToArray();
    }

    public AnchorCollectionRef ResolveCollection(WaypointAuthoringHandle handle, Guid collectionId)
    {
        Validate(handle);
        if (_profile.Collections.All(item => item.Id != collectionId))
            throw new InvalidOperationException("ANCHOR_COLLECTION_HANDLE_TARGET_MISSING");
        return new(_areaId, collectionId, Generation);
    }

    private void Validate(WaypointAuthoringHandle handle)
    {
        if (handle.AreaId != _areaId || handle.ProfileId != _profile.Id || handle.Generation != Generation)
            throw new InvalidOperationException("WAYPOINT_HANDLE_INVALID");
    }
}
