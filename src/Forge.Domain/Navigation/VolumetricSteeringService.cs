using Forge.Domain.Spatial;

namespace Forge.Domain.Navigation;

public sealed record VolumetricAgentState(Guid AgentId, Vector3Value Position, Vector3Value Velocity,
    float Radius, float MaximumSpeed);

public sealed record VolumetricSteeringResult(Guid AgentId, Vector3Value DesiredVelocity,
    Vector3Value NextPosition, int AvoidedAgents, string DiagnosticCode);

public static class VolumetricSteeringService
{
    public static VolumetricSteeringResult Step(VolumetricNavigationGraph graph,
        VolumetricAgentState agent, Vector3Value waypoint, IReadOnlyList<VolumetricAgentState> crowd,
        float deltaSeconds, float avoidanceRadius)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(crowd);
        if (agent.AgentId == Guid.Empty || agent.Radius <= 0 || agent.MaximumSpeed <= 0 ||
            !float.IsFinite(deltaSeconds) || deltaSeconds is <= 0 or > 1 ||
            !float.IsFinite(avoidanceRadius) || avoidanceRadius < agent.Radius)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        var desired = Normalize(Subtract(waypoint, agent.Position));
        var separation = Vector3Value.Zero;
        var avoided = 0;
        foreach (var other in crowd.Where(item => item.AgentId != agent.AgentId).OrderBy(item => item.AgentId))
        {
            var difference = Subtract(agent.Position, other.Position);
            var distance = Length(difference);
            var threshold = avoidanceRadius + agent.Radius + other.Radius;
            if (distance <= .0001f || distance >= threshold) continue;
            var weight = 1 - (distance / threshold);
            separation = Add(separation, Scale(Normalize(difference), weight));
            avoided++;
        }

        var velocity = Scale(Normalize(Add(desired, separation)), agent.MaximumSpeed);
        var next = Add(agent.Position, Scale(velocity, deltaSeconds));
        if (!graph.IsPassable(graph.CellAt(next)))
        {
            velocity = Vector3Value.Zero;
            next = agent.Position;
            return new(agent.AgentId, velocity, next, avoided, "area.volumetric.steering_blocked");
        }

        return new(agent.AgentId, velocity, next, avoided,
            avoided > 0 ? "area.volumetric.crowd_avoided" : "area.volumetric.steering_ready");
    }

    private static Vector3Value Add(Vector3Value a, Vector3Value b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    private static Vector3Value Subtract(Vector3Value a, Vector3Value b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static Vector3Value Scale(Vector3Value value, float scale) => new(value.X * scale, value.Y * scale, value.Z * scale);
    private static float Length(Vector3Value value) => MathF.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));
    private static Vector3Value Normalize(Vector3Value value)
    {
        var length = Length(value);
        return length <= .0001f ? Vector3Value.Zero : Scale(value, 1 / length);
    }
}
