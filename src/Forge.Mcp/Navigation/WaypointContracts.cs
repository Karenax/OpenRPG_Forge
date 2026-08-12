using Forge.Application.Commands;
using Forge.Domain.Navigation;

namespace Forge.Mcp.Navigation;

public sealed record WaypointAuthoringResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    IReadOnlyList<WaypointResourceItem> Waypoints,
    IReadOnlyList<PatrolRouteResourceItem> PatrolRoutes,
    IReadOnlyList<LocatorResourceItem> Locators,
    IReadOnlyList<AnchorCollectionResourceItem> Collections,
    IReadOnlyList<RouteVisualizationResourceItem> RouteVisualizations);

public sealed record WaypointResourceItem(
    string AreaId,
    string ProfileId,
    string WaypointId,
    string Name,
    float X,
    float Y,
    float Z,
    string Icon,
    string Color,
    string Layer,
    bool Visible,
    double ScreenSize,
    string? MapText,
    string? MapIcon,
    string? VisibilityPolicy,
    string? VisibilityScope,
    bool Tracked,
    bool Revealed,
    bool RequiredAtRuntime,
    string? ArrivalFacing,
    double? ArrivalWaitSeconds,
    string? ArrivalAnimation,
    string? ArrivalInteraction,
    string? ArrivalCallback);

public sealed record PatrolRouteResourceItem(
    string AreaId,
    string ProfileId,
    string RouteId,
    string Traversal,
    int PointCount,
    int EdgeCount,
    string ScriptHandler,
    bool RequiredAtRuntime,
    IReadOnlyList<RouteSegmentResourceItem> Segments);

public sealed record RouteSegmentResourceItem(
    string FromId,
    string ToId,
    int Direction,
    double Distance,
    double Cost,
    double TravelSeconds,
    string? Action,
    bool BrokenLink,
    bool NavProjected);

public sealed record LocatorResourceItem(
    string AreaId,
    string ProfileId,
    string LocatorId,
    string Name,
    string Purpose,
    float X,
    float Y,
    float Z,
    IReadOnlyList<string> Tags,
    string? AssetId,
    string? BlueprintId,
    string? ArrivalFacing,
    bool RequiredAtRuntime);

public sealed record AnchorCollectionResourceItem(
    string AreaId,
    string ProfileId,
    string CollectionId,
    string Name,
    string Query,
    string? Purpose,
    IReadOnlyList<string> RequiredTags,
    string Selection,
    int MemberCount,
    int Seed,
    bool RequiredAtRuntime);

public sealed record RouteVisualizationResourceItem(
    string AreaId,
    string ProfileId,
    string VisualizationId,
    bool ShowSegments,
    bool ShowDirection,
    bool ShowCosts,
    bool ShowActions,
    bool ShowBrokenLinks,
    bool ShowNavigationProjection,
    bool ShowTiming,
    double CostPerMeter,
    bool RequiredAtRuntime);

public sealed record WaypointAuthoringPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    WaypointAuthoringProfile Profile);

public sealed record WaypointAuthoringPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
