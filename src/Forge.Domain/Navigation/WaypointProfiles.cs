using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Domain.Navigation;

public enum MarkerVisibilityScope { Player, Party, World }
public enum MarkerVisibilityPolicy { Always, HiddenUntilRevealed, WhileTracked }
public enum PatrolTraversalPolicy { Loop, PingPong, Once, Random, Weighted, Scripted }
public enum ArrivalFacingMode { Preserve, Direction, Target }
public enum LocatorPurpose { Audio, Vfx, Spawn, Objective, Gameplay }
public enum LocatorSelectionMode { Stable, Nearest, RandomDeterministic }

public sealed record WaypointEditorAppearance(string Icon, string Color, string Layer, bool Visible, double ScreenSize);

public sealed record MapMarker(
    string Text,
    string Icon,
    int Floor,
    string Category,
    double Precision,
    MarkerVisibilityPolicy VisibilityPolicy,
    MarkerVisibilityScope VisibilityScope,
    bool Tracked,
    bool Revealed,
    string RevealEvent,
    string RevealAnimation,
    bool Persistent);

public sealed record PatrolPoint(
    Guid Id,
    Vector3Value Position,
    double WaitSeconds,
    double FacingDegrees,
    double Speed,
    string? Action,
    string? BranchCondition,
    double Weight = 1);

public sealed record PatrolRouteEdge(
    Guid Id,
    Guid FromPointId,
    Guid ToPointId,
    int DisplayOrder,
    string? BranchCondition,
    double Weight);

public sealed record WaypointProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Name,
    Vector3Value Position,
    WaypointEditorAppearance Appearance,
    MapMarker? Marker,
    Guid? AnchorInstanceId = null,
    bool RequiredAtRuntime = false,
    string ForgeScriptApiVersion = "1.0",
    ArrivalBehavior? Arrival = null);

public sealed record PatrolRoute
{
    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public PatrolTraversalPolicy Traversal { get; }
    public IReadOnlyList<PatrolPoint> Points { get; }
    public IReadOnlyList<PatrolRouteEdge> Edges { get; }
    public string ScriptHandler { get; }
    public bool RequiredAtRuntime { get; }

    public PatrolRoute(Guid id, int schemaVersion, string provenance, PatrolTraversalPolicy traversal,
        IReadOnlyList<PatrolPoint> points, IReadOnlyList<PatrolRouteEdge>? edges = null,
        string? scriptHandler = null, bool requiredAtRuntime = false)
    {
        ArgumentNullException.ThrowIfNull(points);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance ?? string.Empty;
        Traversal = traversal;
        Points = points.ToArray();
        Edges = (edges ?? BuildOrderedEdges(id, Points)).OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Id).ToArray();
        ScriptHandler = scriptHandler?.Trim() ?? string.Empty;
        RequiredAtRuntime = requiredAtRuntime;
    }

    private static IReadOnlyList<PatrolRouteEdge> BuildOrderedEdges(Guid routeId,
        IReadOnlyList<PatrolPoint> points)
    {
        var result = new List<PatrolRouteEdge>();
        for (var index = 0; index < points.Count - 1; index++)
        {
            var from = points[index];
            var to = points[index + 1];
            result.Add(new(StableId(routeId, $"edge:{from.Id:D}:{to.Id:D}"), from.Id, to.Id,
                index, from.BranchCondition, from.Weight));
        }
        return result;
    }

    private static Guid StableId(Guid ownerId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{ownerId:D}:{discriminator}"));
        return new Guid(bytes[..16]);
    }
}

public sealed record WaypointAuthoringProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public AreaId AreaId { get; }
    public bool RequiredAtRuntime { get; }
    public IReadOnlyList<WaypointProfile> Waypoints { get; }
    public IReadOnlyList<PatrolRoute> PatrolRoutes { get; }
    public IReadOnlyList<GenericLocator> Locators { get; }
    public IReadOnlyList<AnchorCollection> Collections { get; }
    public RouteVisualizationProfile RouteVisualization { get; }

    public WaypointAuthoringProfile(Guid id, int schemaVersion, string? provenance, AreaId areaId,
        bool requiredAtRuntime, IReadOnlyList<WaypointProfile>? waypoints,
        IReadOnlyList<PatrolRoute>? patrolRoutes, IReadOnlyList<GenericLocator>? locators = null,
        IReadOnlyList<AnchorCollection>? collections = null,
        RouteVisualizationProfile? routeVisualization = null)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        Id = id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        AreaId = areaId;
        RequiredAtRuntime = requiredAtRuntime;
        Waypoints = (waypoints ?? []).OrderBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();
        PatrolRoutes = (patrolRoutes ?? []).OrderBy(item => item.Id).ToArray();
        Locators = (locators ?? []).OrderBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();
        Collections = (collections ?? []).OrderBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();
        RouteVisualization = routeVisualization ?? RouteVisualizationProfile.CreateDefault(areaId);
    }

    public static WaypointAuthoringProfile CreateDefault(AreaId areaId) => new(
        StableId(areaId, "waypoint-authoring"), CurrentSchemaVersion, "migration-default", areaId,
        false, [], [], [], [], RouteVisualizationProfile.CreateDefault(areaId));

    public static WaypointAuthoringProfile CloneForArea(WaypointAuthoringProfile source,
        AreaId destinationAreaId)
    {
        var waypointMap = source.Waypoints.Select((item, index) =>
                (item.Id, NewId: StableId(destinationAreaId, $"waypoint:{index}:{item.Name}")))
            .ToDictionary(item => item.Id, item => item.NewId);
        var waypoints = source.Waypoints.Select(item => item with
        {
            Id = waypointMap[item.Id],
            Provenance = "area-clone",
            AnchorInstanceId = null,
        }).ToArray();
        var routes = source.PatrolRoutes.Select((route, routeIndex) =>
        {
            var routeId = StableId(destinationAreaId, $"patrol-route:{routeIndex}:{route.Id:D}");
            var pointMap = route.Points.Select((point, pointIndex) =>
                    (point.Id, NewId: StableId(destinationAreaId,
                        $"patrol-point:{routeIndex}:{pointIndex}:{point.Id:D}")))
                .ToDictionary(item => item.Id, item => item.NewId);
            var points = route.Points.Select(point => point with { Id = pointMap[point.Id] }).ToArray();
            var edges = route.Edges.Select(edge => edge with
            {
                Id = StableId(destinationAreaId, $"patrol-edge:{routeIndex}:{edge.DisplayOrder}:{edge.Id:D}"),
                FromPointId = pointMap[edge.FromPointId],
                ToPointId = pointMap[edge.ToPointId],
            }).ToArray();
            return new PatrolRoute(routeId, route.SchemaVersion, "area-clone", route.Traversal,
                points, edges, route.ScriptHandler, route.RequiredAtRuntime);
        }).ToArray();
        var locatorMap = source.Locators.Select((item, index) =>
                (item.Id, NewId: StableId(destinationAreaId, $"locator:{index}:{item.Name}")))
            .ToDictionary(item => item.Id, item => item.NewId);
        var locators = source.Locators.Select(item => item with
        {
            Id = locatorMap[item.Id],
            Provenance = "area-clone",
        }).ToArray();
        var collections = source.Collections.Select((item, index) => item with
        {
            Id = StableId(destinationAreaId, $"locator-collection:{index}:{item.Name}"),
            Provenance = "area-clone",
            LocatorIds = item.LocatorIds.Where(locatorMap.ContainsKey).Select(id => locatorMap[id]).ToArray(),
        }).ToArray();
        var visualization = source.RouteVisualization with
        {
            Id = StableId(destinationAreaId, "route-visualization"),
            Provenance = "area-clone",
        };
        return new(StableId(destinationAreaId, "waypoint-authoring"), source.SchemaVersion,
            "area-clone", destinationAreaId, source.RequiredAtRuntime, waypoints, routes, locators,
            collections, visualization);
    }

    private static Guid StableId(AreaId areaId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{areaId}:{discriminator}"));
        return new Guid(bytes[..16]);
    }
}

public sealed record PatrolCursor(int Index, int Direction, bool Complete);

public sealed record ArrivalBehavior(
    ArrivalFacingMode Facing,
    double DirectionDegrees,
    string? TargetId,
    string? Animation,
    double WaitSeconds,
    string? Interaction,
    string? Callback);

public sealed record GenericLocator(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Name,
    Vector3Value Position,
    LocatorPurpose Purpose,
    IReadOnlyList<string> Tags,
    ArrivalBehavior? Arrival,
    string? AssetId = null,
    BlueprintId? BlueprintId = null,
    IReadOnlyDictionary<string, string>? ExplicitOverrides = null,
    bool RequiredAtRuntime = false);

public sealed record AnchorCollection(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Query,
    IReadOnlyList<Guid> LocatorIds,
    string Name = "Collection",
    LocatorPurpose? Purpose = null,
    IReadOnlyList<string>? RequiredTags = null,
    LocatorSelectionMode Selection = LocatorSelectionMode.Stable,
    Vector3Value? Origin = null,
    int Seed = 0,
    bool RequiredAtRuntime = false);

public sealed record RouteVisualizationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool ShowSegments,
    bool ShowDirection,
    bool ShowCosts,
    bool ShowActions,
    bool ShowBrokenLinks,
    bool ShowNavigationProjection,
    bool ShowTiming,
    double CostPerMeter,
    bool RequiredAtRuntime = false)
{
    public static RouteVisualizationProfile CreateDefault(AreaId areaId) => new(
        StableId(areaId), 1, "migration-default", true, true, true, true, true, true, true, 1);

    private static Guid StableId(AreaId areaId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{areaId}:route-visualization"));
        return new Guid(bytes[..16]);
    }
}

public sealed record RouteSegmentDiagnostic(
    Guid FromId,
    Guid ToId,
    int Direction,
    double Distance,
    double Cost,
    double TravelSeconds,
    string? Action,
    bool BrokenLink,
    bool NavProjected);
