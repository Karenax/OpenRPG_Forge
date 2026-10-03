// SPDX-License-Identifier: MIT
// Depth-aware GTAO upsample and ambient-only blend adapted from Tidewater PostFX.js.

#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput

struct GtaoSettings {
    clip_from_view: mat4x4<f32>,
    view_from_clip: mat4x4<f32>,
    control: vec4<f32>,
    options: vec4<f32>,
}

@group(0) @binding(0) var scene_color: texture_2d<f32>;
@group(0) @binding(1) var scene_depth: texture_depth_2d;
@group(0) @binding(2) var ao_texture: texture_2d<f32>;
@group(0) @binding(3) var ao_depth: texture_2d<f32>;
@group(0) @binding(4) var<uniform> gtao: GtaoSettings;

fn half_ao_at(pixel: vec2<i32>, depth: f32) -> vec2<f32> {
    let size = vec2<i32>(textureDimensions(ao_texture));
    let p = clamp(pixel, vec2<i32>(0), size - 1);
    let sample_depth = textureLoad(ao_depth, p, 0).r;
    let relative = abs(sample_depth - depth) / max(depth, 1e-7);
    let penalty = relative * 40.0 + 1.0;
    return vec2<f32>(textureLoad(ao_texture, p, 0).r, 1.0 / (penalty * penalty));
}

@fragment
fn composite(input: FullscreenVertexOutput) -> @location(0) vec4<f32> {
    let pixel = vec2<i32>(input.position.xy);
    let color = textureLoad(scene_color, pixel, 0);
    let depth = textureLoad(scene_depth, pixel, 0);
    if depth < 1e-7 || (color.a > 0.125 && color.a < 0.875) {
        return color;
    }

    let size = vec2<f32>(textureDimensions(ao_texture));
    let position = input.uv * size - 0.5;
    let base = vec2<i32>(floor(position));
    let fraction = fract(position);
    var sum = 0.0;
    var weight_sum = 1e-6;
    for (var i = 0; i < 4; i++) {
        let offset = vec2<i32>(i & 1, i >> 1);
        let bilinear = select(1.0 - fraction.x, fraction.x, offset.x == 1)
                       * select(1.0 - fraction.y, fraction.y, offset.y == 1);
        let ao = half_ao_at(base + offset, depth);
        let weight = bilinear * ao.y + 1e-5;
        sum += ao.x * weight;
        weight_sum += weight;
    }
    let visibility = clamp(sum / weight_sum, 0.0, 1.0);
    if gtao.options.x > 0.5 {
        return vec4<f32>(vec3<f32>(visibility), color.a);
    }
    if gtao.control.x <= 0.0 {
        return color;
    }
    let multibounce = max(visibility,
        ((visibility * 0.382 - 1.036) * visibility + 1.654) * visibility);
    let luminance = dot(color.rgb, vec3<f32>(0.2126, 0.7152, 0.0722));
    let ambient_only = 1.0 - smoothstep(0.12, 0.9, luminance);
    let strength = gtao.control.x * mix(0.35, 1.0, ambient_only);
    let attenuation = clamp(mix(1.0, multibounce, strength), 0.0, 1.0);
    return vec4<f32>(color.rgb * attenuation, color.a);
}
