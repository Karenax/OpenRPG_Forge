#import bevy_pbr::{
    forward_io::{VertexOutput, FragmentOutput},
    pbr_fragment::pbr_input_from_standard_material,
    pbr_functions::{alpha_discard, apply_pbr_lighting, main_pass_post_lighting_processing},
    decal::clustered::apply_decals,
}
#import forge_source_decal::composition as source_decal

#ifdef VISIBILITY_RANGE_DITHER
#import bevy_pbr::pbr_functions::visibility_range_dither;
#endif

@group(#{MATERIAL_BIND_GROUP}) @binding(100) var dbuffer_color: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(101) var dbuffer_normal: texture_2d<f32>;

// Only opaque, lit forward materials may opt in through SourceDecalReceiver.
@fragment
fn fragment(in: VertexOutput, @builtin(front_facing) front: bool) -> FragmentOutput {
#ifdef VISIBILITY_RANGE_DITHER
    visibility_range_dither(in.position, in.visibility_range_dither);
#endif
    var p = pbr_input_from_standard_material(in, front);
    p.material.base_color = alpha_discard(p.material, p.material.base_color);
    apply_decals(&p);
    let pixel = vec2<i32>(in.position.xy);
    source_decal::apply(&p, textureLoad(dbuffer_color, pixel, 0),
        textureLoad(dbuffer_normal, pixel, 0));
    var out: FragmentOutput;
    out.color = main_pass_post_lighting_processing(p, apply_pbr_lighting(p));
    return out;
}
