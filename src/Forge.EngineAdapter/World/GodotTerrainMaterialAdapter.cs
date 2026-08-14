using Forge.Domain.Areas;
using Godot;

namespace Forge.EngineAdapter.World;

public sealed record TerrainMaterialDescriptor(
    string Mode,
    int LayerCount,
    bool UsesVertexBlend,
    bool UsesMacroVariation,
    bool PhysicallyBased);

public sealed record GodotTerrainMaterialResult(Material Material, TerrainMaterialDescriptor Descriptor);

public static class GodotTerrainMaterialAdapter
{
    public const string ProceduralPbrMode = "procedural-pbr-vertex-blend";

    public const string ProceduralRoadShaderCode = """
        shader_type spatial;
        render_mode depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

        uniform vec4 base_color : source_color = vec4(0.24, 0.19, 0.14, 1.0);
        varying vec3 world_position;

        float forge_road_hash(vec2 value) {
            return fract(sin(dot(value, vec2(93.1, 271.9))) * 43758.5453123);
        }

        float forge_road_noise(vec2 value) {
            vec2 cell = floor(value);
            vec2 local = fract(value);
            vec2 blend = local * local * (3.0 - 2.0 * local);
            float a = forge_road_hash(cell);
            float b = forge_road_hash(cell + vec2(1.0, 0.0));
            float c = forge_road_hash(cell + vec2(0.0, 1.0));
            float d = forge_road_hash(cell + vec2(1.0, 1.0));
            return mix(mix(a, b, blend.x), mix(c, d, blend.x), blend.y);
        }

        void vertex() {
            world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
        }

        void fragment() {
            float broad = forge_road_noise(world_position.xz * 0.18);
            float grain = forge_road_noise(world_position.xz * 1.15 + vec2(19.0, 7.0));
            float worn_center = smoothstep(0.0, 0.38, 0.5 - abs(UV.x - 0.5));
            vec3 dry_earth = base_color.rgb * vec3(1.08, 0.91, 0.72);
            vec3 damp_earth = base_color.rgb * vec3(0.62, 0.70, 0.68);
            vec3 color = mix(damp_earth, dry_earth, broad * 0.72 + worn_center * 0.28);
            color *= 0.82 + (grain - 0.5) * 0.18;
            ALBEDO = clamp(color, vec3(0.015), vec3(0.70));
            ROUGHNESS = clamp(0.92 - broad * 0.13, 0.72, 0.95);
            METALLIC = 0.0;
            SPECULAR = 0.18;
            EMISSION = color * 1.05;
            AO = 0.82 + broad * 0.16;
            AO_LIGHT_AFFECT = 0.68;
        }
        """;

    public const string ProceduralPbrShaderCode = """
        shader_type spatial;
        render_mode depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

        varying vec3 world_position;
        varying vec3 world_normal;

        float forge_hash(vec2 value) {
            return fract(sin(dot(value, vec2(127.1, 311.7))) * 43758.5453123);
        }

        float forge_noise(vec2 value) {
            vec2 cell = floor(value);
            vec2 local = fract(value);
            vec2 blend = local * local * (3.0 - 2.0 * local);
            float a = forge_hash(cell);
            float b = forge_hash(cell + vec2(1.0, 0.0));
            float c = forge_hash(cell + vec2(0.0, 1.0));
            float d = forge_hash(cell + vec2(1.0, 1.0));
            return mix(mix(a, b, blend.x), mix(c, d, blend.x), blend.y);
        }

        float forge_fbm(vec2 value) {
            float result = 0.0;
            float amplitude = 0.56;
            mat2 rotation = mat2(vec2(0.80, -0.60), vec2(0.60, 0.80));
            for (int octave = 0; octave < 4; octave++) {
                result += forge_noise(value) * amplitude;
                value = rotation * value * 2.03 + vec2(17.0, 9.0);
                amplitude *= 0.48;
            }
            return result;
        }

        void vertex() {
            world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
            world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
        }

        void fragment() {
            float macro = forge_fbm(world_position.xz * 0.035);
            float detail = forge_noise(world_position.xz * 0.31);
            float slope = pow(clamp(1.0 - abs(world_normal.y), 0.0, 1.0), 1.35);
            float damp = smoothstep(0.30, 0.78, forge_noise(world_position.xz * 0.012 + vec2(23.0, 41.0)));

            vec3 base = COLOR.rgb;
            vec3 moss = mix(base, vec3(0.050, 0.205, 0.040), 0.42) * vec3(0.78, 1.02, 0.72);
            vec3 earth = mix(base, vec3(0.155, 0.080, 0.032), 0.35) * vec3(0.84, 0.75, 0.62);
            vec3 rock = mix(base, vec3(dot(base, vec3(0.333))), 0.34) * 0.96;
            vec3 layered = mix(earth, moss, clamp(macro * 0.72 + damp * 0.28, 0.0, 1.0));
            layered = mix(layered, rock, slope * 0.58);
            layered *= 0.74 + macro * 0.22 + (detail - 0.5) * 0.11;
            float leaf_pattern = forge_noise(world_position.xz * 1.7 + vec2(7.0, 31.0));
            float leaf_mask = smoothstep(0.86, 0.97, leaf_pattern) * (1.0 - slope);
            layered = mix(layered, vec3(0.29, 0.105, 0.025), leaf_mask * 0.30);

            ALBEDO = clamp(layered, vec3(0.018), vec3(0.92));
            ROUGHNESS = clamp(mix(0.94, 0.70, macro) + slope * 0.035, 0.62, 0.96);
            METALLIC = 0.012;
            SPECULAR = 0.24;
            AO = clamp(0.76 + macro * 0.23 - slope * 0.06, 0.62, 1.0);
            AO_LIGHT_AFFECT = 0.72;
        }
        """;

    public static TerrainMaterialDescriptor Describe(TerrainDefinition terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        return new TerrainMaterialDescriptor(
            terrain.Enabled ? ProceduralPbrMode : "disabled",
            terrain.MaterialLayers.Count,
            terrain.Enabled,
            terrain.Enabled,
            terrain.Enabled);
    }

    public static GodotTerrainMaterialResult Build(TerrainDefinition terrain)
    {
        var descriptor = Describe(terrain);
        var shader = new Shader
        {
            ResourceName = "ForgeTerrainProceduralPbr",
            Code = ProceduralPbrShaderCode,
        };
        var material = new ShaderMaterial
        {
            ResourceName = "ForgeTerrainProceduralPbrMaterial",
            Shader = shader,
        };
        return new GodotTerrainMaterialResult(material, descriptor);
    }

    public static Material BuildRoad(Color baseColor)
    {
        var shader = new Shader
        {
            ResourceName = "ForgeTerrainRoadPbr",
            Code = ProceduralRoadShaderCode,
        };
        var material = new ShaderMaterial
        {
            ResourceName = "ForgeTerrainRoadPbrMaterial",
            Shader = shader,
        };
        material.SetShaderParameter("base_color", baseColor);
        return material;
    }
}
