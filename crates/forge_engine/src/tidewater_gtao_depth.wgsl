// SPDX-License-Identifier: MIT
// Half-resolution opaque depth copy adapted from Tidewater src/post/PostFX.js.

#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput

@group(0) @binding(0) var scene_depth: texture_depth_2d;

@fragment
fn half_depth(input: FullscreenVertexOutput) -> @location(0) vec4<f32> {
    let full = vec2<i32>(textureDimensions(scene_depth));
    let pixel = min(vec2<i32>(input.position.xy) * 2, full - 1);
    return vec4<f32>(textureLoad(scene_depth, pixel, 0), 0.0, 0.0, 1.0);
}
