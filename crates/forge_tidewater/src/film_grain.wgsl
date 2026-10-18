// SPDX-License-Identifier: MIT
// Source-derived film grain from Tidewater src/post/PostFX.js at 1438b1ab.

#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput

struct GrainSettings {
    intensity: vec4<f32>,
    frame: vec4<u32>,
}

@group(0) @binding(0) var scene_color: texture_2d<f32>;
@group(0) @binding(1) var<uniform> grain: GrainSettings;

fn post_hash(p: vec2<u32>, frame: u32) -> f32 {
    var x = p.x * 1664525u + p.y * 1013904223u + frame * 2654435761u;
    x = x ^ (x >> 16u);
    x = x * 0x7feb352du;
    x = x ^ (x >> 15u);
    x = x * 0x846ca68bu;
    x = x ^ (x >> 16u);
    return f32(x >> 8u) / 16777216.0;
}

@fragment
fn fragment(input: FullscreenVertexOutput) -> @location(0) vec4<f32> {
    let pixel = vec2<u32>(input.position.xy);
    let color = textureLoad(scene_color, vec2<i32>(pixel), 0);
    let noise = (post_hash(pixel, grain.frame.x) +
        post_hash(pixel + vec2<u32>(7919u, 104729u), grain.frame.x) - 1.0) * 0.5;
    return vec4<f32>(color.rgb + color.rgb * (noise * grain.intensity.x), color.a);
}
