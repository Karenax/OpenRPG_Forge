using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.EngineAdapter.World;

public static class SurfaceNavigationPlanBuilder
{
    public const float MaximumSlopeDegrees = 48f;
    public const string MissingGroundAgentCode = "navigation.surface.ground_agent_missing";
    public const string EmptySurfaceCode = "navigation.surface.empty";

    public static SurfaceNavigationPlan Build(
        AreaWorldProfile world,
        AreaNavigationDefinition navigation,
        NavigationAgentProfile? requestedAgent = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(navigation);
        var terrain = TerrainBuildPlanBuilder.Build(world);
        var diagnostics = terrain.Diagnostics.ToList();
        if (!terrain.Enabled || terrain.Indices.Count == 0)
        {
            return new(false, null, [], [], [], 0, diagnostics);
        }

        var agent = requestedAgent ?? navigation.AgentProfiles
            .Where(item => !item.CanFly)
            .OrderBy(item => MathF.Abs(item.Radius - .5f))
            .ThenBy(item => item.Radius)
            .FirstOrDefault();
        if (agent is null || agent.CanFly || !navigation.AgentProfiles.Any(item => item.Id == agent.Id))
        {
            diagnostics.Add(new(
                MissingGroundAgentCode,
                "Surface navigation requires a configured non-flying agent profile.",
                "navigation/agentProfiles"));
            return new(false, null, terrain.Vertices.Select(item => item.Position).ToArray(), [], [], 0, diagnostics);
        }

        var triangles = new List<SurfaceNavigationTriangle>(terrain.TriangleCount);
        var blocked = 0;
        for (var index = 0; index < terrain.Indices.Count; index += 3)
        {
            var aIndex = terrain.Indices[index];
            var bIndex = terrain.Indices[index + 1];
            var cIndex = terrain.Indices[index + 2];
            var a = terrain.Vertices[aIndex].Position;
            var b = terrain.Vertices[bIndex].Position;
            var c = terrain.Vertices[cIndex].Position;
            var center = new Vector3Value(
                (a.X + b.X + c.X) / 3f,
                (a.Y + b.Y + c.Y) / 3f,
                (a.Z + b.Z + c.Z) / 3f);
            if (SlopeDegrees(a, b, c) > MaximumSlopeDegrees || IsBlockedByFeature(world.Features, center, agent.Radius))
            {
                blocked++;
                continue;
            }

            triangles.Add(new(aIndex, bIndex, cIndex));
        }

        if (triangles.Count == 0)
        {
            diagnostics.Add(new(
                EmptySurfaceCode,
                "Terrain and crossing policies produced an empty navigation surface.",
                "world/terrain"));
        }

        var links = navigation.OffMeshLinks
            .Where(link => link.MaximumAgentRadius >= agent.Radius && link.Kind != NavigationLinkKind.Flight)
            .Select(link => new SurfaceNavigationLinkPlan(
                link.Id,
                link.Name,
                link.Start,
                link.End,
                link.Bidirectional))
            .ToArray();
        return new(
            triangles.Count > 0,
            agent,
            terrain.Vertices.Select(item => item.Position).ToArray(),
            triangles,
            links,
            blocked,
            diagnostics);
    }

    private static float SlopeDegrees(Vector3Value a, Vector3Value b, Vector3Value c)
    {
        var ab = new Vector3Value(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var ac = new Vector3Value(c.X - a.X, c.Y - a.Y, c.Z - a.Z);
        var normalX = (ab.Y * ac.Z) - (ab.Z * ac.Y);
        var normalY = (ab.Z * ac.X) - (ab.X * ac.Z);
        var normalZ = (ab.X * ac.Y) - (ab.Y * ac.X);
        var length = MathF.Sqrt((normalX * normalX) + (normalY * normalY) + (normalZ * normalZ));
        if (length <= float.Epsilon) return 90;
        var cosine = Math.Clamp(MathF.Abs(normalY) / length, 0, 1);
        return MathF.Acos(cosine) * 180f / MathF.PI;
    }

    private static bool IsBlockedByFeature(
        IReadOnlyList<TerrainFeatureDefinition> features,
        Vector3Value point,
        float agentRadius)
    {
        var bridges = features.Where(feature => feature.Kind == TerrainFeatureKind.Bridge).ToArray();
        if (bridges.Any(bridge => IsInsideCorridor(bridge, point, agentRadius))) return false;

        return features.Any(feature =>
            feature.Kind is TerrainFeatureKind.River or TerrainFeatureKind.Wall or TerrainFeatureKind.Cliff &&
            feature.CrossingPolicy is TerrainFeatureCrossingPolicy.RequiresBridge or TerrainFeatureCrossingPolicy.Prohibited &&
            IsInsideCorridor(feature, point, agentRadius));
    }

    private static bool IsInsideCorridor(TerrainFeatureDefinition feature, Vector3Value point, float margin)
    {
        var maximumDistance = (feature.Width / 2f) + margin;
        for (var index = 0; index < feature.ControlPoints.Count - 1; index++)
        {
            if (DistanceToSegment(point, feature.ControlPoints[index], feature.ControlPoints[index + 1]) <= maximumDistance)
                return true;
        }

        return false;
    }

    private static float DistanceToSegment(Vector3Value point, Vector3Value start, Vector3Value end)
    {
        var dx = end.X - start.X;
        var dz = end.Z - start.Z;
        var lengthSquared = (dx * dx) + (dz * dz);
        if (lengthSquared <= float.Epsilon)
        {
            var px = point.X - start.X;
            var pz = point.Z - start.Z;
            return MathF.Sqrt((px * px) + (pz * pz));
        }

        var projection = Math.Clamp(
            (((point.X - start.X) * dx) + ((point.Z - start.Z) * dz)) / lengthSquared,
            0,
            1);
        var nearestX = start.X + (dx * projection);
        var nearestZ = start.Z + (dz * projection);
        var offsetX = point.X - nearestX;
        var offsetZ = point.Z - nearestZ;
        return MathF.Sqrt((offsetX * offsetX) + (offsetZ * offsetZ));
    }
}
