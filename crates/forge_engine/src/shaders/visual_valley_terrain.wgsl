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

// Forge-original ground detail: integer cell hashing and smooth interpolation,
// anchored in world metres so adjacent chunks share the same surface pattern.
fn terrain_cell_value(cell: vec2<i32>) -> f32 {
    let bits = bitcast<vec2<u32>>(cell);
    var value = bits.x * 1664525u + bits.y * 1013904223u + 374761393u;
    value = (value ^ (value >> 16u)) * 2246822519u;
    value = (value ^ (value >> 13u)) * 3266489917u;
    value = value ^ (value >> 16u);
    return f32(value & 0x00ffffffu) / 16777215.0;
}

fn terrain_value_noise(position: vec2<f32>) -> f32 {
    let cell = vec2<i32>(floor(position));
    let fraction = fract(position);
    let blend = fraction * fraction * (vec2(3.0) - 2.0 * fraction);
    let lower = mix(terrain_cell_value(cell), terrain_cell_value(cell + vec2(1, 0)), blend.x);
    let upper = mix(terrain_cell_value(cell + vec2(0, 1)), terrain_cell_value(cell + vec2(1, 1)), blend.x);
    return mix(lower, upper, blend.y);
}

fn terrain_surface_detail(world_xz: vec2<f32>) -> f32 {
    let footprint_m = max(length(dpdx(world_xz)), length(dpdy(world_xz)));
    // Roughly 3 m patches, 60 cm soil variation and 20 cm grain. Fade each
    // octave towards its mean as it becomes subpixel, not towards a darker soil.
    let patches = mix(0.5, terrain_value_noise(world_xz * 0.32),
        1.0 - smoothstep(0.8, 2.0, footprint_m));
    let soil = mix(0.5, terrain_value_noise(world_xz * 1.7 + vec2(17.0, 29.0)),
        1.0 - smoothstep(0.12, 0.45, footprint_m));
    let grain = mix(0.5, terrain_value_noise(world_xz * 4.8 + vec2(-23.0, 41.0)),
        1.0 - smoothstep(0.04, 0.14, footprint_m));
    return clamp(patches * 0.5 + soil * 0.35 + grain * 0.15 - 0.5, -0.5, 0.5);
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

    // Scalar detail preserves the authored palette and canonical layer blend.
    // The same dry surface feeds forward/deferred and remains independent of weather.
    let detail = terrain_surface_detail(in.world_position.xz);
    let dry_base_color = base_color * (1.0 + detail * 0.32);
    let dry_roughness = clamp(perceptual_roughness + detail * 0.09, 0.04, 1.0);
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
    pbr_input.material.base_color = vec4(dry_base_color * wet_albedo_multiplier, 1.0);
    pbr_input.material.perceptual_roughness = clamp(
        dry_roughness * wet_roughness_multiplier,
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
