using System.Diagnostics;
using Forge.Domain.Areas;
using Godot;

namespace Forge.EngineAdapter.World;

public sealed record GodotBiomeScatterResult(
    Node3D Root,
    int Layers,
    int Instances,
    int CollisionShapes,
    int CullingGroups,
    double BuildMilliseconds,
    double CollisionMilliseconds);

public static class GodotBiomeScatterAdapter
{
    public static GodotBiomeScatterResult Build(AreaWorldProfile world, Func<string, Mesh?>? meshResolver)
    {
        ArgumentNullException.ThrowIfNull(world);
        var started = Stopwatch.GetTimestamp();
        var root = new Node3D { Name = "BiomeScatter" };
        if (!world.Terrain.Enabled || meshResolver is null)
            return new(root, 0, 0, 0, 0, Stopwatch.GetElapsedTime(started).TotalMilliseconds, 0);

        var layers = 0;
        var instances = 0;
        var collisionShapes = 0;
        var cullingGroups = 0;
        var collisionMilliseconds = 0d;
        foreach (var biome in world.Biomes.Where(candidate => !string.IsNullOrWhiteSpace(candidate.ScatterResource)))
        {
            var mesh = meshResolver(biome.ScatterResource);
            if (mesh is null) continue;
            var placements = BiomeScatterPlanBuilder.Build(world, biome);
            if (placements.Count == 0) continue;
            var multiMesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = mesh,
                InstanceCount = placements.Count,
            };
            for (var index = 0; index < placements.Count; index++)
            {
                var placement = placements[index];
                var basis = Basis.FromEuler(new Vector3(0, placement.YawRadians, 0))
                    .Scaled(Vector3.One * placement.UniformScale);
                multiMesh.SetInstanceTransform(index, new Transform3D(
                    basis,
                    new Vector3(placement.Position.X, placement.Position.Y, placement.Position.Z)));
            }
            var cullingRange = biome.Tags.Contains("ground-cover", StringComparer.OrdinalIgnoreCase) ? 42 : 120;
            root.AddChild(new MultiMeshInstance3D
            {
                Name = $"Scatter_{biome.Id:N}",
                Multimesh = multiMesh,
                VisibilityRangeEnd = cullingRange,
                VisibilityRangeEndMargin = 8,
            });
            layers++;
            instances += placements.Count;
            cullingGroups++;

            if (biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase))
            {
                var collisionStarted = Stopwatch.GetTimestamp();
                var body = new StaticBody3D { Name = $"ScatterCollision_{biome.Id:N}" };
                foreach (var placement in placements.Take(96))
                {
                    body.AddChild(new CollisionShape3D
                    {
                        Position = new Vector3(placement.Position.X, placement.Position.Y, placement.Position.Z) +
                                   Vector3.Up * 1.4f,
                        Shape = new CylinderShape3D { Radius = .38f, Height = 2.8f },
                    });
                    collisionShapes++;
                }
                root.AddChild(body);
                collisionMilliseconds += Stopwatch.GetElapsedTime(collisionStarted).TotalMilliseconds;
            }
        }

        return new(root, layers, instances, collisionShapes, cullingGroups,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds, collisionMilliseconds);
    }

}
