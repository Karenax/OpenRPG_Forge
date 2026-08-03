using Forge.Domain.Navigation;

namespace Forge.Application.Navigation;

public static class WaypointRouteService
{
    public static WaypointProfile Validate(WaypointProfile waypoint)
    {
        ValidateIdentity(waypoint.Id, waypoint.SchemaVersion, waypoint.Provenance);
        if (string.IsNullOrWhiteSpace(waypoint.Name) || string.IsNullOrWhiteSpace(waypoint.Appearance.Icon) ||
            waypoint.Appearance.ScreenSize <= 0 || waypoint.Marker is { Precision: < 0 })
            throw new InvalidOperationException("WAYPOINT_PROFILE_INVALID");
        return waypoint;
    }

    public static PatrolRoute Validate(PatrolRoute route)
    {
        ValidateIdentity(route.Id, route.SchemaVersion, route.Provenance);
        if (route.Points.Count == 0 || route.Points.Select(point => point.Id).Distinct().Count() != route.Points.Count ||
            route.Points.Any(point => point.Id == Guid.Empty || point.WaitSeconds < 0 || point.Speed <= 0 || point.Weight < 0))
            throw new InvalidOperationException("WAYPOINT_ROUTE_INVALID");
        return route;
    }

    public static MapMarker Reveal(MapMarker marker, MarkerVisibilityScope scope, string eventName)
    {
        if (marker.VisibilityScope != scope || !string.Equals(marker.RevealEvent, eventName, StringComparison.Ordinal)) return marker;
        return marker with { Revealed = true };
    }

    public static PatrolCursor Next(PatrolRoute route, PatrolCursor cursor, int deterministicRoll = 0)
    {
        Validate(route);
        var count = route.Points.Count;
        if (cursor.Complete || count == 1) return cursor with { Complete = route.Traversal == PatrolTraversalPolicy.Once };
        return route.Traversal switch
        {
            PatrolTraversalPolicy.Loop => new((cursor.Index + 1) % count, 1, false),
            PatrolTraversalPolicy.Once => cursor.Index == count - 1 ? cursor with { Complete = true } : new(cursor.Index + 1, 1, false),
            PatrolTraversalPolicy.PingPong => PingPong(cursor, count),
            PatrolTraversalPolicy.Random => new(Math.Abs(deterministicRoll) % count, 1, false),
            PatrolTraversalPolicy.Weighted => new(Weighted(route.Points, deterministicRoll), 1, false),
            PatrolTraversalPolicy.Scripted => cursor,
            _ => cursor
        };
    }

    private static PatrolCursor PingPong(PatrolCursor cursor, int count)
    {
        var direction = cursor.Direction == 0 ? 1 : cursor.Direction;
        var next = cursor.Index + direction;
        if (next >= count) return new(count - 2, -1, false);
        if (next < 0) return new(1, 1, false);
        return new(next, direction, false);
    }

    private static int Weighted(IReadOnlyList<PatrolPoint> points, int roll)
    {
        var total = points.Sum(point => point.Weight);
        if (total <= 0) return 0;
        var target = Math.Abs(roll % 10_000) / 10_000d * total;
        var running = 0d;
        for (var index = 0; index < points.Count; index++) { running += points[index].Weight; if (target < running) return index; }
        return points.Count - 1;
    }

    private static void ValidateIdentity(Guid id, int schemaVersion, string provenance)
    {
        if (id == Guid.Empty) throw new InvalidOperationException("WAYPOINT_UUID_REQUIRED");
        if (schemaVersion != 1) throw new InvalidOperationException("WAYPOINT_SCHEMA_UNSUPPORTED");
        if (string.IsNullOrWhiteSpace(provenance)) throw new InvalidOperationException("WAYPOINT_PROVENANCE_REQUIRED");
    }
}
