// SPDX-License-Identifier: MIT
// AirHaze.js / common.js at Tidewater 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
// Pure kernels. Visibility, LUT, lens-medium and low-resolution samples are
// supplied by the render adapter; these helpers never substitute neutral maps.
#define_import_path forge::tidewater_air_haze_core

const HZ_PI: f32 = 3.141592653589793;
const HZ_MARINE_SIGMA: f32 = 1.5e-4;
const HZ_MARINE_H: f32 = 110.0;
const HZ_AEROSOL_SIGMA: f32 = 3.2e-5;
const HZ_AEROSOL_H: f32 = 1400.0;

fn haze_layer_depth(sigma: f32, height: f32, camera_height: f32, view_y: f32, distance: f32) -> f32 {
    let base = exp(-camera_height / height) * sigma;
    let k = view_y * distance / height;
    return base * select(height * (1.0 - exp(-k)) / view_y, distance, abs(k) < 1e-3);
}

fn haze_phase(cos_t: f32) -> f32 {
    let g = 0.62;
    let g2 = g * g;
    let cs = 3.0 * (1.0 - g2) / (8.0 * HZ_PI * (2.0 + g2)) * (cos_t * cos_t + 1.0)
        / pow(max(1.0 + g2 - cos_t * 2.0 * g, 1e-4), 1.5);
    return cs * 0.7 + 0.3 / (4.0 * HZ_PI);
}

fn haze_march_jitter(pixel: vec2f, frame: f32) -> f32 {
    let noise = fract(52.9829189 * fract(dot(pixel, vec2f(0.06711056, 0.00583715))));
    return fract(noise + frame * 0.61803398875);
}

// The adapter samples cascades, terrain and clouds at precisely these positions.
// The same quadratic t is used below for integration, not a linear step grid.
fn haze_march_distance(step: u32, jitter: f32, view_distance: f32) -> f32 {
    let u = (f32(step) + jitter) / 16.0;
    return u * u * min(view_distance, 2500.0);
}

// Same field layout as ObjectShadowSettings and the resident live receiver.
// Camera inputs are explicit: the source uses this view's current camera, not
// the camera from a potentially older cascade allocation.
struct HazeShadowSettings {
    matrices: array<mat4x4f, 4>,
    cascades: array<vec4f, 4>,
    blend: array<vec4f, 4>,
    count: u32,
    map_size: f32,
    bias: f32,
    fade: f32,
    enabled: f32,
    pcss_cascades: u32,
    sun_angular_diameter: f32,
    frame_index: u32,
    camera: vec4f,
    forward: vec4f,
}

fn haze_object_visibility(depth_map: texture_depth_2d_array, shadow: HazeShadowSettings,
                          position: vec3f, camera: vec3f, view_forward: vec3f) -> f32 {
    if (shadow.enabled < 0.5) { return 1.0; }
    let distance = dot(position - camera, view_forward);
    var cascade = -1;
    for (var i = 0u; i < shadow.count; i++) {
        if (distance < shadow.cascades[i].x) { cascade = i32(i); break; }
    }
    if (cascade < 0) { return 1.0; }
    let projected = shadow.matrices[cascade] * vec4f(position, 1.0);
    let uv = vec2f(projected.x * 0.5 + 0.5, 0.5 - projected.y * 0.5);
    if (any(uv <= vec2f(0.0)) || any(uv >= vec2f(1.0)) || projected.z > 1.0) { return 1.0; }
    let size = vec2f(textureDimensions(depth_map));
    let pixel = vec2i(clamp(uv * size, vec2f(0.0), size - 1.0));
    let depth = textureLoad(depth_map, pixel, cascade, 0);
    return select(0.0, 1.0, projected.z - 2e-5 <= depth);
}

// domain: world origin X/Z, square size, source sunBaked blend.
fn haze_terrain_visibility(hill_map: texture_2d<f32>, position: vec3f, domain: vec4f) -> f32 {
    let size = f32(textureDimensions(hill_map).x);
    let st = clamp((position.xz - domain.xy) / domain.z * size - 0.5,
                   vec2f(0.0), vec2f(size - 1.001));
    let pixel = vec2i(floor(st));
    let fraction = fract(st);
    let sample = mix(
        mix(textureLoad(hill_map, pixel, 0), textureLoad(hill_map, pixel + vec2i(1, 0), 0), fraction.x),
        mix(textureLoad(hill_map, pixel + vec2i(0, 1), 0), textureLoad(hill_map, pixel + vec2i(1, 1), 0), fraction.x),
        fraction.y);
    let width = sample.y * 0.012 + 0.35;
    return mix(1.0, smoothstep(-width, width, position.y - sample.x), domain.w);
}

fn haze_cloud_tap(shadow_map: texture_2d<f32>, pixel: vec2i) -> f32 {
    return textureLoad(shadow_map, clamp(pixel, vec2i(0), vec2i(textureDimensions(shadow_map)) - 1), 0).x;
}

// rectangle: cloud shadow center X/Z, world size, shadowStrength.
fn haze_cloud_visibility(shadow_map: texture_2d<f32>, world_xz: vec2f, rectangle: vec4f) -> f32 {
    let uv = (world_xz - rectangle.xy) / rectangle.z + 0.5;
    let st = uv * vec2f(textureDimensions(shadow_map)) - 0.5;
    let pixel = vec2i(floor(st));
    let fraction = fract(st);
    let shadow = mix(
        mix(haze_cloud_tap(shadow_map, pixel), haze_cloud_tap(shadow_map, pixel + vec2i(1, 0)), fraction.x),
        mix(haze_cloud_tap(shadow_map, pixel + vec2i(0, 1)), haze_cloud_tap(shadow_map, pixel + vec2i(1, 1)), fraction.x),
        fraction.y);
    let edge = abs(uv - 0.5);
    let inside = smoothstep(0.5, 0.42, max(edge.x, edge.y));
    return mix(1.0, shadow, rectangle.w * inside);
}

fn haze_visibility(position: vec3f, camera: vec3f, view_forward: vec3f, sun_direction: vec3f,
                   object_map: texture_depth_2d_array, shadow: HazeShadowSettings,
                   hill_map: texture_2d<f32>, terrain_domain: vec4f,
                   cloud_map: texture_2d<f32>, cloud_rectangle: vec4f) -> f32 {
    let ground = position.xz - sun_direction.xz * (max(position.y, 0.0) / max(sun_direction.y, 0.08));
    return haze_object_visibility(object_map, shadow, position, camera, view_forward)
        * haze_terrain_visibility(hill_map, position, terrain_domain)
        * haze_cloud_visibility(cloud_map, ground, cloud_rectangle);
}

fn haze_march(camera: vec3f, direction: vec3f, sea_level: f32, view_distance: f32,
              density: f32, jitter: f32, visibility: array<f32, 16>, enabled: bool) -> vec4f {
    if (!enabled) { return vec4f(0.0, 0.0, view_distance, 1.0); }
    let t_max = min(view_distance, 2500.0);
    let sig_m = HZ_MARINE_SIGMA * density;
    let sig_a = HZ_AEROSOL_SIGMA * density;
    var lit = 0.0;
    var all = 0.0;
    var tau = 0.0;
    var previous = 0.0;
    for (var i = 0u; i < 16u; i++) {
        let u = (f32(i) + jitter) / 16.0;
        let t = haze_march_distance(i, jitter, view_distance);
        let dt = u * 0.125 * t_max;
        let position = camera + direction * t;
        let height = max(position.y - sea_level, 0.0);
        let sigma = exp(-height / HZ_MARINE_H) * sig_m + exp(-height / HZ_AEROSOL_H) * sig_a;
        tau += sigma * (t - previous);
        previous = t;
        let weight = sigma * exp(-tau) * dt;
        lit += weight * visibility[i];
        all += weight;
    }
    return vec4f(lit, all, view_distance, 1.0);
}

// Depth-aware bilinear reconstruction of (lit, all), supplied as four texels
// in order (0,0), (1,0), (0,1), (1,1). The adapter clamps coordinates to the LUT.
fn haze_upsample(fraction: vec2f, distance: f32, samples: array<vec4f, 4>) -> vec2f {
    var accumulated = vec2f(0.0);
    var weight_sum = 1e-6;
    for (var k = 0u; k < 4u; k++) {
        let x = k & 1u;
        let y = k >> 1u;
        let sample = samples[k];
        let bilinear = select(1.0 - fraction.x, fraction.x, x == 1u)
            * select(1.0 - fraction.y, fraction.y, y == 1u);
        let relative = abs(sample.z - distance) / max(distance, 0.5);
        let denominator = relative * 10.0 + 1.0;
        let weight = bilinear / (denominator * denominator) + 1e-5;
        accumulated += sample.xy * weight;
        weight_sum += weight;
    }
    return accumulated / weight_sum;
}

struct HazeCompositeInput {
    color: vec4f,
    fog: vec3f,
    sun_color: vec3f,
    sun_cosine: f32,
    sky_irradiance: vec3f,
    camera_height: f32,
    direction_y: f32,
    distance: f32,
    sky: bool,
    density: f32,
    shafts: f32,
    lit_all: vec2f,
    rays: f32,
    screen_fade: f32,
    enabled: bool,
    in_air: bool,
}

fn haze_luminance(color: vec3f) -> f32 {
    return dot(color, vec3f(0.2126, 0.7152, 0.0722));
}

fn haze_composite(input: HazeCompositeInput) -> vec4f {
    if (!input.enabled || !input.in_air) { return input.color; }
    var color = input.color.rgb;
    let ep = input.sun_color * haze_phase(input.sun_cosine);
    let e_l = haze_luminance(ep);
    let f_sun = e_l / (e_l + haze_luminance(input.sky_irradiance) + 1e-5) * min(input.shafts, 1.0);
    let far = select(smoothstep(0.0, 900.0, input.distance), 1.0, input.sky);
    if (!input.sky) {
        let tau = (haze_layer_depth(HZ_MARINE_SIGMA, HZ_MARINE_H, input.camera_height, input.direction_y, input.distance)
            + haze_layer_depth(HZ_AEROSOL_SIGMA, HZ_AEROSOL_H, input.camera_height, input.direction_y, input.distance)) * input.density;
        let transmit = exp(-tau);
        color = color * transmit + input.fog * (1.0 - transmit) * (1.0 - f_sun * (1.0 - far));
    }
    if (input.shafts > 0.0) {
        let lit = input.lit_all.x;
        let all = max(input.lit_all.y, lit);
        let near = ep * lit * (1.0 - far) * input.shafts;
        let deficit = input.fog * f_sun * (all - lit) * far;
        color = max(color + near - deficit, vec3f(0.0));
    }
    if (input.screen_fade > 0.001) {
        let k = 1.0 - exp(-HZ_MARINE_SIGMA * 300.0 * input.density);
        color += ep * input.rays * k * input.shafts * input.screen_fade * 3.5;
    }
    return vec4f(color, input.color.a);
}

fn haze_sun_mask(sun_cosine: f32, cloud_transmittance: f32, sky: bool) -> f32 {
    let glow = exp((sun_cosine - 1.0) * 600.0) + exp((sun_cosine - 1.0) * 50.0) * 0.25;
    return select(0.0, glow * cloud_transmittance, sky);
}

// Eight taps in each of three passes. Constants are the generated source's
// double-precision weights/normalizers, rounded once by WGSL to GPU f32.
const HZ_RADIAL_WEIGHTS: array<vec4f, 6> = array<vec4f, 6>(
    vec4f(1.0, 0.9, 0.81, 0.7290000000000001),
    vec4f(0.6561000000000001, 0.5904900000000001, 0.531441, 0.4782969000000001),
    vec4f(1.0, 0.97, 0.9409, 0.912673),
    vec4f(0.8852928099999999, 0.8587340256999999, 0.8329720049289999, 0.8079828447811298),
    vec4f(1.0), vec4f(1.0));
const HZ_RADIAL_SUM: array<f32, 3> = array<f32, 3>(5.6953279000000006, 7.208554685410129, 8.0);
const HZ_RADIAL_STEP: array<f32, 3> = array<f32, 3>(0.11875, 0.01484375, 0.00185546875);

fn haze_radial_uv(uv: vec2f, sun_uv: vec2f, pass_index: u32, tap: u32) -> vec2f {
    return uv + (sun_uv - uv) * HZ_RADIAL_STEP[pass_index] * f32(tap);
}

fn haze_radial_accumulate(samples: array<f32, 8>, pass_index: u32) -> f32 {
    var accumulated = 0.0;
    for (var tap = 0u; tap < 8u; tap++) {
        accumulated += samples[tap] * HZ_RADIAL_WEIGHTS[pass_index * 2u + tap / 4u][tap % 4u];
    }
    return accumulated / HZ_RADIAL_SUM[pass_index];
}
