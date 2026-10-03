#import bevy_pbr::{
    forward_io::{VertexOutput, FragmentOutput},
    pbr_fragment::pbr_input_from_standard_material,
    pbr_functions::{alpha_discard, main_pass_post_lighting_processing},
    mesh_view_bindings::view,
}
#import "embedded://forge_engine/shaders/source_pbr_lighting.wgsl"::apply_pbr_lighting
#import "embedded://forge_engine/shaders/source_fragment_common.wgsl"::{source, source_orm, source_orm_sampler, source_world_normal, source_world_color, source_surface_image_uv_from_target, source_base_color_power}

@fragment
fn fragment(in:VertexOutput, @builtin(front_facing) is_front:bool)->FragmentOutput {
    if source.world_control.y==1.0 && source.world_control.x!=1.0 { discard; }
    var surface_input=in;
#ifdef VERTEX_UVS_A
    surface_input.uv=source_surface_image_uv_from_target(in.uv);
#endif
    var p=pbr_input_from_standard_material(surface_input,is_front);
    p.material.base_color=alpha_discard(p.material,p.material.base_color);
    let rgb=vec4<f32>(p.material.base_color.rgb,1.0);
    p.material.base_color=vec4<f32>(dot(source.rgb_rows[0],rgb),dot(source.rgb_rows[1],rgb),dot(source.rgb_rows[2],rgb),p.material.base_color.a);
    if source.world_control.w==1.0 {
        p.material.base_color=vec4<f32>(source_base_color_power(p.material.base_color.rgb,source.world_params.y),p.material.base_color.a);
    }
#ifdef VERTEX_UVS_A
    if source.world_control.x==1.0 {
        p.material.base_color=vec4<f32>(source_world_color(p.material.base_color.rgb,in.uv,in.world_position.xyz,in.world_normal),p.material.base_color.a);
    }
    let packed=textureSampleBias(source_orm,source_orm_sampler,surface_input.uv,view.mip_bias);
    p.material.metallic=dot(source.metallic,packed)+source.scalar_offsets_normal_strength.x;
    p.diffuse_occlusion*=vec3<f32>(dot(source.occlusion,packed)+source.scalar_offsets_normal_strength.y);
    p.material.perceptual_roughness=dot(source.roughness,packed)+source.scalar_offsets_normal_strength.z;
#ifdef VERTEX_TANGENTS
    p.N=source_world_normal(in.uv,in.world_normal,in.world_tangent,is_front);
#endif
#endif
    var out:FragmentOutput;
    out.color=apply_pbr_lighting(p);
    out.color=main_pass_post_lighting_processing(p,out.color);
    return out;
}
