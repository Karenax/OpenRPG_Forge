// SPDX-License-Identifier: MIT
// Derived from Tidewater AirHaze.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput
#import forge::tidewater_air_haze_core::{haze_radial_uv, haze_radial_accumulate}
#import forge::tidewater_air_haze_volume::{HazeVolume, HazeCamera, HazeLighting, haze_screen}

@group(0) @binding(0) var source: texture_2d<f32>;
@group(0) @binding(1) var linear_clamp: sampler;
@group(0) @binding(2) var<uniform> volume: HazeVolume;
@group(0) @binding(3) var<uniform> camera: HazeCamera;
@group(0) @binding(4) var<storage, read> source_light: HazeLighting;

// Three quarter-output-resolution R16Float passes, each reading its predecessor.
// The adapter supplies one immutable uniform slice per pass; rewriting a shared
// buffer three times before submit would make all passes read the final index.
@fragment
fn fragment(input: FullscreenVertexOutput) -> @location(0) vec4f {
    let screen = haze_screen(camera, volume, source_light);
    if (screen.z <= 0.001) { return vec4f(0.0); }
    let pass_index = u32(volume.screen.w);
    var samples: array<f32, 8>;
    for (var tap = 0u; tap < 8u; tap++) {
        let uv = haze_radial_uv(input.uv, screen.xy, pass_index, tap);
        samples[tap] = textureSampleLevel(source, linear_clamp, uv, 0.0).x;
    }
    return vec4f(haze_radial_accumulate(samples, pass_index), 0.0, 0.0, 1.0);
}
