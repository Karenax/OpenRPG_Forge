using Forge.Domain.Areas;
using Forge.Domain.Validation;
using Godot;

namespace Forge.EngineAdapter.World;

public sealed record GodotTerrainResult(Node3D Root, int Vertices, int Triangles, int CollisionShapes,
    int FeatureSegments, int DebugVolumes, int FlightRouteSegments, int ScatterLayers, int ScatterInstances,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class GodotTerrainAdapter
{
    public static GodotTerrainResult Build(AreaWorldProfile world, Func<string, Mesh?>? scatterMeshResolver = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        var plan = TerrainBuildPlanBuilder.Build(world);
        var root = new Node3D { Name = "ForgeAreaWorld" };
        var collisionShapes = 0;
        if (plan.Enabled && plan.Indices.Count > 0)
        {
            var mesh = BuildTerrainMesh(plan);
            root.AddChild(new MeshInstance3D { Name = "TerrainSurface", Mesh = mesh });
            var body = new StaticBody3D { Name = "TerrainCollision" };
            body.AddChild(new CollisionShape3D { Name = "TerrainShape", Shape = mesh.CreateTrimeshShape() });
            root.AddChild(body);
            collisionShapes = 1;
        }

        var featureSegments = 0;
        var featureRoot = new Node3D { Name = "TerrainFeatures" };
        foreach (var feature in plan.Features)
        {
            for (var index = 0; index < feature.ControlPoints.Count - 1; index++)
            {
                featureRoot.AddChild(BuildSegment(feature.ControlPoints[index], feature.ControlPoints[index + 1],
                    feature.Width, Math.Max(feature.Height, .08f), FeatureColor(feature.Kind), $"{feature.Kind}_{feature.Id:N}_{index}"));
                featureSegments++;
            }
        }
        root.AddChild(featureRoot);

        var debugVolumes = 0;
        if (plan.VolumetricNavigation.DebugVisible)
        {
            var volumeRoot = new Node3D { Name = "VolumetricNavigationDebug" };
            volumeRoot.AddChild(BuildVolume(plan.VolumetricNavigation.Center, plan.VolumetricNavigation.Size,
                new Color(.2f, .75f, 1, .09f), "NavigationBounds"));
            debugVolumes++;
            foreach (var obstacle in plan.VolumetricNavigation.Obstacles)
            {
                volumeRoot.AddChild(BuildVolume(obstacle.Center, obstacle.Size, new Color(1, .2f, .15f, .2f),
                    $"Obstacle_{obstacle.Id:N}"));
                debugVolumes++;
            }
            foreach (var cost in plan.VolumetricNavigation.CostVolumes)
            {
                volumeRoot.AddChild(BuildVolume(cost.Center, cost.Size, new Color(1, .65f, .1f, .15f),
                    $"Cost_{cost.Id:N}"));
                debugVolumes++;
            }
            root.AddChild(volumeRoot);
        }

        var routeSegments = 0;
        var routesRoot = new Node3D { Name = "FlightRoutes" };
        foreach (var route in plan.FlightRoutes.Where(route => route.DebugVisible))
        {
            for (var index = 0; index < route.Nodes.Count - 1; index++)
            {
                routesRoot.AddChild(BuildSegment(route.Nodes[index].Position, route.Nodes[index + 1].Position,
                    .08f, .08f, new Color(.2f, .9f, 1, .9f), $"Flight_{route.Id:N}_{index}"));
                routeSegments++;
            }
        }
        root.AddChild(routesRoot);

        var scatter = GodotBiomeScatterAdapter.Build(world, scatterMeshResolver);
        root.AddChild(scatter.Root);
        collisionShapes += scatter.CollisionShapes;

        return new GodotTerrainResult(root, plan.Vertices.Count, plan.TriangleCount, collisionShapes,
            featureSegments, debugVolumes, routeSegments, scatter.Layers, scatter.Instances, plan.Diagnostics);
    }

    private static ArrayMesh BuildTerrainMesh(TerrainBuildPlan plan)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var index in plan.Indices)
        {
            var vertex = plan.Vertices[index];
            surface.SetColor(new Color(vertex.AlbedoColor));
            surface.SetUV(new Vector2(vertex.U, vertex.V));
            surface.AddVertex(new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z));
        }
        surface.GenerateNormals();
        var mesh = surface.Commit();
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = .88f,
            Metallic = .02f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        });
        return mesh;
    }

    private static MeshInstance3D BuildSegment(Forge.Domain.Spatial.Vector3Value start,
        Forge.Domain.Spatial.Vector3Value end, float width, float height, Color color, string name)
    {
        var startValue = new Vector3(start.X, start.Y, start.Z);
        var endValue = new Vector3(end.X, end.Y, end.Z);
        var delta = endValue - startValue;
        var length = Math.Max(delta.Length(), .01f);
        var node = new MeshInstance3D
        {
            Name = name,
            Position = (startValue + endValue) / 2 + new Vector3(0, height / 2, 0),
            Rotation = new Vector3(0, MathF.Atan2(delta.X, delta.Z), 0),
            Mesh = new BoxMesh
            {
                Size = new Vector3(width, height, length),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    Roughness = .72f,
                    Transparency = color.A < .999f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
                },
            },
        };
        return node;
    }

    private static MeshInstance3D BuildVolume(Forge.Domain.Spatial.Vector3Value center,
        Forge.Domain.Spatial.Vector3Value size, Color color, string name) => new()
        {
            Name = name,
            Position = new Vector3(center.X, center.Y, center.Z),
            Mesh = new BoxMesh
            {
                Size = new Vector3(size.X, size.Y, size.Z),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
            },
        };

    private static Color FeatureColor(TerrainFeatureKind kind) => kind switch
    {
        TerrainFeatureKind.Road => new Color(.24f, .19f, .14f),
        TerrainFeatureKind.River => new Color(.08f, .36f, .62f, .78f),
        TerrainFeatureKind.Wall => new Color(.44f, .42f, .4f),
        TerrainFeatureKind.Bridge => new Color(.38f, .22f, .1f),
        TerrainFeatureKind.Cliff => new Color(.35f, .34f, .32f),
        TerrainFeatureKind.Building => new Color(.5f, .42f, .3f),
        _ => new Color(.38f, .55f, .24f),
    };
}
