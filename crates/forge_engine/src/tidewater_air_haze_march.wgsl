// SPDX-License-Identifier: MIT
// Derived from Tidewater AirHaze.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput
#import forge::tidewater_air_haze_core::{HazeShadowSettings, haze_march_jitter, haze_march_distance, haze_visibility, haze_march}
#import forge::tidewater_air_haze_volume::{HazeCamera, HazeVolume, HazeLighting, haze_key_sea, haze_view_ray}

struct HazeLiveShadow {
    settings: HazeShadowSettings,
    control: vec4f,
}

@group(0) @binding(0) var scene_depth: texture_depth_2d;
@group(0) @binding(1) var<uniform> camera: HazeCamera;
@group(0) @binding(2) var<uniform> volume: HazeVolume;
@group(0) @binding(3) var object_depth: texture_depth_2d_array;
@group(0) @binding(4) var hill_shadow: texture_2d<f32>;
@group(0) @binding(5) var cloud_shadow: texture_2d<f32>;
@group(0) @binding(6) var<storage, read> live_shadow: HazeLiveShadow;
@group(0) @binding(7) var<storage, read> source_light: HazeLighting;

// Half-output-resolution RGBA16Float: shadowed depth, unshadowed depth,
// full-resolution view distance, one. The render adapter must prove the live
// receiver and depth layers ready together; no neutral texture substitutes.
@fragment
fn fragment(input: FullscreenVertexOutput) -> @location(0) vec4f {
    let ray = haze_view_ray(input.uv, scene_depth, camera);
    let enabled = volume.control.z > 0.5 && source_light.sky_immersion.w < 0.5 && volume.control.y > 0.0;
    if (!enabled) { return vec4f(0.0, 0.0, ray.distance, 1.0); }
    let origin = camera.camera_world[3].xyz;
    let key_sea = haze_key_sea(source_light);
    let forward = -camera.camera_world[2].xyz;
    let jitter = haze_march_jitter(input.position.xy, volume.control.w);
    var visibility: array<f32, 16>;
    for (var i = 0u; i < 16u; i++) {
        let position = origin + ray.direction * haze_march_distance(i, jitter, ray.distance);
        visibility[i] = haze_visibility(position, origin, forward, key_sea.xyz,
            object_depth, live_shadow.settings, hill_shadow, volume.terrain_domain,
            cloud_shadow, volume.cloud_rectangle);
    }
    return haze_march(origin, ray.direction, key_sea.w, ray.distance,
                      volume.control.x, jitter, visibility, enabled);
}
