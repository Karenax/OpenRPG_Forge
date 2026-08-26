using System.Diagnostics;
using Forge.Domain.Areas;
using Forge.Domain.Validation;
using Forge.EngineAdapter.Rendering;
using Godot;

namespace Forge.EngineAdapter.World;

public sealed record GodotTerrainBuildMetrics(
    double TotalMilliseconds,
    double PlanMilliseconds,
    double MeshMilliseconds,
    double CollisionMilliseconds,
    double FeatureMilliseconds,
    double ScatterMilliseconds,
    double ScatterMeshResolutionMilliseconds,
    double ScatterMeshPreparationMilliseconds,
    double ScatterPlacementMilliseconds,
    double ScatterCellPlanningMilliseconds,
    double ScatterInstanceUploadMilliseconds,
    long ManagedAllocatedBytes);

public sealed record GodotTerrainResult(Node3D Root, int Vertices, int Triangles, int TerrainSectors,
    string TerrainMaterialMode, int TerrainMaterialLayers, int CollisionShapes,
    int FeatureSegments, int DebugVolumes, int FlightRouteSegments, int ScatterLayers, int ScatterInstances,
    int WindAnimatedScatterLayers,
    int WaterSurfaces, int WaterVolumes, int CullingGroups, GodotTerrainBuildMetrics Metrics,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class GodotTerrainAdapter
{
    public static GodotTerrainResult Build(
        AreaWorldProfile world,
        Func<string, Mesh?>? scatterMeshResolver = null,
        IReadOnlyList<WaterBodyProfile>? waterBodies = null,
        Func<string, Texture2D?>? surfaceTextureResolver = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var totalStarted = Stopwatch.GetTimestamp();
        var planStarted = Stopwatch.GetTimestamp();
        var plan = TerrainBuildPlanBuilder.Build(world);
        var planMilliseconds = Stopwatch.GetElapsedTime(planStarted).TotalMilliseconds;
        var root = new Node3D { Name = "ForgeAreaWorld" };
        var collisionShapes = 0;
        var meshMilliseconds = 0d;
        var collisionMilliseconds = 0d;
        var terrainMaterial = GodotTerrainMaterialAdapter.Build(world.Terrain, surfaceTextureResolver);
        root.SetMeta("forge_terrain_material_mode", terrainMaterial.Descriptor.Mode);
        root.SetMeta("forge_terrain_material_layers", terrainMaterial.Descriptor.LayerCount);
        if (plan.Enabled && plan.Indices.Count > 0)
        {
            var meshStarted = Stopwatch.GetTimestamp();
            var sectors = TerrainChunkPlanBuilder.Build(world, plan);
            var sectorRoot = new Node3D { Name = "TerrainSectors" };
            foreach (var sector in sectors)
            {
                var sectorNode = new MeshInstance3D
                {
                    Name = $"TerrainSector_{sector.X}_{sector.Z}",
                    Position = new Vector3(sector.Center.X, sector.Center.Y, sector.Center.Z),
                    Mesh = BuildTerrainMesh(plan, sector.Indices, sector.Center, terrainMaterial.Material),
                    VisibilityRangeEnd = sector.VisibilityRangeEnd,
                    VisibilityRangeEndMargin = MathF.Min(32, sector.VisibilityRangeEnd * .12f),
                };
                sectorNode.AddToGroup("forge_terrain_sector");
                sectorNode.SetMeta("forge_sector_x", sector.X);
                sectorNode.SetMeta("forge_sector_z", sector.Z);
                sectorRoot.AddChild(sectorNode);
            }
            root.AddChild(sectorRoot);
            meshMilliseconds = Stopwatch.GetElapsedTime(meshStarted).TotalMilliseconds;
            var collisionStarted = Stopwatch.GetTimestamp();
            var collisionMesh = BuildTerrainMesh(plan, plan.Indices, Forge.Domain.Spatial.Vector3Value.Zero, null);
            var body = new StaticBody3D { Name = "TerrainCollision" };
            body.AddChild(new CollisionShape3D { Name = "TerrainShape", Shape = collisionMesh.CreateTrimeshShape() });
            root.AddChild(body);
            collisionShapes = 1;
            collisionMilliseconds = Stopwatch.GetElapsedTime(collisionStarted).TotalMilliseconds;
        }

        var featureStarted = Stopwatch.GetTimestamp();
        var featureSegments = 0;
        var waterSurfaces = 0;
        var waterVolumes = 0;
        var featureRoot = new Node3D { Name = "TerrainFeatures" };
        foreach (var feature in plan.Features)
        {
            if (string.Equals(feature.MaterialResource, "builtin/invisible-scatter-exclusion",
                    StringComparison.OrdinalIgnoreCase))
            {
                featureSegments += feature.ControlPoints.Count - 1;
                continue;
            }
            if (feature.Kind == TerrainFeatureKind.Road)
            {
                if (!string.Equals(feature.MaterialResource, "builtin/terrain-painted",
                        StringComparison.OrdinalIgnoreCase))
                    featureRoot.AddChild(BuildRoad(feature, world.Terrain, surfaceTextureResolver));
                featureSegments += feature.ControlPoints.Count - 1;
                continue;
            }

            for (var index = 0; index < feature.ControlPoints.Count - 1; index++)
            {
                if (feature.Kind == TerrainFeatureKind.River)
                {
                    featureRoot.AddChild(BuildRiverSegment(
                        feature.ControlPoints[index],
                        feature.ControlPoints[index + 1],
                        feature.Width,
                        $"{feature.Kind}_{feature.Id:N}_{index}"));
                    waterSurfaces++;
                    waterVolumes++;
                }
                else
                {
                    featureRoot.AddChild(BuildSegment(feature.ControlPoints[index], feature.ControlPoints[index + 1],
                        feature.Width, Math.Max(feature.Height, .08f), FeatureColor(feature.Kind),
                        $"{feature.Kind}_{feature.Id:N}_{index}", feature.Kind));
                }
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
        var featureMilliseconds = Stopwatch.GetElapsedTime(featureStarted).TotalMilliseconds;

        var scatterStarted = Stopwatch.GetTimestamp();
        var scatter = GodotBiomeScatterAdapter.Build(world, scatterMeshResolver, waterBodies);
        root.AddChild(scatter.Root);
        collisionShapes += scatter.CollisionShapes;
        collisionMilliseconds += scatter.CollisionMilliseconds;
        var scatterMilliseconds = Stopwatch.GetElapsedTime(scatterStarted).TotalMilliseconds;

        var metrics = new GodotTerrainBuildMetrics(
            Stopwatch.GetElapsedTime(totalStarted).TotalMilliseconds,
            planMilliseconds,
            meshMilliseconds,
            collisionMilliseconds,
            featureMilliseconds,
            scatterMilliseconds,
            scatter.MeshResolutionMilliseconds,
            scatter.MeshPreparationMilliseconds,
            scatter.PlacementMilliseconds,
            scatter.CellPlanningMilliseconds,
            scatter.InstanceUploadMilliseconds,
            Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore));

        return new GodotTerrainResult(root, plan.Vertices.Count, plan.TriangleCount, plan.ChunkCount,
            terrainMaterial.Descriptor.Mode, terrainMaterial.Descriptor.LayerCount, collisionShapes,
            featureSegments, debugVolumes, routeSegments, scatter.Layers, scatter.Instances,
            scatter.WindAnimatedLayers, waterSurfaces, waterVolumes, scatter.CullingGroups, metrics, plan.Diagnostics);
    }

    private static ArrayMesh BuildTerrainMesh(
        TerrainBuildPlan plan,
        IReadOnlyList<int> indices,
        Forge.Domain.Spatial.Vector3Value origin,
        Material? material)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        void AddVertex(int index)
        {
            var vertex = plan.Vertices[index];
            surface.SetColor(new Color(vertex.AlbedoColor));
            surface.SetNormal(new Vector3(vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z));
            surface.SetUV(new Vector2(vertex.U, vertex.V));
            surface.AddVertex(new Vector3(
                vertex.Position.X - origin.X,
                vertex.Position.Y - origin.Y,
                vertex.Position.Z - origin.Z));
        }

        for (var triangle = 0; triangle < indices.Count; triangle += 3)
        {
            // Forge's canonical X/Z grid is ordered for domain and navigation processing.
            // Godot's front-face convention is the opposite, and concave collision shapes
            // ignore back faces by default. Reverse each triangle when building the engine
            // mesh so the visible surface is also the collidable surface.
            AddVertex(indices[triangle]);
            AddVertex(indices[triangle + 2]);
            AddVertex(indices[triangle + 1]);
        }

        // Re-index canonical samples while retaining the normals computed once from the
        // complete height field. Sector-local normal generation creates visible seams.
        surface.Index();
        var mesh = surface.Commit();
        mesh.SetMeta(GodotSurfaceWetnessAdapter.LocalResourceMeta, true);
        if (material is not null)
            mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private static MeshInstance3D BuildSegment(Forge.Domain.Spatial.Vector3Value start,
        Forge.Domain.Spatial.Vector3Value end, float width, float height, Color color, string name,
        TerrainFeatureKind? featureKind = null)
    {
        var startValue = new Vector3(start.X, start.Y, start.Z);
        var endValue = new Vector3(end.X, end.Y, end.Z);
        var delta = endValue - startValue;
        var length = Math.Max(delta.Length(), .01f);
        var node = new MeshInstance3D
        {
            Name = name,
            Position = (startValue + endValue) / 2 + new Vector3(
                0,
                featureKind == TerrainFeatureKind.Road ? -height * .4f : height / 2,
                0),
            Rotation = new Vector3(0, MathF.Atan2(delta.X, delta.Z), 0),
            Mesh = new BoxMesh
            {
                Size = new Vector3(width, height, length),
                Material = featureKind == TerrainFeatureKind.Road
                    ? GodotTerrainMaterialAdapter.BuildRoad(color)
                    : new StandardMaterial3D
                    {
                        AlbedoColor = color,
                        Roughness = .72f,
                        Transparency = color.A < .999f
                            ? BaseMaterial3D.TransparencyEnum.Alpha
                            : BaseMaterial3D.TransparencyEnum.Disabled,
                    },
            },
        };
        return node;
    }

    private static MeshInstance3D BuildRoad(
        TerrainFeatureDefinition feature,
        TerrainDefinition terrain,
        Func<string, Texture2D?>? surfaceTextureResolver)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var halfWidth = feature.Width * .5f;
        var left = new Vector3[feature.ControlPoints.Count];
        var right = new Vector3[feature.ControlPoints.Count];
        var distance = new float[feature.ControlPoints.Count];
        for (var index = 0; index < feature.ControlPoints.Count; index++)
        {
            var current = feature.ControlPoints[index];
            var previous = feature.ControlPoints[Math.Max(0, index - 1)];
            var next = feature.ControlPoints[Math.Min(feature.ControlPoints.Count - 1, index + 1)];
            var tangent = new Vector3(next.X - previous.X, 0, next.Z - previous.Z).Normalized();
            var perpendicular = new Vector3(tangent.Z, 0, -tangent.X);
            var center = new Vector3(current.X, current.Y, current.Z);
            var leftPosition = center - perpendicular * halfWidth;
            var rightPosition = center + perpendicular * halfWidth;
            var authoredOffset = current.Y - terrain.HeightAt(current.X, current.Z) + .012f;
            leftPosition.Y = terrain.HeightAt(leftPosition.X, leftPosition.Z) + authoredOffset;
            rightPosition.Y = terrain.HeightAt(rightPosition.X, rightPosition.Z) + authoredOffset;
            left[index] = leftPosition;
            right[index] = rightPosition;
            if (index > 0)
            {
                var former = feature.ControlPoints[index - 1];
                distance[index] = distance[index - 1] + new Vector2(current.X - former.X, current.Z - former.Z).Length();
            }
        }

        for (var index = 0; index < feature.ControlPoints.Count - 1; index++)
        {
            var v0 = distance[index] / 5f;
            var v1 = distance[index + 1] / 5f;
            AddRoadVertex(surface, left[index], new Vector2(0, v0));
            AddRoadVertex(surface, left[index + 1], new Vector2(0, v1));
            AddRoadVertex(surface, right[index], new Vector2(1, v0));
            AddRoadVertex(surface, right[index], new Vector2(1, v0));
            AddRoadVertex(surface, left[index + 1], new Vector2(0, v1));
            AddRoadVertex(surface, right[index + 1], new Vector2(1, v1));
        }

        var mesh = surface.Commit();
        mesh.SetMeta(GodotSurfaceWetnessAdapter.LocalResourceMeta, true);
        var surfaceTexture = string.IsNullOrWhiteSpace(feature.MaterialResource) ||
            feature.MaterialResource.StartsWith("builtin/", StringComparison.OrdinalIgnoreCase)
                ? null
                : surfaceTextureResolver?.Invoke(feature.MaterialResource);
        mesh.SurfaceSetMaterial(0, GodotTerrainMaterialAdapter.BuildRoad(
            FeatureColor(TerrainFeatureKind.Road), surfaceTexture, 3.6f));
        return new MeshInstance3D
        {
            Name = $"Road_{feature.Id:N}",
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    private static void AddRoadVertex(SurfaceTool surface, Vector3 position, Vector2 uv)
    {
        // Authored splines may run in either direction. A generated winding normal can
        // therefore point below the terrain and turn an otherwise valid road almost black.
        surface.SetNormal(Vector3.Up);
        surface.SetUV(uv);
        surface.AddVertex(position);
    }

    private static Node3D BuildRiverSegment(
        Forge.Domain.Spatial.Vector3Value start,
        Forge.Domain.Spatial.Vector3Value end,
        float width,
        string name)
    {
        var startValue = new Vector3(start.X, start.Y, start.Z);
        var endValue = new Vector3(end.X, end.Y, end.Z);
        var delta = endValue - startValue;
        var length = Math.Max(delta.Length(), .01f);
        var horizontalDirection = new Vector3(delta.X, 0, delta.Z).Normalized();
        var root = new Node3D
        {
            Name = name,
            Position = ((startValue + endValue) / 2) + new Vector3(0, .045f, 0),
            Rotation = new Vector3(0, MathF.Atan2(delta.X, delta.Z), 0),
        };
        var material = GodotWaterMaterialFactory.Create(
            new Vector2(horizontalDirection.X, horizontalDirection.Z), 1.2f, WaterQualityTier.Ultra);
        root.AddChild(new MeshInstance3D
        {
            Name = "AnimatedSurface",
            Mesh = new PlaneMesh
            {
                Size = new Vector2(width, length),
                SubdivideWidth = Math.Clamp((int)MathF.Ceiling(width), 1, 12),
                SubdivideDepth = Math.Clamp((int)MathF.Ceiling(length / 2f), 1, 64),
                Material = material,
            },
        });

        var volume = new Area3D
        {
            Name = "SwimmableCurrent",
            Monitoring = true,
            Monitorable = true,
            CollisionLayer = 0,
            CollisionMask = 1,
        };
        volume.AddToGroup("forge_water_volume");
        volume.SetMeta("forge_swimming_enabled", true);
        volume.SetMeta("forge_buoyancy", 1.15f);
        volume.SetMeta("forge_navigation_cost", 2.5f);
        volume.SetMeta("forge_flow_direction", horizontalDirection);
        volume.SetMeta("forge_flow_speed", .65f);
        volume.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, -.65f, 0),
            Shape = new BoxShape3D { Size = new Vector3(width, 1.4f, length) },
        });
        root.AddChild(volume);
        return root;
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
        TerrainFeatureKind.Road => new Color(.90f, .75f, .52f),
        TerrainFeatureKind.River => new Color(.08f, .36f, .62f, .78f),
        TerrainFeatureKind.Wall => new Color(.44f, .42f, .4f),
        TerrainFeatureKind.Bridge => new Color(.38f, .22f, .1f),
        TerrainFeatureKind.Cliff => new Color(.35f, .34f, .32f),
        TerrainFeatureKind.Building => new Color(.5f, .42f, .3f),
        _ => new Color(.38f, .55f, .24f),
    };
}
