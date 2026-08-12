using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;

namespace Forge.Application.Navigation;

public sealed record WaypointRuntimeDiagnostic(string Code, string Message, string Path);

public sealed record WaypointRuntimeState(
    IReadOnlySet<Guid> RevealedWaypointIds,
    IReadOnlyDictionary<Guid, PatrolCursor> PatrolCursors)
{
    public static WaypointRuntimeState Empty { get; } = new(new HashSet<Guid>(),
        new Dictionary<Guid, PatrolCursor>());
}

public sealed record WaypointMapPresentation(
    Guid WaypointId,
    string Text,
    string Icon,
    string Category,
    int Floor,
    double Precision,
    MarkerVisibilityScope Scope,
    bool Tracked,
    Vector3Value Position,
    string AccessibleText);

public sealed record WaypointRevealResolution(
    WaypointRuntimeState State,
    IReadOnlyList<Guid> RevealedWaypointIds,
    IReadOnlyList<WaypointRuntimeDiagnostic> Diagnostics);

public sealed record WaypointPatrolStep(
    Guid RouteId,
    Guid FromPointId,
    Guid ToPointId,
    Vector3Value Position,
    double WaitSeconds,
    double FacingDegrees,
    double Speed,
    string? Action,
    string? BranchCondition,
    PatrolCursor Cursor);

public sealed record WaypointPatrolResolution(
    WaypointRuntimeState State,
    WaypointPatrolStep? Step,
    IReadOnlyList<WaypointRuntimeDiagnostic> Diagnostics);

public sealed record WaypointArrivalRuntimeResolution(
    WaypointArrivalResolution? Arrival,
    IReadOnlyList<WaypointRuntimeDiagnostic> Diagnostics);

public sealed record WaypointLocatorPresentation(
    Guid Id,
    string Name,
    LocatorPurpose Purpose,
    Vector3Value Position,
    IReadOnlyList<string> Tags,
    string? AssetId,
    bool RequiredAtRuntime);

public static class WaypointRuntimeService
{
    public const string AuthorityDeniedCode = "runtime.waypoint.authority_denied";
    public const string RequiredMissingCode = "runtime.waypoint.required_missing";
    public const string RouteMissingCode = "runtime.waypoint.route_missing";
    public const string ScriptIndexInvalidCode = "runtime.waypoint.script_index_invalid";
    public const string ArrivalMissingCode = "runtime.waypoint.arrival_missing";
    public const string LocatorCollectionMissingCode = "runtime.waypoint.collection_missing";

    public static IReadOnlyList<WaypointRuntimeDiagnostic> ValidateRuntime(WaypointAuthoringProfile profile,
        string path = "navigation/waypoints")
    {
        var diagnostics = new List<WaypointRuntimeDiagnostic>();
        if (profile.RequiredAtRuntime && profile.Waypoints.Count == 0)
            diagnostics.Add(new(RequiredMissingCode, "Required waypoint authoring content is absent.", path));
        if (profile.Collections.Any(collection => collection.RequiredAtRuntime &&
                WaypointRouteService.ResolveCollection(collection, profile.Locators).Count == 0))
            diagnostics.Add(new(RequiredMissingCode,
                "A required locator collection resolves to no runtime locator.", $"{path}/collections"));
        if (profile.RouteVisualization.RequiredAtRuntime && profile.PatrolRoutes.Count == 0)
            diagnostics.Add(new(RequiredMissingCode,
                "Required route visualization has no patrol route to display.",
                $"{path}/routeVisualization"));
        return diagnostics;
    }

    public static WaypointArrivalRuntimeResolution Arrive(WaypointAuthoringProfile profile, Guid targetId,
        bool authorityGranted = true)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var arrival = profile.Waypoints.FirstOrDefault(item => item.Id == targetId)?.Arrival ??
                      profile.Locators.FirstOrDefault(item => item.Id == targetId)?.Arrival;
        if (arrival is null)
            return new(null, [new(ArrivalMissingCode, "Waypoint or locator has no arrival behavior.",
                $"navigation/waypoints/arrivals/{targetId:D}")]);
        if (!authorityGranted)
            return new(WaypointRouteService.ResolveArrival(arrival, false),
                [new(AuthorityDeniedCode, "Only the authoritative runtime may execute arrival actions.",
                    $"navigation/waypoints/arrivals/{targetId:D}")]);
        return new(WaypointRouteService.ResolveArrival(arrival), []);
    }

    public static IReadOnlyList<WaypointLocatorPresentation> ResolveCollection(
        WaypointAuthoringProfile profile, Guid collectionId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var collection = profile.Collections.SingleOrDefault(item => item.Id == collectionId);
        if (collection is null) return [];
        return WaypointRouteService.ResolveCollection(collection, profile.Locators)
            .Select(item => new WaypointLocatorPresentation(item.Id, item.Name, item.Purpose,
                item.Position, item.Tags, item.AssetId, item.RequiredAtRuntime)).ToArray();
    }

    public static WaypointRevealResolution Reveal(WaypointAuthoringProfile profile,
        WaypointRuntimeState state, MarkerVisibilityScope scope, string eventName, bool authorityGranted = true)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(state);
        if (!authorityGranted)
            return new(state, [], [new(AuthorityDeniedCode,
                "Only the authoritative runtime may reveal persistent map markers.",
                $"navigation/waypoints/{profile.Id:D}")]);
        var revealed = state.RevealedWaypointIds.ToHashSet();
        var changed = profile.Waypoints.Where(waypoint => waypoint.Marker is { } marker &&
                marker.VisibilityPolicy == MarkerVisibilityPolicy.HiddenUntilRevealed &&
                marker.VisibilityScope == scope &&
                string.Equals(marker.RevealEvent, eventName, StringComparison.Ordinal))
            .Select(item => item.Id).Where(revealed.Add).Order().ToArray();
        return new(state with { RevealedWaypointIds = revealed }, changed, []);
    }

    public static IReadOnlyList<WaypointMapPresentation> VisibleMarkers(WaypointAuthoringProfile profile,
        WaypointRuntimeState state, MarkerVisibilityScope scope) => profile.Waypoints
        .Where(waypoint => waypoint.Marker is { } marker && marker.VisibilityScope == scope &&
            (marker.VisibilityPolicy == MarkerVisibilityPolicy.Always ||
             marker.VisibilityPolicy == MarkerVisibilityPolicy.WhileTracked && marker.Tracked ||
             marker.VisibilityPolicy == MarkerVisibilityPolicy.HiddenUntilRevealed &&
             (marker.Revealed || state.RevealedWaypointIds.Contains(waypoint.Id))))
        .OrderBy(waypoint => waypoint.Marker!.Category, StringComparer.Ordinal)
        .ThenBy(waypoint => waypoint.Name, StringComparer.Ordinal).ThenBy(waypoint => waypoint.Id)
        .Select(waypoint =>
        {
            var marker = waypoint.Marker!;
            return new WaypointMapPresentation(waypoint.Id, marker.Text, marker.Icon, marker.Category,
                marker.Floor, marker.Precision, marker.VisibilityScope, marker.Tracked, waypoint.Position,
                $"{marker.Text}, catégorie {marker.Category}, étage {marker.Floor}, précision {marker.Precision:0.#} mètres");
        }).ToArray();

    public static WaypointPatrolResolution Advance(WaypointAuthoringProfile profile,
        WaypointRuntimeState state, Guid routeId, int deterministicRoll = 0, int? scriptedIndex = null,
        bool authorityGranted = true)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(state);
        if (!authorityGranted)
            return new(state, null, [new(AuthorityDeniedCode,
                "Only the authoritative runtime may advance a patrol route.",
                $"navigation/waypoints/patrolRoutes/{routeId:D}")]);
        var route = profile.PatrolRoutes.SingleOrDefault(item => item.Id == routeId);
        if (route is null)
            return new(state, null, [new(RouteMissingCode, "Patrol route does not exist.",
                $"navigation/waypoints/patrolRoutes/{routeId:D}")]);
        var cursor = state.PatrolCursors.GetValueOrDefault(route.Id, new PatrolCursor(0, 1, false));
        PatrolCursor next;
        if (route.Traversal == PatrolTraversalPolicy.Scripted)
        {
            if (scriptedIndex is null || scriptedIndex < 0 || scriptedIndex >= route.Points.Count)
                return new(state, null, [new(ScriptIndexInvalidCode,
                    "Scripted patrol traversal requires an in-range point index.",
                    $"navigation/waypoints/patrolRoutes/{route.Id:D}/scriptedIndex")]);
            next = new(scriptedIndex.Value, 1, false);
        }
        else
        {
            next = WaypointRouteService.Next(route, cursor, deterministicRoll);
        }
        var from = route.Points[Math.Clamp(cursor.Index, 0, route.Points.Count - 1)];
        var to = route.Points[Math.Clamp(next.Index, 0, route.Points.Count - 1)];
        var cursors = state.PatrolCursors.ToDictionary(item => item.Key, item => item.Value);
        cursors[route.Id] = next;
        var step = new WaypointPatrolStep(route.Id, from.Id, to.Id, to.Position, to.WaitSeconds,
            to.FacingDegrees, to.Speed, to.Action, to.BranchCondition, next);
        return new(state with { PatrolCursors = cursors }, step, []);
    }
}
