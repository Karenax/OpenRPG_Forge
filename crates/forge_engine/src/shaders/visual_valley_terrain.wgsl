#import "embedded://forge_engine/shaders/terrain_palette.wgsl"::{VisualValleyTerrainExtension, normalized_material_weights, authored_terrain_surface}
#import bevy_pbr::{
    pbr_fragment::pbr_input_from_standard_material,
    pbr_functions::alpha_discard,
}

#ifdef VISIBILITY_RANGE_DITHER
#import bevy_pbr::pbr_functions::visibility_range_dither;
#endif

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
@group(#{MATERIAL_BIND_GROUP}) @binding(100)
var<uniform> visual_valley: VisualValleyTerrainExtension;

@group(#{MATERIAL_BIND_GROUP}) @binding(101)
var terrain_detail: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(102)
var terrain_detail_sampler: sampler;

@fragment
fn fragment(
    in: VertexOutput,
    @builtin(front_facing) is_front: bool,
) -> FragmentOutput {
#ifdef VISIBILITY_RANGE_DITHER
    visibility_range_dither(in.position, in.visibility_range_dither);
#endif
    var pbr_input = pbr_input_from_standard_material(in, is_front);

#ifdef VERTEX_COLORS
    let weights = normalized_material_weights(in.color.rgb);
#else
    let weights = vec3(1.0, 0.0, 0.0);
#endif

    let authored = authored_terrain_surface(weights, in.world_position.xyz, pbr_input.world_normal,
        visual_valley, terrain_detail, terrain_detail_sampler);
    pbr_input.N = authored.normal;
    pbr_input.material.base_color = authored.base_color;
    pbr_input.material.perceptual_roughness = authored.perceptual_roughness;
    pbr_input.material.metallic = authored.metallic;
    pbr_input.material.reflectance = authored.reflectance;
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
