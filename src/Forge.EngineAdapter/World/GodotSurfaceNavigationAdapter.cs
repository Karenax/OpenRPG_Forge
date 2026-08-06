using System.Diagnostics;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Validation;
using Godot;

namespace Forge.EngineAdapter.World;

public sealed record GodotSurfaceNavigationResult(
    Node3D Root,
    int Regions,
    int Polygons,
    int BlockedTriangles,
    int Links,
    double BuildMilliseconds,
    long ManagedAllocatedBytes,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class GodotSurfaceNavigationAdapter
{
    public static GodotSurfaceNavigationResult Build(
        AreaWorldProfile world,
        AreaNavigationDefinition navigation,
        NavigationAgentProfile? requestedAgent = null)
    {
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        var plan = SurfaceNavigationPlanBuilder.Build(world, navigation, requestedAgent);
        var root = new Node3D { Name = "ForgeSurfaceNavigation" };
        if (!plan.Enabled || plan.Agent is null)
        {
            return new(root, 0, 0, plan.BlockedTriangles, 0,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore),
                plan.Diagnostics);
        }

        var navigationMesh = new NavigationMesh
        {
            CellSize = Math.Clamp(navigation.CellSize, .25f, 1f),
            CellHeight = .2f,
            AgentRadius = plan.Agent.Radius,
            AgentHeight = plan.Agent.Height,
            AgentMaxClimb = .6f,
            AgentMaxSlope = SurfaceNavigationPlanBuilder.MaximumSlopeDegrees,
        };
        navigationMesh.SetVertices(plan.Vertices
            .Select(vertex => new Vector3(vertex.X, vertex.Y, vertex.Z))
            .ToArray());
        foreach (var triangle in plan.Triangles)
        {
            navigationMesh.AddPolygon([triangle.A, triangle.B, triangle.C]);
        }

        var region = new NavigationRegion3D
        {
            Name = $"Surface_{plan.Agent.Name}",
            NavigationMesh = navigationMesh,
            Enabled = true,
            UseEdgeConnections = true,
        };
        region.SetMeta("forge_navigation_agent", plan.Agent.Id.ToString());
        root.AddChild(region);
        foreach (var link in plan.Links)
        {
            var linkNode = new NavigationLink3D
            {
                Name = $"Link_{link.Id}",
                Enabled = true,
                Bidirectional = link.Bidirectional,
                StartPosition = new Vector3(link.Start.X, link.Start.Y, link.Start.Z),
                EndPosition = new Vector3(link.End.X, link.End.Y, link.End.Z),
            };
            linkNode.SetMeta("forge_navigation_link", link.Id.ToString());
            root.AddChild(linkNode);
        }

        return new(root, 1, plan.Triangles.Count, plan.BlockedTriangles, plan.Links.Count,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore),
            plan.Diagnostics);
    }
}
