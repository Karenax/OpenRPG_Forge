// SPDX-License-Identifier: MIT
// Derived from Tidewater AirHaze.js / Atmosphere.js / Sky.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput
#import forge::tidewater_air_haze_core::{HazeCompositeInput, haze_upsample, haze_composite}
#import forge::tidewater_air_haze_volume::{HazeCamera, HazeVolume, HazeLighting, haze_view_ray, haze_screen}

struct HazeMoon {
    // Actual SkyParams.moonDir XYZ and Frame.night W from this accepted frame.
    direction_night: vec4f,
}

@group(0) @binding(0) var scene_color: texture_2d<f32>;
@group(0) @binding(1) var scene_depth: texture_depth_2d;
@group(0) @binding(2) var sky_view: texture_2d<f32>;
@group(0) @binding(3) var linear_clamp: sampler;
@group(0) @binding(4) var<uniform> camera: HazeCamera;
@group(0) @binding(5) var<uniform> volume: HazeVolume;
@group(0) @binding(6) var<storage, read> source_light: HazeLighting;
@group(0) @binding(7) var march_result: texture_2d<f32>;
@group(0) @binding(8) var screen_rays: texture_2d<f32>;
// Actual near-clip lens-medium query, not surface-water alpha or a depth mask.
@group(0) @binding(9) var lens_medium: texture_2d<f32>;
@group(0) @binding(10) var<uniform> moon: HazeMoon;

fn haze_atmosphere_sky(dir: vec3f) -> vec3f {
    let view_height = camera.atmosphere[1].w;
    let horizon = sqrt(max(view_height * view_height - 6360.0 * 6360.0, 0.0));
    let beta = acos(horizon / view_height);
    let zenith_horizon = 3.141592653589793 - beta;
    let angle = acos(clamp(dir.y, -1.0, 1.0));
    let va = (1.0 - sqrt(max(1.0 - angle / zenith_horizon, 0.0))) * 0.5;
    let vb = sqrt(max((angle - zenith_horizon) / beta, 0.0)) * 0.5 + 0.5;
    let v = select(vb, va, angle < zenith_horizon);
    let sun = normalize(camera.atmosphere[3].xz + vec2f(1e-5, 0.0));
    let horizontal = normalize(dir.xz + vec2f(1e-5, 0.0));
    let u = sqrt(clamp(dot(sun, horizontal) * -0.5 + 0.5, 0.0, 1.0));
    let uv = vec2f(u * 0.9947916666666666 + 0.0026041666666666665,
                   v * 0.9907407407407407 + 0.004629629629629629);
    return textureSampleLevel(sky_view, linear_clamp, uv, 0.0).rgb;
}

fn haze_moon_sky(dir: vec3f) -> vec3f {
    let cosine = dot(dir, moon.direction_night.xyz);
    let angle = acos(clamp(cosine, -1.0, 1.0));
    let aureole = exp(angle * -14.0) * 2.4 + exp(angle * -2.5) * 0.9;
    let gradient = mix(1.7, 1.0, clamp(dir.y * 3.0, 0.0, 1.0));
    let up = smoothstep(-0.05, 0.15, moon.direction_night.y);
    return vec3f(0.005, 0.0068, 0.0105) * (gradient + aureole) * moon.direction_night.w * up;
}

@fragment
fn fragment(input: FullscreenVertexOutput) -> @location(0) vec4f {
    let uv = input.uv;
    let color_pixel = vec2i(clamp(uv, vec2f(0.0), vec2f(0.9999)) * vec2f(textureDimensions(scene_color)));
    let color = textureLoad(scene_color, color_pixel, 0);
    let enabled = volume.control.z > 0.5 && source_light.sky_immersion.w < 0.5;
    if (!enabled) { return color; }
    let medium_pixel = vec2i(clamp(uv, vec2f(0.0), vec2f(0.9999)) * vec2f(textureDimensions(lens_medium)));
    if (textureLoad(lens_medium, medium_pixel, 0).r > 0.5) { return color; }
    let ray = haze_view_ray(uv, scene_depth, camera);
    let horizon_dir = normalize(vec3f(ray.direction.x, max(ray.direction.y, 0.02), ray.direction.z));
    var composite: HazeCompositeInput;
    composite.color = color;
    composite.fog = haze_atmosphere_sky(horizon_dir) + haze_moon_sky(horizon_dir);
    composite.sun_color = source_light.sun_color.xyz;
    composite.sun_cosine = dot(ray.direction, source_light.sun_sea.xyz);
    composite.sky_irradiance = source_light.sky_immersion.xyz;
    composite.camera_height = max(camera.camera_world[3].y - source_light.sun_sea.w, 0.0);
    composite.direction_y = ray.direction.y;
    composite.distance = ray.distance;
    composite.sky = ray.sky;
    composite.density = volume.control.x;
    composite.shafts = volume.control.y;
    composite.lit_all = vec2f(0.0);
    if (composite.shafts > 0.0) {
        let size = vec2f(textureDimensions(march_result));
        let st = uv * size - 0.5;
        let base = vec2i(floor(st));
        var samples: array<vec4f, 4>;
        for (var k = 0u; k < 4u; k++) {
            let offset = vec2i(i32(k & 1u), i32(k >> 1u));
            let pixel = clamp(base + offset, vec2i(0), vec2i(size) - 1);
            samples[k] = textureLoad(march_result, pixel, 0);
        }
        composite.lit_all = haze_upsample(fract(st), ray.distance, samples);
    }
    composite.rays = 0.0;
    composite.screen_fade = haze_screen(camera, volume, source_light).z;
    if (composite.screen_fade > 0.001) {
        composite.rays = textureSampleLevel(screen_rays, linear_clamp, uv, 0.0).r;
    }
    composite.enabled = true;
    composite.in_air = true;
    return haze_composite(composite);
}
