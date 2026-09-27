// Forge adapter calling Bevy 0.19.1's existing MIT/Apache-2.0 raymarch module.
// No upstream shader implementation or external texture is copied here.
#import bevy_pbr::{
    mesh_view_bindings::{view, depth_prepass_texture, view_transmission_texture},
    view_transformations::position_world_to_ndc,
    raymarch::{depth_ray_march_new_from_depth, depth_ray_march_from_cs,
        depth_ray_march_to_ws, depth_ray_march_march},
}

// RGB is already exposed scene radiance; A is confidence, not surface opacity.
fn water_scene_reflection(P: vec3<f32>, N: vec3<f32>, V: vec3<f32>,
    roughness: f32, settings: vec4<f32>) -> vec4<f32> {
    let dims = vec2<f32>(textureDimensions(depth_prepass_texture));
    // Bevy's marcher uses full-target UVs. Until an offset-aware adapter is
    // qualified, refuse subviewports rather than sampling a different viewport.
    if settings.w <= 0.0 || roughness >= 0.4
        || any(view.viewport.xy != vec2(0.0)) || any(view.viewport.zw != dims) {
        return vec4(0.0);
    }
    let direction = reflect(-V, N);
    var trace = depth_ray_march_new_from_depth(dims);
    depth_ray_march_from_cs(&trace, position_world_to_ndc(P + N * 0.02));
    depth_ray_march_to_ws(&trace, P + direction * settings.y);
    trace.linear_steps = u32(clamp(settings.x, 2.0, 64.0));
    trace.bisection_steps = 4u;
    trace.use_secant = true;
    trace.depth_thickness_linear_z = settings.z;
    let hit = depth_ray_march_march(&trace);
    if !hit.hit {
        return vec4(0.0);
    }
    let edge = min(hit.hit_uv, vec2(1.0) - hit.hit_uv);
    let confidence = smoothstep(0.0, 0.08, min(edge.x, edge.y))
        * (1.0 - smoothstep(0.2, 0.4, roughness));
    let color_dims = vec2<i32>(textureDimensions(view_transmission_texture));
    let pixel = clamp(vec2<i32>(hit.hit_uv * vec2<f32>(color_dims)), vec2(0), color_dims - vec2(1));
    let color = textureLoad(view_transmission_texture, pixel, 0);
    return vec4(color.rgb, confidence * step(0.000001, color.a));
}

// Candidate refraction lookup. The caller must replace (not add to) its existing
// transmitted radiance and use path_m for extinction. A miss keeps that caller's
// explicit approximation. This function alone does not qualify a quality tier.
struct WaterRefractionHit {
    radiance: vec3<f32>,
    path_m: f32,
    valid: bool,
    confidence: f32,
}

fn water_scene_refraction(P: vec3<f32>, N: vec3<f32>, V: vec3<f32>,
    ior: f32, settings: vec4<f32>) -> WaterRefractionHit {
    let miss = WaterRefractionHit(vec3(0.0), 0.0, false, 0.0);
    let dims = vec2<f32>(textureDimensions(depth_prepass_texture));
    if settings.w <= 0.0 || settings.x < 2.0 || settings.y <= 0.0 || ior < 1.0
        || dot(N, V) <= 0.0
        || any(view.viewport.xy != vec2(0.0)) || any(view.viewport.zw != dims) {
        return miss;
    }
    // Air-to-water only. Underwater cameras require a separate interface contract.
    let direction = refract(-V, N, 1.0 / ior);
    if dot(direction, direction) < 0.000001 {
        return miss;
    }
    var trace = depth_ray_march_new_from_depth(dims);
    depth_ray_march_from_cs(&trace, position_world_to_ndc(P));
    depth_ray_march_to_ws(&trace, P + direction * settings.y);
    trace.linear_steps = u32(clamp(settings.x, 2.0, 64.0));
    trace.bisection_steps = 4u;
    trace.use_secant = true;
    trace.depth_thickness_linear_z = settings.z;
    let hit = depth_ray_march_march(&trace);
    if !hit.hit || any(hit.hit_uv <= vec2(0.0)) || any(hit.hit_uv >= vec2(1.0)) {
        return miss;
    }
    let pixel = vec2<i32>(hit.hit_uv * dims);
    let depth = textureLoad(depth_prepass_texture, pixel, 0);
    if depth <= 0.0 {
        return miss;
    }
    // Reconstruct the actual sampled texel centre, not an interpolated hit depth.
    let uv = (vec2<f32>(pixel) + vec2(0.5)) / dims;
    let point = view.world_from_clip * vec4(uv * vec2(2.0, -2.0) + vec2(-1.0, 1.0), depth, 1.0);
    if abs(point.w) < 0.000001 {
        return miss;
    }
    let offset = point.xyz / point.w - P;
    let path = length(offset);
    let along = dot(offset, direction);
    // Reject foreground silhouettes/above-water geometry and samples inconsistent
    // with the refracted ray. No clamped off-screen colour masquerades as a hit.
    if dot(offset, N) >= 0.0 || along <= 0.0 || path > settings.y
        || length(offset - direction * along) > settings.z {
        return miss;
    }
    let color_dims = vec2<i32>(textureDimensions(view_transmission_texture));
    if any(color_dims != vec2<i32>(dims)) {
        return miss;
    }
    let color = textureLoad(view_transmission_texture, pixel, 0);
    if color.a <= 0.0 {
        return miss;
    }
    let edge = min(hit.hit_uv, vec2(1.0) - hit.hit_uv);
    let ray_error = length(offset - direction * along);
    let confidence = smoothstep(0.0, 0.08, min(edge.x, edge.y))
        * (1.0 - smoothstep(settings.y * 0.8, settings.y, path))
        * (1.0 - smoothstep(settings.z * 0.5, settings.z, ray_error))
        * smoothstep(0.0, 0.1, -dot(offset, N));
    return WaterRefractionHit(color.rgb, path, true, confidence);
}
