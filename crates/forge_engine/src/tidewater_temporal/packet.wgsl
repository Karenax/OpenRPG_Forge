// SPDX-License-Identifier: MIT
// Adapted from Tidewater MeshShader.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.

// Must match frame::Packet: 336 bytes, four matrices followed by five vec4s.
struct TidewaterTemporalPacket {
    current_unjittered_clip_from_world: mat4x4<f32>,
    previous_unjittered_clip_from_world: mat4x4<f32>,
    current_world_from_jittered_clip: mat4x4<f32>,
    previous_world_from_jittered_clip: mat4x4<f32>,
    camera_position: vec4<f32>,
    previous_camera_position: vec4<f32>,
    sizes: vec4<u32>,
    near_far_jitter: vec4<f32>,
    phase_epoch_seed: vec4<u32>,
};
@group(1) @binding(0) var<uniform> tidewater_temporal_packet: TidewaterTemporalPacket;

struct TidewaterAuxiliaryOutput {
    @location(0) velocity: vec4<f32>,
    @location(1) mask: vec4<f32>,
};

// Source UV current-minus-previous; framebuffer UV has downward-positive Y.
// The alpha and weight are the source late-material coverage, not clamped here.
fn tidewater_auxiliary_clips(current: vec4<f32>, previous: vec4<f32>,
    alpha: f32, weight: f32, mask: vec4<f32>) -> TidewaterAuxiliaryOutput {
    let velocity = (current.xy / current.w - previous.xy / previous.w) * vec2<f32>(0.5, -0.5);
    let coverage = alpha * weight;
    var output: TidewaterAuxiliaryOutput;
    output.velocity = vec4<f32>(velocity * coverage, 0.0, coverage);
    output.mask = mask;
    return output;
}

fn tidewater_auxiliary_world(current: vec3<f32>, previous: vec3<f32>,
    alpha: f32, weight: f32, mask: vec4<f32>) -> TidewaterAuxiliaryOutput {
    // Flag 1 is history seeding; flag 2 explicitly resets rigid object history.
    // Only the Render-owned first/reset/recovery frame opts in. A resize alone
    // keeps the real previous model, and camera-only source families stay equal.
    var previous_world = previous;
    if (tidewater_temporal_packet.phase_epoch_seed.w & 2u) != 0u {
        previous_world = current;
    }
    return tidewater_auxiliary_clips(
        tidewater_temporal_packet.current_unjittered_clip_from_world * vec4<f32>(current, 1.0),
        tidewater_temporal_packet.previous_unjittered_clip_from_world * vec4<f32>(previous_world, 1.0),
        alpha, weight, mask);
}
