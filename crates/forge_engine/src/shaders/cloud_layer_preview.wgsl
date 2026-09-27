#import bevy_pbr::{forward_io::VertexOutput, mesh_view_bindings::view}
#import bevy_pbr::mesh_view_bindings as bindings
#ifdef ENVIRONMENT_MAP
#import bevy_pbr::environment_map::quat_rotate
#endif

struct CloudLayer {
    layer: vec4<f32>,
    offset_sun: vec4<f32>,
    radiance: vec4<f32>,
    march: vec4<f32>,
}
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var density: texture_3d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var density_sampler: sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var<uniform> cloud: CloudLayer;

fn field(position: vec3<f32>, depth: f32) -> f32 {
    let uv = vec3(position.x / cloud.layer.y - cloud.offset_sun.x,
        (position.y - cloud.layer.x) / depth,
        position.z / cloud.layer.y - cloud.offset_sun.y);
    // Explicit LOD is legal inside the bounded, divergent traversal.
    return textureSampleLevel(density, density_sampler, uv, 0.0).r;
}

// Use the existing view environment, not the solar colour as an ambient fill.
// One cubemap read per sky fragment, outside the volume loop. Missing IBL is black.
fn sky_fill() -> vec3<f32> {
#ifdef ENVIRONMENT_MAP
    let index = bindings::light_probes.view_cubemap_index;
    if index < 0 { return vec3(0.0); }
    var direction = quat_rotate(bindings::light_probes.view_rotation, vec3(0.0, 1.0, 0.0));
    direction.z = -direction.z;
#ifdef MULTIPLE_LIGHT_PROBES_IN_ARRAY
    let irradiance = textureSampleLevel(bindings::diffuse_environment_maps[index],
        bindings::environment_map_sampler, direction, 0.0).rgb;
#else
    let irradiance = textureSampleLevel(bindings::diffuse_environment_map,
        bindings::environment_map_sampler, direction, 0.0).rgb;
#endif
    return irradiance * bindings::light_probes.intensity_for_view * 0.5;
#else
    return vec3(0.0);
#endif
}

@fragment
fn fragment(in: VertexOutput) -> @location(0) vec4<f32> {
    let ray = normalize(in.world_position.xyz - view.world_position);
    let height = cloud.layer.x - view.world_position.y;
    if ray.y <= 0.08 || height <= 0.0 { discard; }
    let depth = cloud.layer.y * cloud.march.y;
    let entry = height / ray.y;
    let steps = clamp(cloud.march.x, 1.0, 32.0);
    let stride = depth / (steps * ray.y);
    let sun = vec3(cloud.offset_sun.z, cloud.radiance.w, cloud.offset_sun.w);
    // Spatial jitter is time-independent: no random flicker between paused frames.
    let jitter = fract(52.9829189 * fract(dot(in.position.xy, vec2(0.06711056, 0.00583715))));
    var transmittance = 1.0;
    let ambient = sky_fill();
    var light_sum = vec3(0.0);
    for (var step = 0u; step < 32u; step += 1u) {
        if f32(step) >= steps { break; }
        let position = view.world_position + ray * (entry + (f32(step) + jitter) * stride);
        let amount = field(position, depth);
        let alpha = 1.0 - exp(-amount * stride * 0.008 * cloud.layer.z);
        // Two bounded sunward samples approximate self-shadowing, not multiple scattering.
        let occlusion = field(position + sun * depth * 0.25, depth)
            + field(position + sun * depth * 0.6, depth);
        let lighting = ambient + cloud.radiance.rgb * (0.78 * exp(-occlusion * 2.0));
        light_sum += transmittance * alpha * lighting;
        transmittance *= 1.0 - alpha;
        if transmittance < 0.015 { break; }
    }
    let opacity = 1.0 - transmittance;
    if opacity < 0.002 { discard; }
    let color = (light_sum / opacity) * view.exposure;
    return vec4(color, opacity * smoothstep(0.08, 0.28, ray.y));
}
