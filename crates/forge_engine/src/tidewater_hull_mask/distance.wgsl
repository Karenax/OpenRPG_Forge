// SPDX-License-Identifier: MIT
// Tidewater SceneRenderer.js 1438b1ab: nearest closed-volume surface distance.
#import bevy_render::view::View
@group(0) @binding(0) var<uniform> view: View;
struct Object { world_from_local: mat4x4<f32>, };
@group(1) @binding(0) var<uniform> object: Object;
struct Output {
    @builtin(position) clip: vec4<f32>,
    @location(0) world: vec3<f32>,
};
@vertex
fn vertex(@location(0) position: vec3<f32>) -> Output {
    var out: Output;
    out.world = (object.world_from_local * vec4<f32>(position, 1.0)).xyz;
    out.clip = view.clip_from_world * vec4<f32>(out.world, 1.0);
    return out;
}
@fragment
fn fragment(in: Output) -> @location(0) f32 {
    return length(in.world - view.world_position);
}
