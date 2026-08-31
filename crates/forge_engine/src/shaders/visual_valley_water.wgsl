#import bevy_pbr::{
    mesh_functions,
    pbr_fragment::pbr_input_from_standard_material,
    pbr_functions::alpha_discard,
    view_transformations::position_world_to_clip,
}

#ifdef PREPASS_PIPELINE
#import bevy_pbr::prepass_io::{Vertex, VertexOutput}
#ifdef PREPASS_FRAGMENT
#import bevy_pbr::{
    prepass_io::FragmentOutput,
    pbr_deferred_functions::deferred_output,
}
#endif
#else
#import bevy_pbr::{
    forward_io::{Vertex, VertexOutput, FragmentOutput},
    pbr_functions::{apply_pbr_lighting, main_pass_post_lighting_processing},
}
#endif

// Forge-original deterministic river surface. COLOR.r stores interpolated bed depth in metres.
// CPU state supplies a wrapped current/previous phase; WGSL never reads wall-clock time.
struct VisualValleyWaterExtension {
    shallow_color_surface_tint: vec4<f32>,
    deep_color_max_depth: vec4<f32>,
    wave_parameters: vec4<f32>,
    flow_phase: vec4<f32>,
    optical_parameters: vec4<f32>,
}

@group(#{MATERIAL_BIND_GROUP}) @binding(100)
var<uniform> water: VisualValleyWaterExtension;

const TAU: f32 = 6.283185307179586;

fn safe_normalize_xz(value: vec2<f32>) -> vec2<f32> {
    let length_squared = dot(value, value);
    if length_squared > 0.000001 {
        return value * inverseSqrt(length_squared);
    }
    return vec2(1.0, 0.0);
}

// X is bounded vertical displacement. Y/Z are its analytic world-XZ gradient.
fn wave_response(world_xz: vec2<f32>, phase: f32) -> vec3<f32> {
    let amplitude = max(water.wave_parameters.x, 0.0);
    let wavelength = max(water.wave_parameters.y, 0.000001);
    let wave_number = TAU / wavelength;
    let flow = safe_normalize_xz(water.flow_phase.xy);
    let cross_flow = vec2(-flow.y, flow.x);
    let secondary = safe_normalize_xz(flow * 0.55 + cross_flow * 0.835);
    let primary_phase = dot(world_xz, flow) * wave_number - phase;
    let secondary_wave_number = wave_number * 1.73;
    // The integer temporal multiplier keeps the secondary octave continuous at phase wrap.
    let secondary_phase = dot(world_xz, secondary) * secondary_wave_number - phase * 2.0;
    let primary_weight = 0.72;
    let secondary_weight = 0.28;
    let height = amplitude * (
        sin(primary_phase) * primary_weight
        + sin(secondary_phase) * secondary_weight
    );
    let gradient = amplitude * (
        cos(primary_phase) * primary_weight * wave_number * flow
        + cos(secondary_phase) * secondary_weight * secondary_wave_number * secondary
    );
    return vec3(height, gradient);
}

@vertex
fn vertex(vertex: Vertex) -> VertexOutput {
    var out: VertexOutput;
    let world_from_local = mesh_functions::get_world_from_local(vertex.instance_index);
    let undeformed_world_position = mesh_functions::mesh_position_local_to_world(
        world_from_local,
        vec4(vertex.position, 1.0),
    );
    let wave = wave_response(undeformed_world_position.xz, water.flow_phase.z);
    out.world_position = undeformed_world_position + vec4(0.0, wave.x, 0.0, 0.0);
    out.position = position_world_to_clip(out.world_position.xyz);

#ifdef UNCLIPPED_DEPTH_ORTHO_EMULATION
    out.unclipped_depth = out.position.z;
    out.position.z = min(out.position.z, 1.0);
#endif

#ifdef PREPASS_PIPELINE
#ifdef NORMAL_PREPASS_OR_DEFERRED_PREPASS
#ifdef VERTEX_NORMALS
    let base_world_normal = mesh_functions::mesh_normal_local_to_world(
        vertex.normal,
        vertex.instance_index,
    );
    let normal_strength = max(water.wave_parameters.w, 0.0);
    out.world_normal = normalize(
        base_world_normal + vec3(-wave.y, 0.0, -wave.z) * normal_strength,
    );
#endif
#endif
#else
#ifdef VERTEX_NORMALS
    let base_world_normal = mesh_functions::mesh_normal_local_to_world(
        vertex.normal,
        vertex.instance_index,
    );
    let normal_strength = max(water.wave_parameters.w, 0.0);
    out.world_normal = normalize(
        base_world_normal + vec3(-wave.y, 0.0, -wave.z) * normal_strength,
    );
#endif
#endif

#ifdef VERTEX_UVS_A
    out.uv = vertex.uv;
#endif
#ifdef VERTEX_UVS_B
    out.uv_b = vertex.uv_b;
#endif
#ifdef VERTEX_TANGENTS
#ifdef PREPASS_PIPELINE
#ifdef NORMAL_PREPASS_OR_DEFERRED_PREPASS
    out.world_tangent = mesh_functions::mesh_tangent_local_to_world(
        world_from_local,
        vertex.tangent,
        vertex.instance_index,
    );
#endif
#else
    out.world_tangent = mesh_functions::mesh_tangent_local_to_world(
        world_from_local,
        vertex.tangent,
        vertex.instance_index,
    );
#endif
#endif
#ifdef VERTEX_COLORS
    out.color = vertex.color;
#endif

#ifdef MOTION_VECTOR_PREPASS
    let previous_world_from_local = mesh_functions::get_previous_world_from_local(
        vertex.instance_index,
    );
    let previous_undeformed_world_position = mesh_functions::mesh_position_local_to_world(
        previous_world_from_local,
        vec4(vertex.position, 1.0),
    );
    let previous_wave = wave_response(
        previous_undeformed_world_position.xz,
        water.flow_phase.w,
    );
    out.previous_world_position = previous_undeformed_world_position
        + vec4(0.0, previous_wave.x, 0.0, 0.0);
#endif

#ifdef VERTEX_OUTPUT_INSTANCE_INDEX
    out.instance_index = vertex.instance_index;
#endif
    return out;
}

#ifndef PREPASS_PIPELINE
@fragment
fn fragment(
    in: VertexOutput,
    @builtin(front_facing) is_front: bool,
) -> FragmentOutput {
    var pbr_input = pbr_input_from_standard_material(in, is_front);
#ifdef VERTEX_COLORS
    let sampled_bed_depth = max(in.color.r, 0.0);
#else
    let sampled_bed_depth = 0.0;
#endif
    let maximum_depth = max(water.deep_color_max_depth.w, 0.000001);
    let depth_response = smoothstep(0.0, 1.0, sampled_bed_depth / maximum_depth);
    let depth_color = mix(
        water.shallow_color_surface_tint.rgb,
        water.deep_color_max_depth.rgb,
        depth_response,
    );
    let surface_tint = clamp(water.shallow_color_surface_tint.w, 0.0, 1.0);
    let resolved_color = mix(
        depth_color,
        water.deep_color_max_depth.rgb,
        depth_response * surface_tint,
    );
    pbr_input.material.base_color = vec4(resolved_color, 1.0);
    pbr_input.material.perceptual_roughness = clamp(
        water.optical_parameters.y,
        0.04,
        1.0,
    );
    pbr_input.material.metallic = 0.0;
    pbr_input.material.reflectance = vec3(clamp(water.optical_parameters.w, 0.0, 1.0));
    pbr_input.material.base_color = alpha_discard(
        pbr_input.material,
        pbr_input.material.base_color,
    );
    var out: FragmentOutput;
    out.color = apply_pbr_lighting(pbr_input);
    out.color = main_pass_post_lighting_processing(pbr_input, out.color);
    return out;
}
#else
#ifdef PREPASS_FRAGMENT
@fragment
fn fragment(
    in: VertexOutput,
    @builtin(front_facing) is_front: bool,
) -> FragmentOutput {
    var pbr_input = pbr_input_from_standard_material(in, is_front);
#ifdef VERTEX_COLORS
    let sampled_bed_depth = max(in.color.r, 0.0);
#else
    let sampled_bed_depth = 0.0;
#endif
    let maximum_depth = max(water.deep_color_max_depth.w, 0.000001);
    let depth_response = smoothstep(0.0, 1.0, sampled_bed_depth / maximum_depth);
    let depth_color = mix(
        water.shallow_color_surface_tint.rgb,
        water.deep_color_max_depth.rgb,
        depth_response,
    );
    let surface_tint = clamp(water.shallow_color_surface_tint.w, 0.0, 1.0);
    let resolved_color = mix(
        depth_color,
        water.deep_color_max_depth.rgb,
        depth_response * surface_tint,
    );
    pbr_input.material.base_color = vec4(resolved_color, 1.0);
    pbr_input.material.perceptual_roughness = clamp(
        water.optical_parameters.y,
        0.04,
        1.0,
    );
    pbr_input.material.metallic = 0.0;
    pbr_input.material.reflectance = vec3(clamp(water.optical_parameters.w, 0.0, 1.0));
    pbr_input.material.base_color = alpha_discard(
        pbr_input.material,
        pbr_input.material.base_color,
    );
    return deferred_output(in, pbr_input);
}
#endif
#endif
