using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.Domain.Navigation;

public sealed record FlightRouteActorState(Guid ActorId, int SegmentIndex, float SegmentProgress,
    Vector3Value Position, Vector3Value Forward, float BankDegrees, bool Completed);

public sealed record FlightRouteStepResult(FlightRouteActorState State, float Speed,
    bool ObstacleDetected, string DiagnosticCode);

public interface IFlightObstacleProbe
{
    bool IsBlocked(Vector3Value position, float clearanceRadius);
}

public static class FlightRouteController
{
    public static FlightRouteActorState CreateInitial(Guid actorId, FlightRouteDefinition route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (actorId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(actorId));
        var forward = Normalize(Subtract(route.Nodes[1].Position, route.Nodes[0].Position));
        return new(actorId, 0, 0, route.Nodes[0].Position, forward, route.Nodes[0].BankDegrees, false);
    }

    public static FlightRouteStepResult Step(FlightRouteDefinition route, FlightRouteActorState state,
        float deltaSeconds, float clearanceRadius, IFlightObstacleProbe obstacleProbe)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(obstacleProbe);
        if (state.ActorId == Guid.Empty || !float.IsFinite(deltaSeconds) || deltaSeconds is <= 0 or > 1 ||
            !float.IsFinite(clearanceRadius) || clearanceRadius <= 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (state.Completed) return new(state, 0, false, "waypoint.flight.completed");

        var segment = Math.Clamp(state.SegmentIndex, 0, route.Nodes.Count - 2);
        var start = route.Nodes[segment];
        var end = route.Nodes[segment + 1];
        var segmentLength = Math.Max(.001f, Length(Subtract(end.Position, start.Position)));
        var speed = Lerp(start.Speed, end.Speed, state.SegmentProgress);
        var progress = state.SegmentProgress + (speed * deltaSeconds / segmentLength);
        var nextSegment = segment;
        var completed = false;
        while (progress >= 1)
        {
            progress -= 1;
            nextSegment++;
            if (nextSegment >= route.Nodes.Count - 1)
            {
                if (route.Closed) nextSegment = 0;
                else
                {
                    nextSegment = route.Nodes.Count - 2;
                    progress = 1;
                    completed = true;
                    break;
                }
            }
        }

        var nextPosition = Sample(route, nextSegment, progress);
        var blocked = obstacleProbe.IsBlocked(nextPosition, clearanceRadius);
        var code = "waypoint.flight.following";
        if (blocked && route.ObstaclePolicy == FlightObstaclePolicy.Stop)
            return new(state, 0, true, "waypoint.flight.obstacle_stopped");
        if (blocked && route.ObstaclePolicy == FlightObstaclePolicy.Avoid)
        {
            nextPosition = new(nextPosition.X, nextPosition.Y + Math.Max(1, clearanceRadius * 2), nextPosition.Z);
            code = "waypoint.flight.obstacle_avoided";
        }
        else if (blocked)
        {
            code = "waypoint.flight.obstacle_ignored";
        }

        var forward = Normalize(Subtract(nextPosition, state.Position));
        if (Length(forward) <= .0001f) forward = state.Forward;
        var targetBank = Lerp(route.Nodes[nextSegment].BankDegrees,
            route.Nodes[nextSegment + 1].BankDegrees, progress);
        var bank = MoveTowards(state.BankDegrees, targetBank, 90 * deltaSeconds);
        return new(new(state.ActorId, nextSegment, progress, nextPosition, forward, bank, completed),
            completed ? 0 : speed, blocked, completed ? "waypoint.flight.completed" : code);
    }

    private static Vector3Value Sample(FlightRouteDefinition route, int segment, float t)
    {
        var p1 = route.Nodes[segment].Position;
        var p2 = route.Nodes[segment + 1].Position;
        if (route.Curve == FlightRouteCurve.Linear) return Lerp(p1, p2, t);
        if (route.Curve == FlightRouteCurve.Bezier)
        {
            var smooth = t * t * (3 - (2 * t));
            return Lerp(p1, p2, smooth);
        }

        var p0 = route.Nodes[Math.Max(0, segment - 1)].Position;
        var p3 = route.Nodes[Math.Min(route.Nodes.Count - 1, segment + 2)].Position;
        var t2 = t * t;
        var t3 = t2 * t;
        return Scale(Add(Add(Scale(p1, 2), Scale(Subtract(p2, p0), t)),
            Add(Scale(Add(Add(Scale(p0, 2), Scale(p1, -5)), Add(Scale(p2, 4), Scale(p3, -1))), t2),
                Scale(Add(Add(Scale(p0, -1), Scale(p1, 3)), Add(Scale(p2, -3), p3)), t3))), .5f);
    }

    private static float MoveTowards(float current, float target, float maximumDelta) =>
        MathF.Abs(target - current) <= maximumDelta ? target : current + MathF.CopySign(maximumDelta, target - current);
    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
    private static Vector3Value Lerp(Vector3Value a, Vector3Value b, float t) => Add(a, Scale(Subtract(b, a), t));
    private static Vector3Value Add(Vector3Value a, Vector3Value b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    private static Vector3Value Subtract(Vector3Value a, Vector3Value b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static Vector3Value Scale(Vector3Value value, float scale) => new(value.X * scale, value.Y * scale, value.Z * scale);
    private static float Length(Vector3Value value) => MathF.Sqrt(value.X * value.X + value.Y * value.Y + value.Z * value.Z);
    private static Vector3Value Normalize(Vector3Value value) => Length(value) <= .0001f ? Vector3Value.Zero : Scale(value, 1 / Length(value));
}
