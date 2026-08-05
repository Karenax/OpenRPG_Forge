using Forge.Domain.Areas;
using Godot;

namespace Forge.EngineAdapter.World;

public sealed record GodotBiomeScatterResult(Node3D Root, int Layers, int Instances, int CollisionShapes);

public static class GodotBiomeScatterAdapter
{
    public static GodotBiomeScatterResult Build(AreaWorldProfile world, Func<string, Mesh?>? meshResolver)
    {
        ArgumentNullException.ThrowIfNull(world);
        var root = new Node3D { Name = "BiomeScatter" };
        if (!world.Terrain.Enabled || meshResolver is null)
            return new(root, 0, 0, 0);

        var layers = 0;
        var instances = 0;
        var collisionShapes = 0;
        foreach (var biome in world.Biomes.Where(candidate => !string.IsNullOrWhiteSpace(candidate.ScatterResource)))
        {
            var mesh = meshResolver(biome.ScatterResource);
            if (mesh is null) continue;
            var transforms = PlanTransforms(world, biome);
            if (transforms.Count == 0) continue;
            var multiMesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = mesh,
                InstanceCount = transforms.Count,
            };
            for (var index = 0; index < transforms.Count; index++)
                multiMesh.SetInstanceTransform(index, transforms[index]);
            root.AddChild(new MultiMeshInstance3D
            {
                Name = $"Scatter_{biome.Id:N}",
                Multimesh = multiMesh,
                VisibilityRangeEnd = biome.Tags.Contains("ground-cover", StringComparer.OrdinalIgnoreCase) ? 42 : 120,
                VisibilityRangeEndMargin = 8,
            });
            layers++;
            instances += transforms.Count;

            if (biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase))
            {
                var body = new StaticBody3D { Name = $"ScatterCollision_{biome.Id:N}" };
                foreach (var transform in transforms.Take(96))
                {
                    body.AddChild(new CollisionShape3D
                    {
                        Position = transform.Origin + Vector3.Up * 1.4f,
                        Shape = new CylinderShape3D { Radius = .38f, Height = 2.8f },
                    });
                    collisionShapes++;
                }
                root.AddChild(body);
            }
        }

        return new(root, layers, instances, collisionShapes);
    }

    private static IReadOnlyList<Transform3D> PlanTransforms(AreaWorldProfile world, BiomeLayerDefinition biome)
    {
        var terrain = world.Terrain;
        var target = Math.Clamp((int)Math.Round(terrain.Width * terrain.Depth / 90d * biome.ScatterDensity),
            0, biome.Tags.Contains("ground-cover", StringComparer.OrdinalIgnoreCase) ? 420 : 220);
        var result = new List<Transform3D>(target);
        var biomeSeedBytes = biome.Id.ToByteArray();
        var biomeSeed = BitConverter.ToInt32(biomeSeedBytes, 0);
        var random = new StableRandom(unchecked(terrain.Seed ^ biomeSeed));
        var minimumDistance = biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase) ? 3.4f : 1.1f;
        for (var attempt = 0; attempt < target * 18 && result.Count < target; attempt++)
        {
            var x = (random.NextFloat() - .5f) * terrain.Width;
            var z = (random.NextFloat() - .5f) * terrain.Depth;
            var height = terrain.HeightAt(x, z);
            var slope = SlopeDegrees(terrain, x, z);
            if (height < biome.MinimumAltitude || height > biome.MaximumAltitude ||
                slope < biome.MinimumSlopeDegrees || slope > biome.MaximumSlopeDegrees ||
                IsExcluded(world, biome, x, z) ||
                result.Any(existing => DistanceSquared(existing.Origin, x, z) < minimumDistance * minimumDistance))
                continue;
            var authoredScale = biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase)
                ? 2.75f
                : biome.Tags.Contains("ground-cover", StringComparer.OrdinalIgnoreCase) ? 1.35f : 1f;
            var scale = (.84f + random.NextFloat() * .42f) * authoredScale;
            var basis = Basis.FromEuler(new Vector3(0, random.NextFloat() * MathF.Tau, 0)).Scaled(Vector3.One * scale);
            result.Add(new Transform3D(basis, new Vector3(x, height, z)));
        }
        return result;
    }

    private static bool IsExcluded(AreaWorldProfile world, BiomeLayerDefinition biome, float x, float z)
    {
        foreach (var feature in world.Features)
        {
            var tag = feature.Kind.ToString().ToLowerInvariant();
            if (!biome.ExclusionTags.Contains(tag, StringComparer.OrdinalIgnoreCase)) continue;
            for (var index = 0; index < feature.ControlPoints.Count - 1; index++)
            {
                var a = feature.ControlPoints[index];
                var b = feature.ControlPoints[index + 1];
                if (DistanceToSegment(x, z, a.X, a.Z, b.X, b.Z) <= feature.Width * .5f + 1.5f)
                    return true;
            }
        }
        return false;
    }

    private static float SlopeDegrees(TerrainDefinition terrain, float x, float z)
    {
        const float step = 1f;
        var dx = terrain.HeightAt(x + step, z) - terrain.HeightAt(x - step, z);
        var dz = terrain.HeightAt(x, z + step) - terrain.HeightAt(x, z - step);
        return MathF.Atan(MathF.Sqrt(dx * dx + dz * dz) / (step * 2)) * 180f / MathF.PI;
    }

    private static float DistanceSquared(Vector3 position, float x, float z)
    {
        var dx = position.X - x;
        var dz = position.Z - z;
        return dx * dx + dz * dz;
    }

    private static float DistanceToSegment(float px, float pz, float ax, float az, float bx, float bz)
    {
        var dx = bx - ax;
        var dz = bz - az;
        var length = dx * dx + dz * dz;
        if (length <= .0001f) return MathF.Sqrt((px - ax) * (px - ax) + (pz - az) * (pz - az));
        var t = Math.Clamp(((px - ax) * dx + (pz - az) * dz) / length, 0, 1);
        var x = ax + t * dx;
        var z = az + t * dz;
        return MathF.Sqrt((px - x) * (px - x) + (pz - z) * (pz - z));
    }

    private sealed class StableRandom
    {
        private uint _state;
        internal StableRandom(int seed) => _state = unchecked((uint)seed) | 1u;
        internal float NextFloat()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (_state & 0x00ffffff) / 16777216f;
        }
    }
}
