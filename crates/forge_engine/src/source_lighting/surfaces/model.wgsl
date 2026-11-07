#import bevy_pbr::{
    forward_io::{VertexOutput, FragmentOutput},
    pbr_fragment::pbr_input_from_standard_material,
    pbr_functions::{alpha_discard, main_pass_post_lighting_processing},
}
#ifdef VISIBILITY_RANGE_DITHER
#import bevy_pbr::pbr_functions::visibility_range_dither
#endif
@fragment
fn fragment(in:VertexOutput,@builtin(front_facing) front:bool)->FragmentOutput {
#ifdef VISIBILITY_RANGE_DITHER
    visibility_range_dither(in.position,in.visibility_range_dither);
#endif
    var input=pbr_input_from_standard_material(in,front);
    input.material.base_color=alpha_discard(input.material,input.material.base_color);
    var out:FragmentOutput;
    out.color=sourceSurfaceLighting(input);
    out.color=main_pass_post_lighting_processing(input,out.color);
    return out;
}
