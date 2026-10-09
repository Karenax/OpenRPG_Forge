// Port of Unity's MIT-licensed Particle Alpha Blend.shader.
// The original shader and license are retained with the conversion receipt.
// Inputs are measured source values. This library does not supply camera,
// texture, particle-geometry, fog or depth defaults and is not render admission.

fn source_particle_alpha_uv(uv: vec2<f32>, source_maintex_st: vec4<f32>) -> vec2<f32> {
    return uv * source_maintex_st.xy + source_maintex_st.zw;
}

fn source_particle_alpha_vertex_color(
    source_vertex_color: vec4<f32>, source_tint: vec4<f32>
) -> vec4<f32> {
    return source_vertex_color * source_tint;
}

fn source_particle_alpha_surface(
    interpolated_source_color: vec4<f32>, sampled_source_texel: vec4<f32>,
    soft_particles_enabled: bool, source_inv_fade: f32,
    measured_scene_linear_eye_depth: f32, measured_particle_linear_eye_depth: f32,
    fog_enabled: bool, measured_source_fog_factor: f32, source_fog_color: vec3<f32>
) -> vec4<f32> {
    var color = interpolated_source_color;
    if soft_particles_enabled {
        let fade = clamp(source_inv_fade *
            (measured_scene_linear_eye_depth - measured_particle_linear_eye_depth), 0.0, 1.0);
        color.a *= fade;
    }
    var output = 2.0 * color * sampled_source_texel;
    // Preserve legacy doubled alpha followed by saturation. Do not "correct"
    // this to a single-brightness alpha or premultiply the source RGB.
    output.a = clamp(output.a, 0.0, 1.0);
    if fog_enabled {
        output = vec4<f32>(mix(source_fog_color, output.rgb,
            clamp(measured_source_fog_factor, 0.0, 1.0)), output.a);
    }
    return output;
}

// Required by the original ShaderLab pass, separately from the surface math:
// Blend SrcAlpha OneMinusSrcAlpha; ColorMask RGB; Cull Off; Lighting Off;
// ZWrite Off. Alpha in the color attachment must remain unmodified.
// A complete adapter must also qualify source billboard geometry, atlas UVs,
// depth projection, fog variants, transparent ordering and native sampling.
