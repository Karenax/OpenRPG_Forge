#import bevy_pbr::{
    pbr_fragment::pbr_input_from_standard_material,
    pbr_functions::alpha_discard,
}

#ifdef PREPASS_PIPELINE
#import bevy_pbr::{
    prepass_io::{VertexOutput, FragmentOutput},
    pbr_deferred_functions::deferred_output,
}
#else
#import bevy_pbr::{
    forward_io::{VertexOutput, FragmentOutput},
    pbr_functions::{apply_pbr_lighting, main_pass_post_lighting_processing},
}
#endif

// Forge-original procedural palettes. COLOR.rgb carries the normalized logical
// grass, earth and rock weights generated from the canonical terrain document.
struct VisualValleyTerrainExtension {
    grass_color_roughness: vec4<f32>,
    earth_color_roughness: vec4<f32>,
    rock_color_roughness: vec4<f32>,
    grass_metallic_reflectance: vec4<f32>,
    earth_metallic_reflectance: vec4<f32>,
    rock_metallic_reflectance: vec4<f32>,
    wetness_parameters: vec4<f32>,
}

@group(#{MATERIAL_BIND_GROUP}) @binding(100)
var<uniform> visual_valley: VisualValleyTerrainExtension;

fn normalized_material_weights(raw_weights: vec3<f32>) -> vec3<f32> {
    let non_negative = max(raw_weights, vec3(0.0));
    let total = dot(non_negative, vec3(1.0));
    if total <= 0.000001 {
        return vec3(1.0, 0.0, 0.0);
    }
    return non_negative / total;
}

@fragment
fn fragment(
    in: VertexOutput,
    @builtin(front_facing) is_front: bool,
) -> FragmentOutput {
    var pbr_input = pbr_input_from_standard_material(in, is_front);

#ifdef VERTEX_COLORS
    let weights = normalized_material_weights(in.color.rgb);
#else
    let weights = vec3(1.0, 0.0, 0.0);
#endif

    let base_color =
        visual_valley.grass_color_roughness.rgb * weights.x
        + visual_valley.earth_color_roughness.rgb * weights.y
        + visual_valley.rock_color_roughness.rgb * weights.z;
    let perceptual_roughness = dot(
        vec3(
            visual_valley.grass_color_roughness.a,
            visual_valley.earth_color_roughness.a,
            visual_valley.rock_color_roughness.a,
        ),
        weights,
    );
    let metallic = dot(
        vec3(
            visual_valley.grass_metallic_reflectance.x,
            visual_valley.earth_metallic_reflectance.x,
            visual_valley.rock_metallic_reflectance.x,
        ),
        weights,
    );
    let reflectance = dot(
        vec3(
            visual_valley.grass_metallic_reflectance.y,
            visual_valley.earth_metallic_reflectance.y,
            visual_valley.rock_metallic_reflectance.y,
        ),
        weights,
    );

    let wetness = clamp(visual_valley.wetness_parameters.x, 0.0, 1.0);
    let wet_albedo_multiplier = mix(
        1.0,
        visual_valley.wetness_parameters.z,
        wetness,
    );
    let wet_roughness_multiplier = mix(
        1.0,
        visual_valley.wetness_parameters.y,
        wetness,
    );
    pbr_input.material.base_color = vec4(base_color * wet_albedo_multiplier, 1.0);
    pbr_input.material.perceptual_roughness = clamp(
        perceptual_roughness * wet_roughness_multiplier,
        0.04,
        1.0,
    );
    pbr_input.material.metallic = clamp(metallic, 0.0, 1.0);
    pbr_input.material.reflectance = vec3(clamp(reflectance, 0.0, 1.0));
    pbr_input.material.base_color = alpha_discard(
        pbr_input.material,
        pbr_input.material.base_color,
    );

#ifdef PREPASS_PIPELINE
    let out = deferred_output(in, pbr_input);
#else
    var out: FragmentOutput;
    out.color = apply_pbr_lighting(pbr_input);
    out.color = main_pass_post_lighting_processing(pbr_input, out.color);
#endif

    return out;
}
