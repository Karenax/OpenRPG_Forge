// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 DRG Software Solutions
// World lighting adaptation; source geometry, surfaces and masks are above.
#import bevy_pbr::mesh_bindings::mesh
#ifdef PREPASS_PIPELINE
#ifdef PREPASS_FRAGMENT
#import bevy_pbr::prepass_io::FragmentOutput
#endif
#ifdef MOTION_VECTOR_PREPASS
#import bevy_pbr::prepass_bindings::previous_view_uniforms
#endif
#else
#import bevy_pbr::{pbr_types, mesh_types::MESH_FLAGS_TRANSMITTED_SHADOW_RECEIVER_BIT, pbr_functions::{apply_pbr_lighting, main_pass_post_lighting_processing, calculate_view}}
#endif

@vertex
fn vertex(v:ProbeVertex)->ProbeOut {
    setupSource();
    let tag=mesh_functions::get_tag(v.instance);
    let model=sourceModels[tag]; let record=sourceInstances[tag];
    var source:SourceVertex;
    source.position=v.position; source.normal=v.normal; source.uv=v.uv;
    source.model=model; source.iPos=record.iPos; source.iDat=record.iDat;
    source.aVeg=v.aVeg; source.aMat=v.aMat; source.aLobe=v.aLobe;
#ifdef PLANT_KIND_3
    source.iCell=record.iPos;
    source.aSide=v.aVeg; source.aSlot=v.aMat; source.aBlade=v.aLobe;
#endif
#ifdef MOTION_VECTOR_PREPASS
    var previous=source;
#endif
    var o:ProbeOut;
    sourceVertex(&source,&o);
    o.clip=position_world_to_clip(source.worldPos);
    o.P=source.worldPos; o.normal=source.worldNormal; o.uv=v.uv;
    o.mesh_flags=mesh[v.instance].flags;
#ifdef UNCLIPPED_DEPTH_ORTHO_EMULATION
#ifdef PLANT_KIND_3
    o.vHf=o.clip.z;
#else
    o.unclipped_depth=o.clip.z;
#endif
    o.clip.z=min(o.clip.z,1.0);
#endif
#ifdef MOTION_VECTOR_PREPASS
    // Same source deformation and billboard orientation, with the actual
    // previous host frame. The source model is immutable for this residency.
    frame.cameraPos=plantFrame.previous_camera.xyz;
    vegParams.camPos=plantFrame.previous_camera.xyz;
    frame.time=plantFrame.previous_wind.w;
    frame.windDir=plantFrame.previous_wind.xy;
    frame.windSpeed=plantFrame.previous_wind.z;
    frame.frameIndex=u32(plantFrame.previous_gust.z);
    vegParams.gustOffset=plantFrame.previous_gust.xy;
    var previous_out:ProbeOut;
    sourceVertex(&previous,&previous_out);
#ifdef PLANT_KIND_3
    o.vTrunkT=previous.worldPos;
#else
    o.previous_position=previous.worldPos;
#endif
#endif
    return o;
}

fn forge_surface(vs:ProbeOut,front:bool)->SourceSurface {
    setupSource();
    var input:FragInput;
    input.vs=vs; input.P=vs.P; input.N=normalize(select(-vs.normal,vs.normal,front));
    input.V=normalize(plantFrame.camera.xyz-vs.P); input.uv=vs.uv;
    input.front=front; input.pixel=vs.clip.xy;
    var surface:SourceSurface;
    surface.ao=1.0;
    sourceSurface(input,&surface);
    return surface;
}

#ifdef PREPASS_PIPELINE
@fragment
fn fragment(vs:ProbeOut,@builtin(front_facing) front:bool)
#ifdef PREPASS_FRAGMENT
->FragmentOutput
#endif
{
    let surface=forge_surface(vs,front);
#ifdef PREPASS_FRAGMENT
    var out:FragmentOutput;
#ifdef NORMAL_PREPASS
    out.normal=vec4f(normalize(surface.normal)*0.5+0.5,1.0);
#endif
#ifdef MOTION_VECTOR_PREPASS
    let clip=view.unjittered_clip_from_world*vec4f(vs.P,1.0);
#ifdef PLANT_KIND_3
    let previous=previous_view_uniforms.clip_from_world*vec4f(vs.vTrunkT,1.0);
#else
    let previous=previous_view_uniforms.clip_from_world*vec4f(vs.previous_position,1.0);
#endif
    out.motion_vector=(clip.xy/clip.w-previous.xy/previous.w)*vec2f(0.5,-0.5);
#endif
#ifdef UNCLIPPED_DEPTH_ORTHO_EMULATION
#ifdef PLANT_KIND_3
    out.frag_depth=vs.vHf;
#else
    out.frag_depth=vs.unclipped_depth;
#endif
#endif
    return out;
#endif
}
#else
@fragment
fn fragment(vs:ProbeOut,@builtin(front_facing) front:bool)->@location(0) vec4f {
#ifdef FORGE_SOURCE_LIGHTING
    return forgeSourceFragment(vs,front);
#else
    let surface=forge_surface(vs,front);
    var input=pbr_types::pbr_input_new();
    input.flags=vs.mesh_flags | MESH_FLAGS_TRANSMITTED_SHADOW_RECEIVER_BIT;
    input.frag_coord=vs.clip;
    input.world_position=vec4f(vs.P,1.0);
    input.world_normal=normalize(select(-vs.normal,vs.normal,front));
    input.N=normalize(surface.normal);
    input.is_orthographic=view.clip_from_view[3].w==1.0;
    input.V=calculate_view(input.world_position,input.is_orthographic);
    input.material.base_color=vec4f(surface.albedo,1.0);
    input.material.perceptual_roughness=surface.roughness;
    input.material.metallic=surface.metalness;
    // Source dielectric F0 is 0.04 * intensity. Bevy remaps reflectance as
    // 0.16 * reflectance squared; retain F0 when changing the lighting model.
    input.material.reflectance=vec3f(0.5*sqrt(max(surface.specularIntensity,0.0)));
    input.material.diffuse_transmission=clamp(surface.translucency.x,0.0,1.0);
    input.material.flags=pbr_types::STANDARD_MATERIAL_FLAGS_DOUBLE_SIDED_BIT
        | pbr_types::STANDARD_MATERIAL_FLAGS_FOG_ENABLED_BIT;
    input.diffuse_occlusion=vec3f(surface.ao);
    return main_pass_post_lighting_processing(input,apply_pbr_lighting(input));
#endif
}
#endif
