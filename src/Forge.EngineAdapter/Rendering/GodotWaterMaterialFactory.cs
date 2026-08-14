using Forge.Domain.Areas;
using Godot;

namespace Forge.EngineAdapter.Rendering;

public static class GodotWaterMaterialFactory
{
    public static Material Create(Vector2 flowDirection, float flowSpeed, WaterQualityTier quality)
    {
        if (quality < WaterQualityTier.High)
        {
            return new StandardMaterial3D
            {
                AlbedoColor = new Color(.08f, .3f, .42f, .82f),
                Metallic = .18f,
                Roughness = .3f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
        }

        var shader = new Shader
        {
            Code = """
                shader_type spatial;
                render_mode blend_mix, depth_prepass_alpha, cull_disabled, diffuse_burley, specular_schlick_ggx;

                uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
                uniform vec4 shallow_color : source_color = vec4(0.06, 0.46, 0.58, 0.86);
                uniform vec4 deep_color : source_color = vec4(0.012, 0.11, 0.22, 0.92);
                uniform vec2 flow_direction = vec2(0.65, 0.25);
                uniform float flow_speed = 0.35;
                uniform float wave_height = 0.075;
                uniform float refraction_strength = 0.012;

                varying vec3 water_world_position;

                void vertex() {
                    float primary = sin(dot(VERTEX.xz, vec2(0.21, 0.16)) * 6.0 + TIME * flow_speed * 3.2);
                    float secondary = cos(dot(VERTEX.xz, vec2(-0.13, 0.27)) * 8.0 - TIME * flow_speed * 2.1);
                    VERTEX.y += (primary * 0.65 + secondary * 0.35) * wave_height;
                    water_world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
                }

                void fragment() {
                    float phase_a = dot(water_world_position.xz, normalize(flow_direction + vec2(0.001))) * 0.34 + TIME * flow_speed;
                    float phase_b = dot(water_world_position.xz, vec2(-flow_direction.y, flow_direction.x)) * 0.47 - TIME * flow_speed * 0.72;
                    vec2 ripple = vec2(sin(phase_a * 3.1), cos(phase_b * 2.7));
                    vec2 refracted_uv = clamp(SCREEN_UV + ripple * refraction_strength, vec2(0.002), vec2(0.998));
                    vec3 refracted_scene = textureLod(screen_texture, refracted_uv, 0.0).rgb;
                    float fresnel = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 4.0);
                    float crest = smoothstep(0.58, 1.0, sin(phase_a * 4.2 + phase_b) * 0.5 + 0.5);
                    vec3 water_tint = mix(deep_color.rgb, shallow_color.rgb, 0.48 + crest * 0.18);
                    vec3 sky_reflection = mix(vec3(0.34, 0.58, 0.74), vec3(0.72, 0.84, 0.91), crest * 0.35);
                    vec3 wave_normal = normalize(vec3(ripple.x * 0.28, ripple.y * 0.22, 1.0));
                    NORMAL_MAP = wave_normal * 0.5 + 0.5;
                    NORMAL_MAP_DEPTH = 0.72;
                    ALBEDO = mix(refracted_scene, water_tint, 0.43 + fresnel * 0.22);
                    ALBEDO = mix(ALBEDO, sky_reflection, fresnel * 0.58);
                    EMISSION = shallow_color.rgb * crest * 0.065 + sky_reflection * fresnel * 0.085;
                    METALLIC = 0.08;
                    ROUGHNESS = mix(0.055, 0.16, crest);
                    SPECULAR = 0.92;
                    ALPHA = mix(shallow_color.a, deep_color.a, fresnel * 0.52);
                }
                """,
        };
        var material = new ShaderMaterial { Shader = shader };
        var normalized = flowDirection.LengthSquared() <= .0001f ? new Vector2(.65f, .25f) : flowDirection.Normalized();
        material.SetShaderParameter("flow_direction", normalized);
        material.SetShaderParameter("flow_speed", Math.Clamp(flowSpeed, .08f, 2.4f));
        material.SetShaderParameter("wave_height", quality == WaterQualityTier.Ultra ? .11f : .065f);
        material.SetShaderParameter("refraction_strength", quality == WaterQualityTier.Ultra ? .016f : .01f);
        return material;
    }
}
