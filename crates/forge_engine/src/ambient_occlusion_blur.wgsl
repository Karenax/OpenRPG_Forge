// SPDX-License-Identifier: MIT
// Separable five-tap depth-aware GTAO blur adapted from Tidewater src/post/PostFX.js.

#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput

@group(0) @binding(0) var ao_source: texture_2d<f32>;
@group(0) @binding(1) var ao_depth: texture_2d<f32>;

fn pixel_depth(pixel: vec2<i32>) -> f32 {
    let size = vec2<i32>(textureDimensions(ao_depth));
    return textureLoad(ao_depth, clamp(pixel, vec2<i32>(0), size - 1), 0).r;
}

fn bilateral(pixel: vec2<i32>, direction: vec2<i32>) -> vec4<f32> {
    let size = vec2<i32>(textureDimensions(ao_source));
    let center = pixel_depth(pixel);
    var total = 0.0;
    var weight_total = 0.0;
    for (var i = -2; i <= 2; i++) {
        let sample_pixel = clamp(pixel + direction * i, vec2<i32>(0), size - 1);
        let sample_depth = pixel_depth(sample_pixel);
        let relative = abs(sample_depth - center) / max(center, 1e-7);
        let weight = 1.0 / pow(relative * 40.0 + 1.0, 2.0);
        total += textureLoad(ao_source, sample_pixel, 0).r * weight;
        weight_total += weight;
    }
    return vec4<f32>(total / max(weight_total, 1e-6), 0.0, 0.0, 1.0);
}

@fragment
fn blur_x(input: FullscreenVertexOutput) -> @location(0) vec4<f32> {
    return bilateral(vec2<i32>(input.position.xy), vec2<i32>(1, 0));
}

@fragment
fn blur_y(input: FullscreenVertexOutput) -> @location(0) vec4<f32> {
    return bilateral(vec2<i32>(input.position.xy), vec2<i32>(0, 1));
}
