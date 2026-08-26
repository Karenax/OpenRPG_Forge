using System.Diagnostics;
using System.Globalization;
using Forge.Domain.Areas;
using Godot;

namespace Forge.EngineAdapter.World;

public sealed record GodotBiomeScatterResult(
    Node3D Root,
    int Layers,
    int Instances,
    int WindAnimatedLayers,
    int CollisionShapes,
    int CullingGroups,
    double BuildMilliseconds,
    double CollisionMilliseconds,
    double MeshResolutionMilliseconds,
    double MeshPreparationMilliseconds,
    double PlacementMilliseconds,
    double CellPlanningMilliseconds,
    double InstanceUploadMilliseconds);

public static class GodotBiomeScatterAdapter
{
    public const string WindAnimatedTag = "wind-animated";

    public const string WindVegetationShaderCode = """
        shader_type spatial;
        render_mode depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

        global uniform vec2 gnd_wind_direction;
        global uniform float gnd_wind_speed;
        global uniform float gnd_wind_strength;

        uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable, hint_default_white;
        uniform vec4 albedo_tint : source_color = vec4(1.0);
        uniform float material_roughness : hint_range(0.0, 1.0) = 0.82;
        uniform float material_metallic : hint_range(0.0, 1.0) = 0.0;
        uniform float bend_height : hint_range(0.05, 12.0) = 1.0;
        uniform float bend_strength : hint_range(0.0, 1.0) = 0.12;
        uniform float speed_multiplier : hint_range(0.05, 4.0) = 1.0;
        uniform float flutter_strength : hint_range(0.0, 1.0) = 0.04;
        uniform vec4 foliage_tint : source_color = vec4(1.0);
        uniform float foliage_tint_strength : hint_range(0.0, 1.0) = 0.0;
        uniform float forge_surface_wetness : hint_range(0.0, 1.0) = 0.0;
        uniform float forge_wetness_albedo_darkening : hint_range(0.0, 0.35) = 0.14;
        uniform float forge_wetness_roughness_reduction : hint_range(0.0, 0.8) = 0.36;
        uniform float forge_wetness_specular_increase : hint_range(0.0, 0.6) = 0.2;

        void vertex() {
            vec2 direction = length(gnd_wind_direction) > 0.01
                ? normalize(gnd_wind_direction)
                : normalize(vec2(0.82, 0.28));
            vec2 perpendicular = vec2(-direction.y, direction.x);
            vec3 world_origin = MODEL_MATRIX[3].xyz;
            float height_mask = smoothstep(0.015, max(bend_height, 0.05), max(VERTEX.y, 0.0));
            height_mask *= height_mask;

            float speed = max(gnd_wind_speed, 0.1) * speed_multiplier;
            float spatial_phase = dot(world_origin.xz, vec2(0.071, 0.113));
            float gust = sin(TIME * speed * 0.36 + spatial_phase) * 0.5 + 0.5;
            gust = mix(0.56, 1.0, gust * gust);
            float broad_wave = sin(TIME * speed * 0.62 + spatial_phase + VERTEX.y * 0.31);
            float flutter = sin(TIME * speed * 1.45 + spatial_phase * 1.91 + VERTEX.x * 2.7);
            float amplitude = bend_strength * (0.48 + gnd_wind_strength * 0.48) * gust;

            VERTEX.xz += direction * broad_wave * amplitude * height_mask;
            VERTEX.xz += perpendicular * flutter * flutter_strength * height_mask;
        }

        void fragment() {
            vec4 sampled = texture(albedo_texture, UV) * albedo_tint;
            vec3 vegetation_color = sampled.rgb * COLOR.rgb;
            float source_luminance = dot(vegetation_color, vec3(0.2126, 0.7152, 0.0722));
            float tinted_luminance = clamp(source_luminance * 1.12 + 0.05, 0.12, 0.90);
            vec3 stylized_tint = foliage_tint.rgb * tinted_luminance;
            vegetation_color = mix(vegetation_color, stylized_tint, foliage_tint_strength);
            float wetness = clamp(forge_surface_wetness, 0.0, 1.0);
            float wet_albedo_scale = 1.0 - forge_wetness_albedo_darkening * wetness;
            ALBEDO = vegetation_color * wet_albedo_scale;
            ALPHA = sampled.a;
            EMISSION = vegetation_color * 0.025;
            ROUGHNESS = clamp(material_roughness - forge_wetness_roughness_reduction * wetness, 0.08, 1.0);
            METALLIC = material_metallic;
            SPECULAR = clamp(0.24 + forge_wetness_specular_increase * wetness, 0.0, 1.0);
        }
        """;

    public static GodotBiomeScatterResult Build(
        AreaWorldProfile world,
        Func<string, Mesh?>? meshResolver,
        IReadOnlyList<WaterBodyProfile>? waterBodies = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        var started = Stopwatch.GetTimestamp();
        var root = new Node3D { Name = "BiomeScatter" };
        if (!world.Terrain.Enabled || meshResolver is null)
            return new(root, 0, 0, 0, 0, 0, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                0, 0, 0, 0, 0, 0);

        var layers = 0;
        var instances = 0;
        var windAnimatedLayers = 0;
        var collisionShapes = 0;
        var cullingGroups = 0;
        var collisionMilliseconds = 0d;
        var meshResolutionMilliseconds = 0d;
        var meshPreparationMilliseconds = 0d;
        var placementMilliseconds = 0d;
        var cellPlanningMilliseconds = 0d;
        var instanceUploadMilliseconds = 0d;
        var scatterPreparationStarted = Stopwatch.GetTimestamp();
        var scatterBuild = BiomeScatterPlanBuilder.Prepare(world);
        placementMilliseconds += Stopwatch.GetElapsedTime(scatterPreparationStarted).TotalMilliseconds;
        foreach (var biome in world.Biomes.Where(candidate => !string.IsNullOrWhiteSpace(candidate.ScatterResource)))
        {
            var meshResolutionStarted = Stopwatch.GetTimestamp();
            var mesh = meshResolver(biome.ScatterResource);
            meshResolutionMilliseconds += Stopwatch.GetElapsedTime(meshResolutionStarted).TotalMilliseconds;
            if (mesh is null) continue;
            var meshPreparationStarted = Stopwatch.GetTimestamp();
            var renderMesh = PrepareScatterMesh(mesh, biome);
            meshPreparationMilliseconds += Stopwatch.GetElapsedTime(meshPreparationStarted).TotalMilliseconds;
            var placementStarted = Stopwatch.GetTimestamp();
            var placements = scatterBuild.Build(biome).Placements
                .Where(placement => !IsInsideWaterBody(placement.Position.X, placement.Position.Z, waterBodies))
                .ToList();
            placementMilliseconds += Stopwatch.GetElapsedTime(placementStarted).TotalMilliseconds;
            if (placements.Count == 0) continue;
            var policy = BiomeScatterRenderingPolicy.For(biome);
            var cellPlanningStarted = Stopwatch.GetTimestamp();
            var cells = BiomeScatterCellPlanBuilder.Build(world.Terrain, placements, policy.CellSize);
            cellPlanningMilliseconds += Stopwatch.GetElapsedTime(cellPlanningStarted).TotalMilliseconds;
            var instanceUploadStarted = Stopwatch.GetTimestamp();
            foreach (var cell in cells)
            {
                var multiMesh = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                    Mesh = renderMesh,
                    InstanceCount = cell.Placements.Count,
                };
                for (var index = 0; index < cell.Placements.Count; index++)
                {
                    var placement = cell.Placements[index];
                    var basis = Basis.FromEuler(new Vector3(0, placement.YawRadians, 0))
                        .Scaled(Vector3.One * placement.UniformScale);
                    multiMesh.SetInstanceTransform(index, new Transform3D(
                        basis,
                        new Vector3(
                            placement.Position.X - cell.Origin.X,
                            placement.Position.Y,
                            placement.Position.Z - cell.Origin.Z)));
                }
                root.AddChild(new MultiMeshInstance3D
                {
                    Name = $"Scatter_{biome.Id:N}_{cell.CellX}_{cell.CellZ}",
                    Position = new Vector3(cell.Origin.X, 0, cell.Origin.Z),
                    Multimesh = multiMesh,
                    VisibilityRangeEnd = policy.VisibilityRangeEnd,
                    VisibilityRangeEndMargin = 8,
                    CastShadow = policy.CastShadows
                        ? GeometryInstance3D.ShadowCastingSetting.On
                        : GeometryInstance3D.ShadowCastingSetting.Off,
                });
            }
            instanceUploadMilliseconds += Stopwatch.GetElapsedTime(instanceUploadStarted).TotalMilliseconds;
            layers++;
            if (biome.Tags.Contains(WindAnimatedTag, StringComparer.OrdinalIgnoreCase)) windAnimatedLayers++;
            instances += placements.Count;
            cullingGroups += cells.Count;

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

        root.SetMeta("forge_wind_animated_layers", windAnimatedLayers);
        return new(root, layers, instances, windAnimatedLayers, collisionShapes, cullingGroups,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds, collisionMilliseconds,
            meshResolutionMilliseconds, meshPreparationMilliseconds, placementMilliseconds,
            cellPlanningMilliseconds, instanceUploadMilliseconds);
    }

    private static Mesh PrepareScatterMesh(Mesh source, BiomeLayerDefinition biome)
    {
        if (!biome.Tags.Contains(WindAnimatedTag, StringComparer.OrdinalIgnoreCase)) return source;
        var animated = source.Duplicate() as Mesh
            ?? throw new InvalidOperationException("A scatter mesh could not be duplicated for wind animation.");
        animated.SetMeta(GodotSurfaceWetnessAdapter.LocalResourceMeta, true);
        for (var surface = 0; surface < animated.GetSurfaceCount(); surface++)
        {
            var sourceMaterial = source.SurfaceGetMaterial(surface) as BaseMaterial3D;
            var shader = new Shader
            {
                ResourceName = "ForgeWindVegetation",
                Code = WindVegetationShaderCode,
            };
            var material = new ShaderMaterial
            {
                ResourceName = "ForgeWindVegetationMaterial",
                Shader = shader,
            };
            material.SetShaderParameter("albedo_tint", sourceMaterial?.AlbedoColor ?? Colors.White);
            if (sourceMaterial?.AlbedoTexture is { } albedoTexture)
                material.SetShaderParameter("albedo_texture", albedoTexture);
            material.SetShaderParameter("material_roughness", sourceMaterial?.Roughness ?? .82f);
            material.SetShaderParameter("material_metallic", sourceMaterial?.Metallic ?? 0f);
            material.SetShaderParameter("bend_height", BendHeight(biome));
            material.SetShaderParameter("bend_strength", BendStrength(biome));
            material.SetShaderParameter("speed_multiplier", SpeedMultiplier(biome));
            material.SetShaderParameter("flutter_strength", FlutterStrength(biome));
            var foliageTint = FoliageTint(biome, sourceMaterial);
            material.SetShaderParameter("foliage_tint", foliageTint.Color);
            material.SetShaderParameter("foliage_tint_strength", foliageTint.Strength);
            animated.SurfaceSetMaterial(surface, material);
        }
        return animated;
    }

    private static (Color Color, float Strength) FoliageTint(
        BiomeLayerDefinition biome,
        BaseMaterial3D? sourceMaterial)
    {
        const string colorPrefix = "foliage-tint:";
        const string strengthPrefix = "foliage-tint-strength:";
        var colorTag = biome.Tags.FirstOrDefault(candidate =>
            candidate.StartsWith(colorPrefix, StringComparison.OrdinalIgnoreCase));
        if (colorTag is null ||
            sourceMaterial?.ResourceName.Contains("bark", StringComparison.OrdinalIgnoreCase) == true ||
            sourceMaterial?.ResourceName.Contains("trunk", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (Colors.White, 0);
        }

        var strength = .5f;
        var strengthTag = biome.Tags.FirstOrDefault(candidate =>
            candidate.StartsWith(strengthPrefix, StringComparison.OrdinalIgnoreCase));
        if (strengthTag is not null && float.TryParse(
                strengthTag[strengthPrefix.Length..],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsedStrength) && float.IsFinite(parsedStrength))
        {
            strength = Math.Clamp(parsedStrength, 0, 1);
        }

        return (Color.FromString(colorTag[colorPrefix.Length..], Colors.White), strength);
    }

    private static float BendHeight(BiomeLayerDefinition biome) =>
        biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase) ? 5.5f :
        biome.Tags.Contains("shrub", StringComparer.OrdinalIgnoreCase) ? 1.4f : .72f;

    private static float BendStrength(BiomeLayerDefinition biome) =>
        biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase) ? .10f :
        biome.Tags.Contains("shrub", StringComparer.OrdinalIgnoreCase) ? .08f : .11f;

    private static float SpeedMultiplier(BiomeLayerDefinition biome) =>
        biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase) ? .34f :
        biome.Tags.Contains("shrub", StringComparer.OrdinalIgnoreCase) ? .45f : .52f;

    private static float FlutterStrength(BiomeLayerDefinition biome) =>
        biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase) ? .012f :
        biome.Tags.Contains("shrub", StringComparer.OrdinalIgnoreCase) ? .018f : .025f;

    private static bool IsInsideWaterBody(float x, float z, IReadOnlyList<WaterBodyProfile>? waterBodies)
    {
        if (waterBodies is null || waterBodies.Count == 0) return false;
        const float shorelineClearance = 2.5f;
        return waterBodies.Any(body =>
            MathF.Abs(x - body.Center.X) <= (body.Size.X * .5f) + shorelineClearance &&
            MathF.Abs(z - body.Center.Z) <= (body.Size.Z * .5f) + shorelineClearance);
    }

}
