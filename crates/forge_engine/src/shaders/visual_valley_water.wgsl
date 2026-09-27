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
#ifdef STANDARD_MATERIAL_SPECULAR_TRANSMISSION
#import "embedded://forge_engine/shaders/water_optical_depth.wgsl"::{water_optical_path_m, water_beer_transmittance, water_medium_transmittance, water_medium_scattering_weight, water_contact_coverage}
#import "embedded://forge_engine/shaders/water_flow_detail.wgsl"::{flowing_ripple_gradient_uv, river_world_gradient}
#import "embedded://forge_engine/shaders/water_reflection_preview.wgsl"::{water_scene_reflection, water_scene_refraction}
#import bevy_pbr::{mesh_view_bindings as optical_view, prepass_utils::prepass_depth}
#import bevy_pbr::{
    shadows as optical_shadows,
    mesh_view_types::DIRECTIONAL_LIGHT_FLAGS_SHADOWS_ENABLED_BIT,
    mesh_types::MESH_FLAGS_SHADOW_RECEIVER_BIT,
    view_transformations::position_world_to_view,
}
#endif
#endif

// Forge-original deterministic river surface. COLOR.r is bed depth; COLOR.gb is local flow XZ.
// CPU state supplies a wrapped current/previous phase; WGSL never reads wall-clock time.
struct VisualValleyWaterExtension {
    shallow_color_surface_tint: vec4<f32>,
    deep_color_max_depth: vec4<f32>,
    wave_parameters: vec4<f32>,
    flow_phase: vec4<f32>,
    optical_parameters: vec4<f32>,
    absorption_medium: vec4<f32>,
    scattering_medium: vec4<f32>,
    reflection_preview: vec4<f32>,
    refraction_preview: vec4<f32>,
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
#ifdef STANDARD_MATERIAL_SPECULAR_TRANSMISSION
    // Calm-river candidate: volume motion is subordinate to advected normal detail.
    let amplitude = max(water.wave_parameters.x, 0.0) * 0.15;
#else
    let amplitude = max(water.wave_parameters.x, 0.0);
#endif
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

// Millimetre-scale normal detail derived from the authored wave scale, not extra geometry.
// Fade frequencies above the pixel footprint to avoid distant specular shimmer.
fn surface_ripple_gradient(world_xz: vec2<f32>, phase: f32) -> vec2<f32> {
    let flow = safe_normalize_xz(water.flow_phase.xy);
    let across = vec2(-flow.y, flow.x);
    let first_direction = safe_normalize_xz(flow * 0.8 + across * 0.6);
    let second_direction = safe_normalize_xz(flow * 0.35 - across * 0.94);
    let wavelength = max(water.wave_parameters.y, 0.000001);
    let amplitude = max(water.wave_parameters.x, 0.0);
    let first_k = TAU / (wavelength * 0.085);
    let second_k = TAU / (wavelength * 0.137);
    let first_phase = dot(world_xz, first_direction) * first_k - phase * 3.0;
    let second_phase = dot(world_xz, second_direction) * second_k - phase * 5.0;
    let first_filter = 1.0 - smoothstep(1.0, TAU * 0.5, fwidth(first_phase));
    let second_filter = 1.0 - smoothstep(1.0, TAU * 0.5, fwidth(second_phase));
    return amplitude * (
        cos(first_phase) * 0.075 * first_k * first_direction * first_filter
        + cos(second_phase) * 0.045 * second_k * second_direction * second_filter
    );
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
    out.world_normal = base_world_normal;
#endif
#endif
#else
#ifdef VERTEX_NORMALS
    let base_world_normal = mesh_functions::mesh_normal_local_to_world(
        vertex.normal,
        vertex.instance_index,
    );
    out.world_normal = base_world_normal;
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
    // Sample the analytic gradient per fragment, independently of river strip tessellation.
    var shaded_in = in;
#ifdef VERTEX_NORMALS
    let wave = wave_response(in.world_position.xz, water.flow_phase.z);
    let normal_strength = max(water.wave_parameters.w, 0.0);
#ifdef STANDARD_MATERIAL_SPECULAR_TRANSMISSION
    let footprint = max(length(dpdx(in.uv)), length(dpdy(in.uv)));
    let detail_uv = flowing_ripple_gradient_uv(
        in.uv, water.flow_phase.z, in.color.gb,
        water.wave_parameters.y, water.wave_parameters.x, footprint,
    );
    let gradient = wave.yz + river_world_gradient(detail_uv,
        dpdx(in.uv), dpdy(in.uv),
        dpdx(in.world_position.xz), dpdy(in.world_position.xz));
#else
    let gradient = wave.yz + surface_ripple_gradient(in.world_position.xz, water.flow_phase.z);
#endif
    shaded_in.world_normal = normalize(in.world_normal
        + vec3(-gradient.x, 0.0, -gradient.y) * normal_strength);
#endif
    var pbr_input = pbr_input_from_standard_material(shaded_in, is_front);
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
#ifdef STANDARD_MATERIAL_SPECULAR_TRANSMISSION
    let scene_depth = prepass_depth(in.position, 0u);
    let view_optical_path = water_optical_path_m(
        in.position.xy, optical_view::view.viewport, optical_view::view.world_from_clip,
        in.world_position.xyz, in.position.z, scene_depth, maximum_depth,
    );
    let refracted_hit = water_scene_refraction(in.world_position.xyz, pbr_input.N,
        pbr_input.V, water.optical_parameters.z, water.refraction_preview);
    let optical_path = mix(view_optical_path, refracted_hit.path_m, refracted_hit.confidence);
    var transmission = water_beer_transmittance(
        // Authored palette colors describe appearance, not transmittance coefficients.
        // Interpret opacity as extinction at the reference depth for this explicit candidate.
        optical_path, vec3(clamp(1.0 - water.optical_parameters.x, 0.000001, 1.0)), maximum_depth,
    );
    if water.absorption_medium.w > 0.0 {
        transmission = water_medium_transmittance(optical_path,
            water.absorption_medium.rgb, water.scattering_medium.rgb);
    }
    // Bounded approximation: view-ray thickness drives a shortened refraction
    // offset, not a refracted ray/terrain intersection. Bevy rejects foreground samples.
    pbr_input.material.thickness = optical_path * surface_tint;
    pbr_input.material.base_color = vec4(transmission, 1.0);
    if refracted_hit.valid {
        // Keep full specular transmission (no diffuse water lobe). Attenuate
        // only Bevy's transmitted colour by the complement of hit confidence.
        pbr_input.material.base_color = vec4(transmission * (1.0 - refracted_hit.confidence), 1.0);
    }
#endif
#ifdef STANDARD_MATERIAL_SPECULAR_TRANSMISSION
    let scene_reflection = water_scene_reflection(in.world_position.xyz, pbr_input.N,
        pbr_input.V, pbr_input.material.perceptual_roughness, water.reflection_preview);
    let original_specular_occlusion = pbr_input.specular_occlusion;
    // Replace only the environment specular contribution, not transmission or sun.
    pbr_input.specular_occlusion *= 1.0 - scene_reflection.a;
#endif
    out.color = apply_pbr_lighting(pbr_input);
#ifdef STANDARD_MATERIAL_SPECULAR_TRANSMISSION
    // Bounded diffuse-volume approximation, not a volumetric ray integration.
    // Palette RGB is dimensionless: illuminate it before applying exposure,
    // rather than treating it as an almost-dark emissive radiance in a sunlit scene.
    var incident_irradiance = max(optical_view::lights.ambient_color.rgb, vec3(0.0));
    let water_view_z = position_world_to_view(in.world_position.xyz).z;
    for (var i = 0u; i < optical_view::lights.n_directional_lights; i += 1u) {
        let light = optical_view::lights.directional_lights[i];
        var visibility = 1.0;
        if ((pbr_input.flags & MESH_FLAGS_SHADOW_RECEIVER_BIT) != 0u
            && (light.flags & DIRECTIONAL_LIGHT_FLAGS_SHADOWS_ENABLED_BIT) != 0u) {
            visibility = optical_shadows::fetch_directional_shadow(
                i, in.world_position, in.world_normal, water_view_z, in.position.xy);
        }
        incident_irradiance += light.color.rgb
            * max(light.direction_to_light.y, 0.0) * visibility;
    }
    let f0 = 0.16 * pbr_input.material.reflectance * pbr_input.material.reflectance;
    let fresnel = f0 + (vec3(1.0) - f0)
        * pow(1.0 - clamp(dot(pbr_input.N, pbr_input.V), 0.0, 1.0), 5.0);
    if refracted_hit.valid {
        // Transmission texture is already exposed; do not expose it twice.
        out.color = vec4(out.color.rgb + refracted_hit.radiance * transmission
            * (vec3(1.0) - fresnel) * refracted_hit.confidence, out.color.a);
    }
    out.color = vec4(out.color.rgb + scene_reflection.rgb * fresnel
        * scene_reflection.a * original_specular_occlusion, out.color.a);
    var scattering_weight = water.deep_color_max_depth.rgb * (vec3(1.0) - transmission);
    if water.absorption_medium.w > 0.0 {
        scattering_weight = water_medium_scattering_weight(transmission,
            water.absorption_medium.rgb, water.scattering_medium.rgb);
    }
    let scattered = scattering_weight
        * (vec3(1.0) - fresnel) * incident_irradiance / 3.141592653589793;
    out.color = vec4(out.color.rgb + optical_view::view.exposure * scattered, out.color.a);
#endif
    out.color = main_pass_post_lighting_processing(pbr_input, out.color);
#ifdef STANDARD_MATERIAL_SPECULAR_TRANSMISSION
    // The opaque background is already exposed and fogged. Blend after water's
    // post-lighting processing, without exposing or fogging the background twice.
    // Absolute target pixels match Bevy's full-target transmission texture even
    // for an offset viewport. One exact, undistorted read; no extra render pass.
    let contact_coverage = water_contact_coverage(view_optical_path, 0.2);
    if contact_coverage < 1.0 {
        let dimensions = vec2<i32>(textureDimensions(optical_view::view_transmission_texture));
        let pixel = clamp(vec2<i32>(in.position.xy), vec2(0), dimensions - vec2(1));
        let background = textureLoad(optical_view::view_transmission_texture, pixel, 0);
        if background.a > 0.0 {
            out.color = vec4(mix(background.rgb, out.color.rgb, contact_coverage), out.color.a);
        }
    }
#endif
    return out;
}
#else
#ifdef PREPASS_FRAGMENT
@fragment
fn fragment(
    in: VertexOutput,
    @builtin(front_facing) is_front: bool,
) -> FragmentOutput {
    // Sample the analytic gradient per fragment, independently of river strip tessellation.
    var shaded_in = in;
#ifdef VERTEX_NORMALS
    let wave = wave_response(in.world_position.xz, water.flow_phase.z);
    let normal_strength = max(water.wave_parameters.w, 0.0);
    let gradient = wave.yz + surface_ripple_gradient(in.world_position.xz, water.flow_phase.z);
    shaded_in.world_normal = normalize(in.world_normal
        + vec3(-gradient.x, 0.0, -gradient.y) * normal_strength);
#endif
    var pbr_input = pbr_input_from_standard_material(shaded_in, is_front);
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
