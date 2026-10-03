// SPDX-License-Identifier: MIT
// Adapted from Tidewater SkyShader.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
// packet.wgsl is prepended by shaders::sky(). Draw behind geometry at reversed depth 0.

@vertex
fn vertex(@builtin(vertex_index) index: u32) -> @builtin(position) vec4<f32> {
    let uv = vec2<f32>(f32((index << 1u) & 2u), f32(index & 2u));
    return vec4<f32>(uv * 2.0 - 1.0, 0.0, 1.0);
}

@fragment
fn fragment(@builtin(position) pixel: vec4<f32>) -> TidewaterAuxiliaryOutput {
    let uv = pixel.xy / vec2<f32>(tidewater_temporal_packet.sizes.xy);
    let ndc = uv * vec2<f32>(2.0, -2.0) + vec2<f32>(-1.0, 1.0);
    let world_h = tidewater_temporal_packet.current_world_from_jittered_clip * vec4<f32>(ndc, 0.0, 1.0);
    // Homogeneous direction works with finite and infinite far projections.
    let direction = world_h.xyz - world_h.w * tidewater_temporal_packet.camera_position.xyz;
    let c = tidewater_temporal_packet.current_unjittered_clip_from_world * vec4<f32>(direction, 0.0);
    let p = tidewater_temporal_packet.previous_unjittered_clip_from_world * vec4<f32>(direction, 0.0);
    // Sky.js guards a direction that leaves either camera's forward hemisphere.
    let current = c.xy / max(abs(c.w), 1e-6) * sign(c.w);
    let previous = p.xy / max(abs(p.w), 1e-6) * sign(p.w);
    var output: TidewaterAuxiliaryOutput;
    output.velocity = vec4<f32>(select(vec2<f32>(0.0), (current - previous) * vec2<f32>(0.5, -0.5), c.w > 1e-6 && p.w > 1e-6), 0.0, 1.0);
    output.mask = vec4<f32>(0.0);
    return output;
}
