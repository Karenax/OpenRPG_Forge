using Forge.Domain.Areas;
using Godot;

namespace Forge.EngineAdapter.World;

public sealed record TerrainMaterialDescriptor(
    string Mode,
    int LayerCount,
    bool UsesVertexBlend,
    bool UsesMacroVariation,
    bool PhysicallyBased);

public sealed record GodotTerrainMaterialResult(
    Material Material,
    TerrainMaterialDescriptor Descriptor,
    int ResolvedTextureCount);

public static class GodotTerrainMaterialAdapter
{
    public const string ProceduralPbrMode = "procedural-pbr-vertex-blend";

    public const string ProceduralRoadShaderCode = """
        shader_type spatial;
        render_mode blend_mix, depth_prepass_alpha, cull_disabled, diffuse_burley, specular_schlick_ggx;

        uniform vec4 base_color : source_color = vec4(0.24, 0.19, 0.14, 1.0);
        uniform sampler2D surface_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform bool use_surface_texture = false;
        uniform float surface_uv_scale : hint_range(0.5, 32.0) = 4.0;
        uniform float forge_surface_wetness : hint_range(0.0, 1.0) = 0.0;
        uniform float forge_wetness_albedo_darkening : hint_range(0.0, 0.35) = 0.14;
        uniform float forge_wetness_roughness_reduction : hint_range(0.0, 0.8) = 0.36;
        uniform float forge_wetness_specular_increase : hint_range(0.0, 0.6) = 0.2;
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
            vec3 dry_earth = base_color.rgb * vec3(1.15, 1.02, 0.86);
            vec3 damp_earth = base_color.rgb * vec3(0.78, 0.80, 0.76);
            vec3 color = mix(damp_earth, dry_earth, broad * 0.65 + worn_center * 0.35);
            color *= 0.91 + (grain - 0.5) * 0.12;
            if (use_surface_texture) {
                vec3 surface = texture(surface_texture, world_position.xz / surface_uv_scale).rgb;
                float surface_luminance = max(dot(surface, vec3(0.2126, 0.7152, 0.0722)), 0.08);
                vec3 textured = mix(vec3(surface_luminance), surface, 0.78);
                textured *= vec3(1.06, 1.08, 1.14);
                color = mix(color, textured, 0.94);
            }
            float left_rut = 1.0 - smoothstep(0.05, 0.16, abs(UV.x - 0.32));
            float right_rut = 1.0 - smoothstep(0.05, 0.16, abs(UV.x - 0.68));
            color *= 1.0 - max(left_rut, right_rut) * 0.075;
            float wetness = clamp(forge_surface_wetness, 0.0, 1.0);
            float wet_albedo_scale = 1.0 - forge_wetness_albedo_darkening * wetness;
            ALBEDO = clamp(color * wet_albedo_scale, vec3(0.045), vec3(0.90));
            EMISSION = ALBEDO * 0.65;
            float edge = abs(UV.x - 0.5) * 2.0;
            float edge_noise = (forge_road_noise(world_position.xz * 0.62 + vec2(5.0, 17.0)) - 0.5) * 0.13;
            ALPHA = 1.0 - smoothstep(0.72 + edge_noise, 1.0 + edge_noise, edge);
            ROUGHNESS = clamp(0.92 - broad * 0.13 - forge_wetness_roughness_reduction * wetness, 0.08, 0.95);
            METALLIC = 0.0;
            SPECULAR = clamp(0.18 + forge_wetness_specular_increase * wetness, 0.0, 1.0);
            AO = 0.82 + broad * 0.16;
            AO_LIGHT_AFFECT = 0.68;
        }
        """;

    public const string ProceduralPbrShaderCode = """
        shader_type spatial;
        render_mode depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

        varying vec3 world_position;
        varying vec3 world_normal;
        uniform sampler2D surface_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform bool use_surface_texture = false;
        uniform float surface_uv_scale : hint_range(0.5, 32.0) = 4.5;
        uniform sampler2D secondary_surface_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform bool use_secondary_surface_texture = false;
        uniform float secondary_surface_uv_scale : hint_range(0.5, 32.0) = 4.0;
        uniform float forge_surface_wetness : hint_range(0.0, 1.0) = 0.0;
        uniform float forge_wetness_albedo_darkening : hint_range(0.0, 0.35) = 0.14;
        uniform float forge_wetness_roughness_reduction : hint_range(0.0, 0.8) = 0.36;
        uniform float forge_wetness_specular_increase : hint_range(0.0, 0.6) = 0.2;

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
            float raw_slope = clamp(1.0 - abs(world_normal.y), 0.0, 1.0);
            float slope = pow(raw_slope, 1.35);
            float damp = smoothstep(0.30, 0.78, forge_noise(world_position.xz * 0.012 + vec2(23.0, 41.0)));

            vec3 base = COLOR.rgb;
            vec3 cool_variation = base * vec3(0.82, 1.08, 0.79);
            vec3 warm_variation = base * vec3(1.10, 0.94, 0.76);
            vec3 rock = mix(base, vec3(dot(base, vec3(0.333))), 0.28) * 0.94;
            vec3 layered = mix(warm_variation, cool_variation,
                clamp(macro * 0.70 + damp * 0.20 + 0.10, 0.0, 1.0));
            layered = mix(layered, rock, slope * 0.52);
            layered *= 0.88 + macro * 0.17 + (detail - 0.5) * 0.08;
            float leaf_pattern = forge_noise(world_position.xz * 1.7 + vec2(7.0, 31.0));
            float leaf_mask = smoothstep(0.86, 0.97, leaf_pattern) * (1.0 - slope);
            layered = mix(layered, base * vec3(1.12, 0.72, 0.48), leaf_mask * 0.14);
            float grass_mask = smoothstep(0.015, 0.16, base.g - max(base.r, base.b)) * (1.0 - slope);
            float soil_mask = smoothstep(0.025, 0.15, base.r - base.g) * (1.0 - slope * 0.72);
            if (use_surface_texture) {
                mat2 surface_rotation = mat2(vec2(0.80, -0.60), vec2(0.60, 0.80));
                vec2 surface_uv = world_position.xz / surface_uv_scale;
                vec3 surface_fine = texture(surface_texture, surface_uv).rgb;
                vec3 surface_rotated = texture(
                    surface_texture,
                    surface_rotation * surface_uv * 0.73 + vec2(31.7, 11.3)).rgb;
                vec3 surface_large = texture(
                    surface_texture,
                    surface_rotation * surface_uv * 0.31 + vec2(7.1, 43.9)).rgb;
                float surface_breakup = forge_noise(world_position.xz * 0.021 + vec2(13.0, 29.0));
                vec3 surface = mix(surface_fine, surface_rotated, 0.30 + surface_breakup * 0.24);
                surface = mix(surface, surface_large, 0.16);
                float surface_luminance = max(dot(surface, vec3(0.2126, 0.7152, 0.0722)), 0.08);
                vec3 surface_chroma = clamp(surface / surface_luminance, vec3(0.58), vec3(1.42));
                vec3 stylized_surface = mix(surface, base * surface_chroma, 0.06);
                stylized_surface *= 0.96 + surface_luminance * 0.10;
                // A production terrain must remain visibly textured even when a coarse
                // heightfield makes a broad, locally walkable cell inherit a rock biome.
                // The previous grass-only mask could therefore turn an entire generated
                // village plateau into an untextured grey surface. Keep the biome colour
                // direction, but guarantee a real texture contribution on every surface.
                float surface_weight = clamp(
                    0.64 + grass_mask * 0.32 - raw_slope * 0.18,
                    0.48,
                    0.96);
                layered = mix(layered, stylized_surface, surface_weight);
            }
            if (use_secondary_surface_texture) {
                mat2 secondary_rotation = mat2(vec2(0.64, -0.77), vec2(0.77, 0.64));
                vec2 secondary_uv = world_position.xz / secondary_surface_uv_scale;
                vec3 secondary_fine = texture(
                    secondary_surface_texture,
                    secondary_uv).rgb;
                vec3 secondary_rotated = texture(
                    secondary_surface_texture,
                    secondary_rotation * secondary_uv * 0.67 + vec2(19.4, 37.2)).rgb;
                vec3 secondary_large = texture(
                    secondary_surface_texture,
                    secondary_rotation * secondary_uv * 0.29 + vec2(47.3, 5.8)).rgb;
                float secondary_breakup = forge_noise(world_position.xz * 0.017 + vec2(41.0, 3.0));
                vec3 secondary = mix(
                    secondary_fine,
                    secondary_rotated,
                    0.28 + secondary_breakup * 0.22);
                secondary = mix(secondary, secondary_large, 0.14);
                float secondary_luminance = dot(secondary, vec3(0.2126, 0.7152, 0.0722));
                vec3 stylized_secondary = mix(vec3(secondary_luminance), secondary, 0.82);
                stylized_secondary *= vec3(0.96, 0.98, 1.04);
                float secondary_weight = clamp(
                    soil_mask * 0.82 + raw_slope * 1.10,
                    0.0,
                    0.78);
                layered = mix(layered, stylized_secondary, secondary_weight);
            }
            float luminance = dot(layered, vec3(0.2126, 0.7152, 0.0722));
            layered = mix(vec3(luminance), layered, 0.99);

            float wetness = clamp(forge_surface_wetness, 0.0, 1.0);
            float wet_albedo_scale = 1.0 - forge_wetness_albedo_darkening * wetness;
            ALBEDO = clamp(layered * wet_albedo_scale, vec3(0.025), vec3(0.84));
            ROUGHNESS = clamp(mix(0.94, 0.70, macro) + slope * 0.035 -
                forge_wetness_roughness_reduction * wetness, 0.08, 0.96);
            METALLIC = 0.012;
            SPECULAR = clamp(0.24 + forge_wetness_specular_increase * wetness, 0.0, 1.0);
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

    public static GodotTerrainMaterialResult Build(
        TerrainDefinition terrain,
        Func<string, Texture2D?>? textureResolver = null)
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
        var texturedLayer = terrain.MaterialLayers.FirstOrDefault(layer =>
            !string.IsNullOrWhiteSpace(layer.MaterialResource));
        var secondaryTexturedLayer = terrain.MaterialLayers.SkipWhile(layer => layer != texturedLayer)
            .Skip(texturedLayer is null ? 0 : 1)
            .FirstOrDefault(layer => !string.IsNullOrWhiteSpace(layer.MaterialResource));
        var surfaceTexture = texturedLayer is null || textureResolver is null
            ? null
            : textureResolver(texturedLayer.MaterialResource);
        var secondarySurfaceTexture = secondaryTexturedLayer is null || textureResolver is null
            ? null
            : textureResolver(secondaryTexturedLayer.MaterialResource);
        material.SetShaderParameter("use_surface_texture", surfaceTexture is not null);
        if (surfaceTexture is not null)
        {
            material.SetShaderParameter("surface_texture", surfaceTexture);
            material.SetShaderParameter("surface_uv_scale", texturedLayer!.UvScale);
        }
        material.SetShaderParameter("use_secondary_surface_texture", secondarySurfaceTexture is not null);
        if (secondarySurfaceTexture is not null)
        {
            material.SetShaderParameter("secondary_surface_texture", secondarySurfaceTexture);
            material.SetShaderParameter("secondary_surface_uv_scale", secondaryTexturedLayer!.UvScale);
        }
        return new GodotTerrainMaterialResult(
            material,
            descriptor,
            (surfaceTexture is null ? 0 : 1) + (secondarySurfaceTexture is null ? 0 : 1));
    }

    public static Material BuildRoad(Color baseColor, Texture2D? surfaceTexture = null, float surfaceUvScale = 4)
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
        material.SetShaderParameter("use_surface_texture", surfaceTexture is not null);
        if (surfaceTexture is not null)
        {
            material.SetShaderParameter("surface_texture", surfaceTexture);
            material.SetShaderParameter("surface_uv_scale", surfaceUvScale);
        }
        return material;
    }
}
