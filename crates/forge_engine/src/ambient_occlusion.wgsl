// SPDX-License-Identifier: MIT
// GTAO horizon search and closed-form cosine integral adapted from Tidewater GTAO.js.

#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput

struct GtaoSettings {
    clip_from_view: mat4x4<f32>,
    view_from_clip: mat4x4<f32>,
    control: vec4<f32>,
    options: vec4<f32>,
}

@group(0) @binding(0) var scene_depth: texture_2d<f32>;
@group(0) @binding(1) var<uniform> gtao: GtaoSettings;

const PI: f32 = 3.141592653589793;
const DIRECTIONS: i32 = 3;
const STEPS: i32 = 4;

fn load_depth(pixel: vec2<i32>) -> f32 {
    let dims = vec2<i32>(textureDimensions(scene_depth));
    return textureLoad(scene_depth, clamp(pixel, vec2<i32>(0), dims - 1), 0).r;
}

fn sample_depth(uv: vec2<f32>) -> f32 {
    let dims = vec2<f32>(textureDimensions(scene_depth));
    return load_depth(vec2<i32>(clamp(uv, vec2<f32>(0.0), vec2<f32>(0.999999)) * dims));
}

fn view_position(uv: vec2<f32>, depth: f32) -> vec3<f32> {
    let ndc = vec2<f32>(uv.x * 2.0 - 1.0, (1.0 - uv.y) * 2.0 - 1.0);
    let v = gtao.view_from_clip * vec4<f32>(ndc, depth, 1.0);
    return v.xyz / max(abs(v.w), 1e-6) * select(-1.0, 1.0, v.w >= 0.0);
}

fn screen_from_clip(clip: vec4<f32>) -> vec2<f32> {
    let s = clip.xy / max(abs(clip.w), 1e-6) * select(-1.0, 1.0, clip.w >= 0.0) * 0.5 + 0.5;
    return vec2<f32>(s.x, 1.0 - s.y);
}

fn normal_from_depth(uv: vec2<f32>) -> vec3<f32> {
    let size = vec2<f32>(textureDimensions(scene_depth));
    let p = vec2<i32>(uv * size);
    let center_depth = load_depth(p);
    let l2 = load_depth(p - vec2<i32>(2, 0));
    let l1 = load_depth(p - vec2<i32>(1, 0));
    let r1 = load_depth(p + vec2<i32>(1, 0));
    let r2 = load_depth(p + vec2<i32>(2, 0));
    let b2 = load_depth(p + vec2<i32>(0, 2));
    let b1 = load_depth(p + vec2<i32>(0, 1));
    let t1 = load_depth(p - vec2<i32>(0, 1));
    let t2 = load_depth(p - vec2<i32>(0, 2));
    let dl = abs((2.0 * l1 - l2) - center_depth);
    let dr = abs((2.0 * r1 - r2) - center_depth);
    let db = abs((2.0 * b1 - b2) - center_depth);
    let dt = abs((2.0 * t1 - t2) - center_depth);
    let ce = view_position(uv, center_depth);
    let dx = select(
        -ce + view_position(uv + vec2<f32>(1.0 / size.x, 0.0), r1),
        ce - view_position(uv - vec2<f32>(1.0 / size.x, 0.0), l1),
        dl < dr,
    );
    let dy = select(
        -ce + view_position(uv - vec2<f32>(0.0, 1.0 / size.y), t1),
        ce - view_position(uv + vec2<f32>(0.0, 1.0 / size.y), b1),
        db < dt,
    );
    return normalize(cross(dx, dy) + vec3<f32>(0.0, 0.0, 1e-6));
}

fn noise_at(pixel: vec2<f32>) -> f32 {
    let tile = vec2<f32>(vec2<i32>(pixel) % vec2<i32>(5));
    return fract(sin(dot(tile, vec2<f32>(12.9898, 78.233))) * 43758.5453);
}

@fragment
fn ao(input: FullscreenVertexOutput) -> @location(0) vec4<f32> {
    let uv = input.uv;
    // The source first copies one opaque depth sample for each AO pixel.
    let depth = load_depth(vec2<i32>(input.position.xy));
    if depth < 1e-7 { return vec4<f32>(1.0); }

    let position = view_position(uv, depth);
    let normal = normal_from_depth(uv);
    let view_dir = normalize(-position);
    let clip_position = gtao.clip_from_view * vec4<f32>(position, 1.0);
    let radius = gtao.control.y;
    let inv_radius = 1.0 / radius;
    let angle_noise = noise_at(input.position.xy) * PI;
    let jitter = noise_at(input.position.yx + vec2<f32>(2.0, 3.0)) * 0.75;
    var occlusion = 0.0;

    for (var direction_index = 0; direction_index < DIRECTIONS; direction_index++) {
        let angle = f32(direction_index) / f32(DIRECTIONS) * PI + angle_noise;
        let sample_dir = vec3<f32>(cos(angle), sin(angle), 0.0);
        let clip_radius = gtao.clip_from_view * vec4<f32>(sample_dir * radius, 0.0);
        let bitangent = normalize(cross(sample_dir, view_dir));
        let tangent = cross(bitangent, view_dir);
        let projected_normal_raw = normal - bitangent * dot(normal, bitangent);
        let projected_normal_len = length(projected_normal_raw);
        let projected_normal = projected_normal_raw / max(projected_normal_len, 1e-4);
        let normal_sin = dot(projected_normal, tangent);
        let normal_cos = clamp(dot(projected_normal, view_dir), 0.0, 1.0);
        let angle_normal = select(-1.0, 1.0, normal_sin >= 0.0) * acos(normal_cos);
        let horizon_to_normal = cross(projected_normal, bitangent);
        let cosine_horizon = dot(view_dir, horizon_to_normal);
        var horizons = vec2<f32>(cosine_horizon, -cosine_horizon);

        for (var step_index = 0; step_index < STEPS; step_index++) {
            let step_t = (f32(step_index) + 1.0 + jitter) / f32(STEPS);
            let offset = clip_radius * step_t * step_t;
            let uv_positive = screen_from_clip(clip_position + offset);
            let uv_negative = screen_from_clip(clip_position - offset);
            let delta_positive = view_position(uv_positive, sample_depth(uv_positive)) - position;
            let delta_negative = view_position(uv_negative, sample_depth(uv_negative)) - position;
            let len_positive = length(delta_positive);
            let len_negative = length(delta_negative);
            let horizon_positive = dot(view_dir, delta_positive) / max(len_positive, 1e-4);
            let horizon_negative = dot(view_dir, delta_negative) / max(len_negative, 1e-4);
            let weight_positive = min(len_positive * inv_radius, 1.0);
            let weight_negative = min(len_negative * inv_radius, 1.0);
            if abs(delta_positive.z) < gtao.control.z {
                horizons.x = mix(max(horizons.x, horizon_positive), horizons.x,
                                 weight_positive * weight_positive);
            }
            if abs(delta_negative.z) < gtao.control.z {
                horizons.y = mix(max(horizons.y, horizon_negative), horizons.y,
                                 weight_negative * weight_negative);
            }
        }

        let h_positive = acos(clamp(horizons.y, -1.0, 1.0));
        let h_negative = -acos(clamp(horizons.x, -1.0, 1.0));
        let term_positive = -cos(h_positive * 2.0 - angle_normal)
                            + normal_cos + h_positive * 2.0 * normal_sin;
        let term_negative = -cos(h_negative * 2.0 - angle_normal)
                            + normal_cos + h_negative * 2.0 * normal_sin;
        occlusion += projected_normal_len * (term_positive + term_negative) * 0.25;
    }
    let visibility = pow(clamp(occlusion / f32(DIRECTIONS), 0.0, 1.0), gtao.control.w);
    return vec4<f32>(visibility, 0.0, 0.0, 1.0);
}
