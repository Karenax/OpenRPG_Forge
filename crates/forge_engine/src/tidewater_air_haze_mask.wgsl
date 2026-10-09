// SPDX-License-Identifier: MIT
// Derived from Tidewater AirHaze.js / Clouds.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput
#import forge::tidewater_air_haze_core::haze_sun_mask
#import forge::tidewater_air_haze_volume::{HazeCamera, HazeVolume, HazeLighting, haze_view_ray, haze_screen}
#import forge::tidewater_air_haze_cloud_view::{HazeCloudView, haze_cloud_sample_view}

@group(0) @binding(0) var scene_depth: texture_depth_2d;
@group(0) @binding(1) var<uniform> camera: HazeCamera;
@group(0) @binding(2) var<uniform> volume: HazeVolume;
@group(0) @binding(3) var<storage, read> source_light: HazeLighting;
@group(0) @binding(4) var<uniform> cloud_view: HazeCloudView;
@group(0) @binding(5) var cloud_history: texture_2d<f32>;
@group(0) @binding(6) var cloud_panorama: texture_2d<f32>;
@group(0) @binding(7) var linear_clamp: sampler;
@group(0) @binding(8) var linear_repeat: sampler;

// Quarter-output-resolution R16Float. The adapter must bind the actual traced
// history and its matching basis; the ground cloud-shadow map is not this data.
@fragment
fn fragment(input: FullscreenVertexOutput) -> @location(0) vec4f {
    var result = vec4f(0.0);
    if (haze_screen(camera, volume, source_light).z > 0.001) {
        let ray = haze_view_ray(input.uv, scene_depth, camera);
        let cloud = haze_cloud_sample_view(ray.direction, cloud_view, cloud_history,
            cloud_panorama, linear_clamp, linear_repeat);
        result = vec4f(haze_sun_mask(dot(ray.direction, source_light.sun_sea.xyz), cloud.a, ray.sky), 0.0, 0.0, 1.0);
    }
    return result;
}
