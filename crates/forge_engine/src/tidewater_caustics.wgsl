// SPDX-License-Identifier: MIT
// Photon splatting adapted from Tidewater src/ocean/Caustics.js at 1438b1ab.

struct PhotonParams {
    sun_direction: vec4<f32>,
    geometry: vec4<f32>,
    optics: vec4<f32>,
}

@group(0) @binding(0) var ocean_derivatives: texture_2d_array<f32>;
@group(0) @binding(1) var repeat_sampler: sampler;
@group(0) @binding(2) var<uniform> params: PhotonParams;

struct PhotonOut {
    @builtin(position) position: vec4<f32>,
    @location(0) surface_point: vec2<f32>,
    @location(1) landing_point: vec2<f32>,
}

@vertex
fn vertex(@builtin(vertex_index) vertex_index: u32) -> PhotonOut {
    let grid = u32(params.geometry.y);
    let row = grid + 1u;
    let cell = vec2<f32>(f32(vertex_index % row), f32(vertex_index / row));
    let margin = params.optics.z;
    let uv = cell / f32(grid) * (1.0 + 2.0 * margin) - margin;
    let derivatives = textureSampleLevel(
        ocean_derivatives,
        repeat_sampler,
        uv,
        i32(params.geometry.z),
        params.geometry.w,
    );
    let slope = vec2<f32>(
        derivatives.x / max(derivatives.z + 1.0, 0.3),
        derivatives.y / max(derivatives.w + 1.0, 0.3),
    );
    let normal = normalize(vec3<f32>(-slope.x, 1.0, -slope.y));
    let refracted = refract(-params.sun_direction.xyz, normal, 1.0 / 1.333);
    let flat = refract(-params.sun_direction.xyz, vec3<f32>(0.0, 1.0, 0.0), 1.0 / 1.333);
    let down = max(-refracted.y, 0.15);
    let offset = (refracted.xz / down - flat.xz / max(-flat.y, 0.15)) * params.optics.x;
    let surface = uv * params.geometry.x;
    let landing = surface + offset;
    let ndc = landing / params.geometry.x * 2.0 - 1.0;
    var output: PhotonOut;
    output.position = vec4<f32>(ndc.x, ndc.y, 0.0, 1.0);
    output.surface_point = surface;
    output.landing_point = landing;
    return output;
}

@fragment
fn fragment(input: PhotonOut) -> @location(0) vec4<f32> {
    let before = abs(
        dpdx(input.surface_point).x * dpdy(input.surface_point).y
        - dpdx(input.surface_point).y * dpdy(input.surface_point).x
    );
    let after = abs(
        dpdx(input.landing_point).x * dpdy(input.landing_point).y
        - dpdx(input.landing_point).y * dpdy(input.landing_point).x
    );
    let intensity = before / max(after + before * (1.0 / 8.0), 1e-9);
    let red = select(0.0, intensity, params.optics.y < 0.5);
    let green = select(0.0, intensity, params.optics.y >= 0.5);
    return vec4<f32>(red, green, 0.0, 1.0);
}
