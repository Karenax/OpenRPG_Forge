// SPDX-License-Identifier: MIT
// Source-derived aerial perspective from Tidewater AirHaze.js and Atmosphere.js.

#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput
#import forge::tidewater_air_haze_core::haze_layer_depth

struct HazeSettings {
    camera_world: mat4x4<f32>,
    projection: vec4<f32>,
    control: vec4<f32>,
    atmosphere: array<vec4<f32>, 4>,
}

@group(0) @binding(0) var scene_color: texture_2d<f32>;
@group(0) @binding(1) var scene_depth: texture_depth_2d;
@group(0) @binding(2) var sky_view: texture_2d<f32>;
@group(0) @binding(3) var sky_sampler: sampler;
@group(0) @binding(4) var<uniform> haze: HazeSettings;

const PI: f32 = 3.141592653589793;
const RG: f32 = 6360.0;
const MARINE_SIGMA: f32 = 1.5e-4;
const MARINE_H: f32 = 110.0;
const AEROSOL_SIGMA: f32 = 3.2e-5;
const AEROSOL_H: f32 = 1400.0;

fn horizon_sky(direction: vec3<f32>) -> vec3<f32> {
    let dir = normalize(vec3(direction.x, max(direction.y, 0.02), direction.z));
    let view_height = haze.atmosphere[1].w;
    let horizon = sqrt(max(view_height * view_height - RG * RG, 0.0));
    let beta = acos(clamp(horizon / view_height, -1.0, 1.0));
    let zenith_horizon = PI - beta;
    let angle = acos(clamp(dir.y, -1.0, 1.0));
    let va = (1.0 - sqrt(max(1.0 - angle / zenith_horizon, 0.0))) * 0.5;
    let vb = sqrt(max((angle - zenith_horizon) / beta, 0.0)) * 0.5 + 0.5;
    let v = select(vb, va, angle < zenith_horizon);
    let sun = normalize(haze.atmosphere[3].xz + vec2(1e-5, 0.0));
    let horizontal = normalize(dir.xz + vec2(1e-5, 0.0));
    let u = sqrt(clamp(dot(sun, horizontal) * -0.5 + 0.5, 0.0, 1.0));
    let uv = vec2(u * (191.0 / 192.0) + 0.5 / 192.0,
                  v * (107.0 / 108.0) + 0.5 / 108.0);
    return textureSampleLevel(sky_view, sky_sampler, uv, 0.0).rgb;
}

// This pass follows Bevy's exposure and ACES stage so it cannot redirect the
// fixed-texture auto-exposure histogram. Map the atmosphere's HDR LUT through
// the same ACES fit before compositing it into the display-linear scene.
fn display_fog(hdr: vec3<f32>) -> vec3<f32> {
    let input_matrix = mat3x3<f32>(
        vec3(0.59719, 0.35458, 0.04823),
        vec3(0.07600, 0.90834, 0.01566),
        vec3(0.02840, 0.13383, 0.83777),
    );
    let output_matrix = mat3x3<f32>(
        vec3(1.60475, -0.53108, -0.07367),
        vec3(-0.10208, 1.10813, -0.00605),
        vec3(-0.00327, -0.07276, 1.07602),
    );
    let transformed = max(hdr, vec3(0.0)) * input_matrix;
    let a = transformed * (transformed + 0.0245786) - 0.000090537;
    let b = transformed * (0.983729 * transformed + 0.4329510) + 0.238081;
    return clamp((a / b) * output_matrix, vec3(0.0), vec3(1.0));
}

@fragment
fn fragment(input: FullscreenVertexOutput) -> @location(0) vec4<f32> {
    let pixel = vec2<i32>(input.position.xy);
    let color = textureLoad(scene_color, pixel, 0);
    let depth = textureLoad(scene_depth, pixel, 0);
    if haze.control.z > 0.5 && depth < 1e-7 {
        return vec4(0.0, 0.0, 1.0, 1.0);
    }
    if depth < 1e-7 { return color; }
    let uv = input.uv;
    let ray = vec3((uv.x * 2.0 - 1.0) / haze.projection.x,
                   ((1.0 - uv.y) * 2.0 - 1.0) / haze.projection.y, -1.0);
    // Reverse-Z: depth = -A + B / distance_along_view_axis. For an
    // infinite projection A is zero; finite projections require both terms.
    let distance = min(haze.projection.w / (depth + haze.control.w) * length(ray), 60000.0);
    if haze.control.z > 0.5 {
        return vec4(clamp(depth * 1000.0, 0.0, 1.0), clamp(distance / 2000.0, 0.0, 1.0), 0.0, 1.0);
    }
    let direction = normalize((haze.camera_world * vec4(ray, 0.0)).xyz);
    let tau = (haze_layer_depth(MARINE_SIGMA, MARINE_H, haze.projection.z, direction.y, distance) +
               haze_layer_depth(AEROSOL_SIGMA, AEROSOL_H, haze.projection.z, direction.y, distance))
              * haze.control.x;
    let transmit = exp(-tau);
#ifdef TIDEWATER_SOURCE_HDR
    let fog = horizon_sky(direction);
#else
    let fog = display_fog(horizon_sky(direction) * 1.3195079);
#endif
    return vec4(color.rgb * transmit + fog * (1.0 - transmit), color.a);
}
