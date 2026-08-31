#import bevy_pbr::{
    mesh_functions,
    pbr_fragment::pbr_input_from_standard_material,
    pbr_functions::alpha_discard,
    view_transformations::position_world_to_clip,
}

#ifdef PREPASS_PIPELINE
#import bevy_pbr::prepass_io::{Vertex, VertexOutput}
#else
#import bevy_pbr::{
    forward_io::{Vertex, VertexOutput, FragmentOutput},
    pbr_functions::{apply_pbr_lighting, main_pass_post_lighting_processing},
}
#endif

#ifdef VISIBILITY_RANGE_DITHER
#import bevy_pbr::pbr_functions::visibility_range_dither;
#endif

// Forge-original procedural vegetation shader. COLOR.rgb carries palette weights and
// COLOR.a carries a normalized wind/interaction weight. MeshTag is variation only.
struct VisualValleyVegetationExtension {
    primary_color_roughness: vec4<f32>,
    secondary_color_roughness: vec4<f32>,
    bark_color_roughness: vec4<f32>,
    metallic_reflectance: vec4<f32>,
    wind_parameters: vec4<f32>,
    interaction_center_radius: vec4<f32>,
    interaction_direction_strength: vec4<f32>,
    wind_direction: vec4<f32>,
    wetness_parameters: vec4<f32>,
}

@group(#{MATERIAL_BIND_GROUP}) @binding(100)
var<uniform> vegetation: VisualValleyVegetationExtension;

const TAU: f32 = 6.283185307179586;
const TAG_PHASE_MASK: u32 = 0x00ffffffu;
const TAG_PHASE_DENOMINATOR: f32 = 16777215.0;

fn normalized_palette_weights(raw_weights: vec3<f32>) -> vec3<f32> {
    let non_negative = max(raw_weights, vec3(0.0));
    let total = dot(non_negative, vec3(1.0));
    if total <= 0.000001 {
        return vec3(1.0, 0.0, 0.0);
    }
    return non_negative / total;
}

fn safe_normalize_xz(value: vec2<f32>, fallback: vec2<f32>) -> vec2<f32> {
    let length_squared = dot(value, value);
    if length_squared > 0.000001 {
        return value * inverseSqrt(length_squared);
    }
    let fallback_length_squared = dot(fallback, fallback);
    if fallback_length_squared > 0.000001 {
        return fallback * inverseSqrt(fallback_length_squared);
    }
    return vec2(1.0, 0.0);
}

fn variation_phase(instance_index: u32) -> f32 {
    let tag = mesh_functions::get_tag(instance_index) & TAG_PHASE_MASK;
    return f32(tag) / TAG_PHASE_DENOMINATOR * TAU;
}

fn instance_wind_response(instance_index: u32) -> f32 {
    let response = mesh_functions::get_tag(instance_index) >> 24u;
    return f32(response) / 255.0;
}

fn bounded_deformation(
    world_position: vec3<f32>,
    wind_weight_raw: f32,
    instance_index: u32,
    instance_scale: f32,
    time_seconds: f32,
) -> vec3<f32> {
    let wind_weight = clamp(
        wind_weight_raw * instance_wind_response(instance_index),
        0.0,
        1.0,
    );
    if wind_weight <= 0.000001 {
        return vec3(0.0);
    }

    let wind_direction = safe_normalize_xz(vegetation.wind_direction.xz, vec2(1.0, 0.0));
    let phase = variation_phase(instance_index);
    let angular_time = time_seconds * max(vegetation.wind_parameters.y, 0.0) * TAU;
    let spatial_phase = world_position.x * 0.071 + world_position.z * 0.053;
    let primary_wave = sin(angular_time + phase + spatial_phase);
    let secondary_wave = sin(angular_time * 0.47 - phase * 0.63 + spatial_phase * 1.71);
    let bounded_instance_scale = max(instance_scale, 0.000001);
    let wind_amount = vegetation.wind_parameters.x
        * bounded_instance_scale
        * wind_weight
        * (primary_wave * 0.72 + secondary_wave * 0.28);
    var offset_xz = wind_direction * wind_amount;

    let interaction_radius = max(vegetation.interaction_center_radius.w, 0.0);
    let interaction_delta = world_position.xz - vegetation.interaction_center_radius.xz;
    let interaction_distance = length(interaction_delta);
    if interaction_radius > 0.000001 && interaction_distance < interaction_radius {
        let fallback_push = vegetation.interaction_direction_strength.xy;
        let push_direction = safe_normalize_xz(interaction_delta, fallback_push);
        let normalized_distance = clamp(interaction_distance / interaction_radius, 0.0, 1.0);
        let falloff = (1.0 - normalized_distance) * (1.0 - normalized_distance);
        offset_xz += push_direction
            * max(vegetation.interaction_direction_strength.z, 0.0)
            * bounded_instance_scale
            * falloff
            * wind_weight;
    }

    // The CPU installs an AABB enlarged by this exact sum. Clamp the combined vector, rather
    // than each effect separately, so wind and interaction cannot escape that conservative bound.
    let maximum_displacement = max(
        vegetation.wind_parameters.x + vegetation.interaction_direction_strength.z,
        0.0,
    ) * bounded_instance_scale;
    let displacement_length = length(offset_xz);
    if displacement_length > maximum_displacement && displacement_length > 0.000001 {
        offset_xz *= maximum_displacement / displacement_length;
    }
    return vec3(offset_xz.x, 0.0, offset_xz.y);
}

@vertex
fn vertex(vertex: Vertex) -> VertexOutput {
    var out: VertexOutput;
    let world_from_local = mesh_functions::get_world_from_local(vertex.instance_index);
    let instance_scale = max(
        length(world_from_local[0].xyz),
        length(world_from_local[2].xyz),
    );
    let undeformed_world_position = mesh_functions::mesh_position_local_to_world(
        world_from_local,
        vec4(vertex.position, 1.0),
    );

#ifdef VERTEX_COLORS
    let wind_weight = vertex.color.a;
#else
    let wind_weight = 0.0;
#endif

    let displacement = bounded_deformation(
        undeformed_world_position.xyz,
        wind_weight,
        vertex.instance_index,
        instance_scale,
        vegetation.wind_parameters.z,
    );
    out.world_position = undeformed_world_position + vec4(displacement, 0.0);
    out.position = position_world_to_clip(out.world_position.xyz);

#ifdef UNCLIPPED_DEPTH_ORTHO_EMULATION
    out.unclipped_depth = out.position.z;
    out.position.z = min(out.position.z, 1.0);
#endif

#ifdef VERTEX_NORMALS
#ifdef PREPASS_PIPELINE
#ifdef NORMAL_PREPASS_OR_DEFERRED_PREPASS
    out.world_normal = mesh_functions::mesh_normal_local_to_world(
        vertex.normal,
        vertex.instance_index,
    );
#endif
#else
    out.world_normal = mesh_functions::mesh_normal_local_to_world(
        vertex.normal,
        vertex.instance_index,
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
    let previous_world_from_local = mesh_functions::get_previous_world_from_local(vertex.instance_index);
    let previous_instance_scale = max(
        length(previous_world_from_local[0].xyz),
        length(previous_world_from_local[2].xyz),
    );
    let previous_undeformed_world_position = mesh_functions::mesh_position_local_to_world(
        previous_world_from_local,
        vec4(vertex.position, 1.0),
    );
    let previous_displacement = bounded_deformation(
        previous_undeformed_world_position.xyz,
        wind_weight,
        vertex.instance_index,
        previous_instance_scale,
        vegetation.wind_parameters.w,
    );
    out.previous_world_position = previous_undeformed_world_position
        + vec4(previous_displacement, 0.0);
#endif

#ifdef VERTEX_OUTPUT_INSTANCE_INDEX
    out.instance_index = vertex.instance_index;
#endif
#ifdef VISIBILITY_RANGE_DITHER
    out.visibility_range_dither = mesh_functions::get_visibility_range_dither_level(
        vertex.instance_index,
        world_from_local[3],
    );
#endif
    return out;
}

#ifndef PREPASS_PIPELINE
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
    let palette = normalized_palette_weights(in.color.rgb);
#else
    let palette = vec3(1.0, 0.0, 0.0);
#endif

    let dry_base_color =
        vegetation.primary_color_roughness.rgb * palette.x
            + vegetation.secondary_color_roughness.rgb * palette.y
            + vegetation.bark_color_roughness.rgb * palette.z;
    let wetness = clamp(vegetation.wetness_parameters.x, 0.0, 1.0);
    let wet_albedo_multiplier = mix(1.0, vegetation.wetness_parameters.z, wetness);
    let wet_roughness_multiplier = mix(1.0, vegetation.wetness_parameters.y, wetness);
    pbr_input.material.base_color = vec4(dry_base_color * wet_albedo_multiplier, 1.0);
    pbr_input.material.perceptual_roughness = clamp(
        dot(
            vec3(
                vegetation.primary_color_roughness.a,
                vegetation.secondary_color_roughness.a,
                vegetation.bark_color_roughness.a,
            ),
            palette,
        ) * wet_roughness_multiplier,
        0.04,
        1.0,
    );
    pbr_input.material.metallic = clamp(vegetation.metallic_reflectance.x, 0.0, 1.0);
    pbr_input.material.reflectance = vec3(
        clamp(vegetation.metallic_reflectance.y, 0.0, 1.0),
    );
    pbr_input.material.base_color = alpha_discard(
        pbr_input.material,
        pbr_input.material.base_color,
    );

    var out: FragmentOutput;
    out.color = apply_pbr_lighting(pbr_input);
    out.color = main_pass_post_lighting_processing(pbr_input, out.color);
    return out;
}
#endif
