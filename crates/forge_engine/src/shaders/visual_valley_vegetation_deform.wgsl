#import bevy_pbr::{
    mesh_functions,
    pbr_fragment::pbr_input_from_standard_material,
    pbr_functions::alpha_discard,
    pbr_types,
    view_transformations::position_world_to_clip,
    mesh_view_bindings::view,
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
    hlod_distances: vec4<f32>,
    prototype_parameters: vec4<f32>,
    shadow_fade_distances: vec4<f32>,
    temporal_parameters: vec4<f32>,
}

@group(#{MATERIAL_BIND_GROUP}) @binding(100)
var<uniform> vegetation: VisualValleyVegetationExtension;

struct TemporalLodRecord {
    codes: vec4<i32>,
    metadata: vec4<u32>,
}
@group(#{MATERIAL_BIND_GROUP}) @binding(101)
var<storage, read> temporal_lods: array<TemporalLodRecord>;

fn temporal_record_valid(slot: u32) -> bool {
    return vegetation.temporal_parameters.x > 0.5
        && slot > 0u && slot < arrayLength(&temporal_lods);
}

fn original_mesh_tag(instance_index: u32) -> u32 {
    let tag = mesh_functions::get_tag(instance_index);
    let slot = tag / 4u;
    if temporal_record_valid(slot) {
        return temporal_lods[slot].metadata.y;
    }
    return tag;
}

fn temporal_code(slot: u32, level: u32, spatial_fallback: i32) -> i32 {
    if temporal_record_valid(slot) {
        if temporal_lods[slot].metadata.x == 1u {
            return temporal_lods[slot].codes[min(level, 3u)];
        }
    }
    return spatial_fallback;
}

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
    let tag = original_mesh_tag(instance_index) & TAG_PHASE_MASK;
    return f32(tag) / TAG_PHASE_DENOMINATOR * TAU;
}

fn instance_wind_response(instance_index: u32) -> f32 {
    let response = original_mesh_tag(instance_index) >> 24u;
    return f32(response) / 255.0;
}

// Encode a fade-in for Bevy's public signed dither contract: -16 hidden, 0 solid.
// The corresponding individual LOD fades out with the complementary 0..16 value.
fn hlod_placement_dither(root_world: vec3<f32>) -> i32 {
    let distance = length(view.lod_view_world_position.xyz - root_world);
    if distance >= vegetation.hlod_distances.z {
        return 16;
    }
    let fraction = clamp((distance - vegetation.hlod_distances.x)
        / (vegetation.hlod_distances.y - vegetation.hlod_distances.x), 0.0, 1.0);
    return i32(round(fraction * 16.0)) - 16;
}

fn bounded_deformation(
    world_position: vec3<f32>,
    wind_weight_raw: f32,
    phase: f32,
    wind_response: f32,
    instance_scale: f32,
    time_seconds: f32,
) -> vec3<f32> {
    if vegetation.temporal_parameters.y > 0.5 {
        return vec3(0.0);
    }
    let wind_weight = clamp(
        wind_weight_raw * wind_response,
        0.0,
        1.0,
    );
    if wind_weight <= 0.000001 {
        return vec3(0.0);
    }

    let wind_direction = safe_normalize_xz(vegetation.wind_direction.xz, vec2(1.0, 0.0));
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
fn vertex(vertex: Vertex
#ifdef FORGE_FAR_BATCH
    , @location(10) far_root_scale: vec4<f32>
    , @location(11) far_wind: vec4<f32>
#endif
) -> VertexOutput {
    var out: VertexOutput;
    let world_from_local = mesh_functions::get_world_from_local(vertex.instance_index);
    var instance_scale = max(
        length(world_from_local[0].xyz),
        length(world_from_local[2].xyz),
    );
    let undeformed_world_position = mesh_functions::mesh_position_local_to_world(
        world_from_local,
        vec4(vertex.position, 1.0),
    );

#ifdef VERTEX_COLORS
    var wind_weight = vertex.color.a;
#else
    var wind_weight = 0.0;
#endif
    if vegetation.prototype_parameters.x > 0.5 {
        // Root-normalized geometry: alpha remains authored opacity, never a wind mask.
        let height_fraction = clamp(vertex.position.y * vegetation.prototype_parameters.y, 0.0, 1.0);
        wind_weight = height_fraction * height_fraction;
    }

#ifdef FORGE_FAR_BATCH
    wind_weight = far_wind.x;
    instance_scale *= far_root_scale.w;
    let phase = far_wind.y;
    let wind_response = far_wind.z;
#else
    let phase = variation_phase(vertex.instance_index);
    let wind_response = instance_wind_response(vertex.instance_index);
#endif
    let displacement = bounded_deformation(
        undeformed_world_position.xyz,
        wind_weight,
        phase,
        wind_response,
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

#ifdef PREPASS_PIPELINE
#ifdef MOTION_VECTOR_PREPASS
    let previous_world_from_local = mesh_functions::get_previous_world_from_local(vertex.instance_index);
    var previous_instance_scale = max(
        length(previous_world_from_local[0].xyz),
        length(previous_world_from_local[2].xyz),
    );
    let previous_undeformed_world_position = mesh_functions::mesh_position_local_to_world(
        previous_world_from_local,
        vec4(vertex.position, 1.0),
    );
#ifdef FORGE_FAR_BATCH
    previous_instance_scale *= far_root_scale.w;
#endif
    let previous_displacement = bounded_deformation(
        previous_undeformed_world_position.xyz,
        wind_weight,
        phase,
        wind_response,
        previous_instance_scale,
        vegetation.wind_parameters.w,
    );
    out.previous_world_position = previous_undeformed_world_position
        + vec4(previous_displacement, 0.0);
#endif
#endif

#ifdef VERTEX_OUTPUT_INSTANCE_INDEX
    out.instance_index = vertex.instance_index;
#endif
#ifdef VISIBILITY_RANGE_DITHER
#ifdef FORGE_FAR_BATCH
    let root_world = mesh_functions::mesh_position_local_to_world(world_from_local,
        vec4(far_root_scale.xyz, 1.0));
    out.visibility_range_dither = hlod_placement_dither(root_world.xyz);
#else
#ifdef VERTEX_UVS_B
    // UV1 is present only on Forge's procedural HLOD batches. Use the same undeformed
    // root as the individual tree/shrub/fern, including its terrain elevation.
    if vegetation.prototype_parameters.x <= 0.5 {
        let root_world = mesh_functions::mesh_position_local_to_world(world_from_local,
            vec4(vertex.uv.x, vertex.uv_b.x, vertex.uv.y, 1.0));
        out.visibility_range_dither = hlod_placement_dither(root_world.xyz);
    } else {
        // Imported UV1 is an authored mapping, not a procedural HLOD root encoding.
        out.visibility_range_dither = mesh_functions::get_visibility_range_dither_level(
            vertex.instance_index, world_from_local[3]);
    }
#else
    out.visibility_range_dither = mesh_functions::get_visibility_range_dither_level(
        vertex.instance_index,
        world_from_local[3],
    );
#endif
#endif
    // The standard normal/motion prepass consumes this same varying once; do not
    // add a second rejection there. Stable ranges remain the readiness fallback.
#ifdef FORGE_FAR_BATCH
    out.visibility_range_dither = temporal_code(u32(far_wind.w), 2u,
        out.visibility_range_dither);
#else
    let temporal_tag = mesh_functions::get_tag(vertex.instance_index);
    out.visibility_range_dither = temporal_code(temporal_tag / 4u, temporal_tag % 4u,
        out.visibility_range_dither);
#endif
#endif
    return out;
}

#ifdef FORGE_SHADOW_FADE
#import bevy_pbr::pbr_prepass_functions
#import bevy_pbr::pbr_functions::DITHER_THRESHOLD_MAP
// Same signed coverage contract and threshold map as Bevy's color/prepass path,
// without requiring the visibility-range view buffer absent in shadow layouts.
fn temporal_shadow_discard(position: vec4<f32>, code: i32) {
    if code == 0 { return; }
    if abs(code) >= 16 { discard; }
    let texel = vec2<u32>(floor(position.xy)) % 4u;
    let rank = i32((DITHER_THRESHOLD_MAP[texel.y] >> (texel.x * 8u)) & 255u);
    var retained = rank >= -code;
    if code > 0 { retained = rank < 16 - code; }
    if !retained { discard; }
}
@fragment
fn shadow_fragment(in: VertexOutput) -> @builtin(frag_depth) f32 {
    // Keep authored mask/blend alpha rejection before applying distance fade.
    pbr_prepass_functions::prepass_alpha_discard(in);
    let tag = mesh_functions::get_tag(in.instance_index);
    let slot = tag / 4u;
    var temporal_active = false;
    if temporal_record_valid(slot) {
        temporal_active = temporal_lods[slot].metadata.x == 1u;
    }
    if temporal_active {
        temporal_shadow_discard(in.position, temporal_code(slot, tag % 4u, 0));
    } else if vegetation.prototype_parameters.x > 0.5 && vegetation.temporal_parameters.y <= 0.5 {
        let root = mesh_functions::get_world_from_local(in.instance_index)[3].xyz;
        let distance = length(view.lod_view_world_position.xyz - root);
        let start = vegetation.shadow_fade_distances.x;
        let end = vegetation.shadow_fade_distances.y;
        let fraction = clamp((distance - start) / max(end - start, 0.001), 0.0, 1.0);
        // Fixed shadow-texel pattern: no frame-dependent noise or range-buffer binding.
        let thresholds = array<f32, 16>(
            0.5, 8.5, 2.5, 10.5, 12.5, 4.5, 14.5, 6.5,
            3.5, 11.5, 1.5, 9.5, 15.5, 7.5, 13.5, 5.5);
        let texel = vec2<u32>(in.position.xy) % vec2<u32>(4u);
        if fraction >= thresholds[texel.y * 4u + texel.x] / 16.0 {
            discard;
        }
    }
#ifdef UNCLIPPED_DEPTH_ORTHO_EMULATION
    return in.unclipped_depth;
#else
    return in.position.z;
#endif
}
#endif

#ifndef PREPASS_PIPELINE
@fragment
fn fragment(
    in: VertexOutput,
    @builtin(front_facing) is_front: bool,
) -> FragmentOutput {
    var pbr_input = pbr_input_from_standard_material(in, is_front);
    var lod_opacity = 1.0;
#ifdef VISIBILITY_RANGE_DITHER
    let alpha_mode = pbr_input.material.flags & pbr_types::STANDARD_MATERIAL_FLAGS_ALPHA_MODE_RESERVED_BITS;
    if alpha_mode == pbr_types::STANDARD_MATERIAL_FLAGS_ALPHA_MODE_BLEND {
        // Transparent foliage already has coverage. Do not punch a second screen-space
        // checkerboard into its atlas. Keep Bevy's signed complementary LOD weights;
        // opaque/masked wood still needs dither because it writes depth.
        lod_opacity = clamp(1.0 - abs(f32(in.visibility_range_dither)) / 16.0, 0.0, 1.0);
    } else {
        visibility_range_dither(in.position, in.visibility_range_dither);
    }
#endif

#ifdef VERTEX_COLORS
    let palette = normalized_palette_weights(in.color.rgb);
#else
    let palette = vec3(1.0, 0.0, 0.0);
#endif

    if vegetation.prototype_parameters.x <= 0.5 {
    let dry_base_color =
        vegetation.primary_color_roughness.rgb * palette.x
            + vegetation.secondary_color_roughness.rgb * palette.y
            + vegetation.bark_color_roughness.rgb * palette.z;
    let wetness = clamp(vegetation.wetness_parameters.x, 0.0, 1.0);
    let wet_albedo_multiplier = mix(1.0, vegetation.wetness_parameters.z, wetness);
    // Weather scales microfacet alpha; Bevy consumes its perceptual square root.
    let wet_roughness_multiplier = sqrt(mix(1.0, vegetation.wetness_parameters.y, wetness));
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
    }
    // Governed mode retains PBR maps, vertex tint and alpha; wetness changes RGB/roughness only.
    if vegetation.prototype_parameters.x > 0.5 && vegetation.temporal_parameters.y <= 0.5 {
        let wetness = clamp(vegetation.wetness_parameters.x, 0.0, 1.0);
        pbr_input.material.base_color = vec4(
            pbr_input.material.base_color.rgb * mix(1.0, vegetation.wetness_parameters.z, wetness),
            pbr_input.material.base_color.a);
        pbr_input.material.perceptual_roughness = clamp(
            pbr_input.material.perceptual_roughness * sqrt(mix(1.0, vegetation.wetness_parameters.y, wetness)),
            0.0, 1.0);
    }
    pbr_input.material.base_color = alpha_discard(
        pbr_input.material,
        pbr_input.material.base_color,
    );
    pbr_input.material.base_color.a *= lod_opacity;

    var out: FragmentOutput;
    out.color = apply_pbr_lighting(pbr_input);
    out.color = main_pass_post_lighting_processing(pbr_input, out.color);
    return out;
}
#endif
