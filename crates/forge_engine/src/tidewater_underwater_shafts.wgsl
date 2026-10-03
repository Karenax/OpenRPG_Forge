// SPDX-License-Identifier: MIT
// Adapted from Tidewater src/post/Underwater.js and src/ocean/Caustics.js.
// Copyright 2026 DRG Software Solutions LLC; pinned revision 1438b1ab.
// The full optical module supplies frame and seaDetailSample. No torch yet.
@group(3) @binding(0) var shaft_fine: texture_2d<f32>;
@group(3) @binding(1) var shaft_broad: texture_2d<f32>;
@group(3) @binding(2) var shaft_sampler: sampler;
@group(3) @binding(3) var<uniform> shaft_params: vec4f;

fn shaft_caustic(p: vec3f, depth: f32, detail_k: f32) -> vec3f {
    let light = refract(-normalize(settings.sun_direction.xyz), vec3f(0.0, 1.0, 0.0), 1.0 / IOR);
    let entry = p.xz - light.xz * (depth / max(-light.y, 0.15));
    let fine = textureSampleLevel(shaft_fine, shaft_sampler, entry / shaft_params.x, 1.5).xy;
    let broad = textureSampleLevel(shaft_broad, shaft_sampler, entry / shaft_params.y, 1.5).xy;
    let fine_value = mix(fine.x, fine.y, clamp((depth - 1.2) / 2.8, 0.0, 1.0));
    let broad_value = mix(broad.x, broad.y, clamp(depth / 9.0, 0.0, 1.0));
    let intensity = fine_value * mix(1.0, broad_value, 0.6);
    let strength = smoothstep(0.03, 0.5, depth) * exp(-0.06 * depth) * shaft_params.z * detail_k;
    return mix(vec3f(1.0), vec3f(intensity), strength);
}

@fragment
fn shaft_fragment(input: FullscreenVertexOutput) -> @location(0) vec4f {
    setupUnderwaterHeightFrame();
    let uv = input.uv;
    let size = vec2i(textureDimensions(scene_depth));
    let pixel = clamp(vec2i(uv * vec2f(size)), vec2i(0), size - 1);
    let depth = textureLoad(scene_depth, pixel, 0);
    let ray = view_ray(uv);
    let distance = max(min(length(ray * -view_z(depth)), 600.0) - settings.state.y * length(ray), 0.0);
    let direction = normalize((settings.camera_world * vec4f(ray, 0.0)).xyz);
    let scattering = settings.scattering.xyz;
    let extinction = settings.absorption.xyz + scattering;
    let light = -refract(-normalize(settings.sun_direction.xyz), vec3f(0.0, 1.0, 0.0), 1.0 / IOR);
    let mu = max(light.y, 0.15);
    let g = 0.85;
    let phase = (1.0 - g * g) / (4.0 * UNDERWATER_PI)
        / pow(max(1.0 + g * g - dot(direction, light) * 2.0 * g, 1e-4), 1.5) * 0.75
        + 0.25 / (4.0 * UNDERWATER_PI);
    let max_distance = min(distance, 22.0);
    let ds = max_distance / 20.0;
    let noise_pixel = input.position.xy + f32(frame.frameIndex % 64u) * 5.588238;
    let jitter = fract(fract(dot(noise_pixel, vec2f(0.06711056, 0.00583715))) * 52.9829189);
    let camera = settings.camera_world[3].xyz;
    let detail = seaDetailSample(camera.xz + direction.xz * (max_distance * 0.5));
    let detail_k = mix(0.55, 1.25, detail.gust) * (1.0 - detail.slick * 0.6);
    var shafts = vec3f(0.0);
    for (var i = 0; i < 20; i++) {
        let s = (f32(i) + jitter) * ds;
        let p = camera + direction * s;
        let water_depth = max(water_queries.results[0].x - p.y, 0.01);
        let caustic = shaft_caustic(p, water_depth, detail_k);
        let transmittance = exp(-extinction * (s + water_depth / mu));
        shafts += (caustic - 1.0) * transmittance * ds;
    }
    shafts *= settings.sun_color.xyz * 0.96 * scattering * phase * settings.projection.w * 2.5;
    return vec4f(max(shafts, vec3f(0.0)), distance);
}
