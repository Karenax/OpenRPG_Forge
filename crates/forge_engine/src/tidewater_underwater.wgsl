// SPDX-License-Identifier: MIT
// Source-derived subset of Tidewater src/post/Underwater.js at 1438b1ab.

#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput

struct Settings {
    camera_world: mat4x4<f32>,
    state: vec4<f32>,
    projection: vec4<f32>,
    depth_projection: vec4<f32>,
    absorption: vec4<f32>,
    scattering: vec4<f32>,
    sun_direction: vec4<f32>,
    sun_color: vec4<f32>,
    sky_irradiance: vec4<f32>,
}

struct WaterQueryPacket {
    stamp: vec4<u32>,
    points: array<vec4<f32>, 64>,
    results: array<vec4<f32>, 64>,
}

@group(0) @binding(0) var scene_color: texture_2d<f32>;
@group(0) @binding(1) var scene_sampler: sampler;
@group(0) @binding(2) var scene_depth: texture_depth_2d;
@group(0) @binding(3) var<storage, read> water_queries: WaterQueryPacket;
@group(0) @binding(4) var<uniform> settings: Settings;
@group(0) @binding(5) var shaft_light: texture_2d<f32>;

const PI: f32 = 3.141592653589793;
const IOR: f32 = 1.333;
const MENISCUS_BAND_PIXELS: f32 = 9.0;
const MENISCUS_SEARCH_PIXELS: i32 = 16;

fn view_z(depth: f32) -> f32 {
    if depth <= 1e-7 {
        return -600.0;
    }
    return -settings.depth_projection.x / (depth + settings.depth_projection.y);
}

fn view_ray(uv: vec2<f32>) -> vec3<f32> {
    let ndc = vec2(uv.x * 2.0 - 1.0, (1.0 - uv.y) * 2.0 - 1.0);
    return vec3(ndc.x / settings.projection.x, ndc.y / settings.projection.y, -1.0);
}

// The water material writes Tidewater's visible-surface mask into the HDR
// scene alpha: 0.25 for its air side, 0.75 for its underwater side. Other
// opaque surfaces and the sky retain alpha 1.0. Read the exact raster pixel;
// bilinear sampling would invent a second, displaced waterline at its edge.
fn visible_water_medium(uv: vec2<f32>) -> vec2<f32> {
    let dimensions = vec2<i32>(textureDimensions(scene_color));
    let pixel = clamp(vec2<i32>(uv * vec2<f32>(dimensions)), vec2(0), dimensions - 1);
    let alpha = textureLoad(scene_color, pixel, 0).a;
    return vec2(select(0.0, 1.0, alpha > 0.5), select(0.0, 1.0, alpha > 0.125 && alpha < 0.875));
}

// Same-frame GPU query supplies the local wave normal for pixels without a
// visible surface (shore/objects or a ray into the sky). This is only a
// near-camera endpoint fallback; the visible water mask above carries the
// actual displaced and folded mesh wherever that mesh is on screen.
fn queried_height_at(xz: vec2<f32>) -> f32 {
    let query = water_queries.results[0];
    let normal_xz = query.yz;
    let normal_y = sqrt(max(1.0 - dot(normal_xz, normal_xz), 1e-4));
    let slope = -normal_xz / normal_y;
    return query.x + dot(slope, xz - water_queries.points[0].xy);
}

// A rasterized back face cannot put the near lens in water if the local
// same-frame surface remains below that lens pixel. This also rejects tiny
// self-intersections of the choppy mesh seen from a camera clearly in air.
fn near_lens_in_water(uv: vec2<f32>) -> bool {
    let query = water_queries.results[0];
    let normal_xz = query.yz;
    let normal_y = sqrt(max(1.0 - dot(normal_xz, normal_xz), 1e-4));
    let slope = -normal_xz / normal_y;
    let lens = (settings.camera_world * vec4(view_ray(uv) * settings.state.y, 1.0)).xyz;
    let height = query.x + dot(slope, lens.xz - water_queries.points[0].xy);
    return lens.y < height;
}

fn lens_medium(uv: vec2<f32>) -> f32 {
    if !near_lens_in_water(uv) {
        return 0.0;
    }
    let visible = visible_water_medium(uv);
    if visible.y > 0.5 {
        return visible.x;
    }
    let dimensions = vec2<i32>(textureDimensions(scene_depth));
    let pixel = clamp(vec2<i32>(uv * vec2<f32>(dimensions)), vec2(0), dimensions - 1);
    let depth = textureLoad(scene_depth, pixel, 0);
    let ray = view_ray(uv);
    let world_ray = (settings.camera_world * vec4(ray, 0.0)).xyz;
    if depth <= 1e-7 {
        return select(0.0, 1.0, world_ray.y < 0.0);
    }
    let endpoint = settings.camera_world[3].xyz + world_ray * -view_z(depth);
    if distance(endpoint.xz, water_queries.points[0].xy) > 10.0 {
        return select(0.0, 1.0, settings.state.x < water_queries.results[0].x);
    }
    return select(0.0, 1.0, endpoint.y < queried_height_at(endpoint.xz));
}

// The standalone query packet already contains the same-frame height. The
// combined optical shader replaces this hook with its live FFT/shore setup.
fn setup_lens_medium_frame() {}

// Export precisely the classification used by the underwater composite. Haze
// consumes this R16Float result; surface alpha alone cannot classify the lens.
@fragment
fn lens_medium_fragment(input: FullscreenVertexOutput) -> @location(0) vec4<f32> {
    setup_lens_medium_frame();
    return vec4(lens_medium(input.uv), 0.0, 0.0, 1.0);
}

fn lens_medium_offset(
    uv: vec2<f32>,
    dy: i32,
    dimensions: vec2<i32>
) -> f32 {
    let shifted = clamp(
        uv + vec2(0.0, f32(dy) / f32(dimensions.y)),
        vec2(0.0),
        vec2(0.9999)
    );
    return lens_medium(shifted);
}

fn integrated_light(mu: f32, extinction: vec3<f32>, camera_depth: f32, direction_y: f32, distance: f32) -> vec3<f32> {
    let a = extinction * camera_depth / mu;
    let k = extinction * (1.0 - direction_y / mu);
    let e0 = exp(-a);
    let e1 = exp(-max(a + k * distance, vec3(0.0)));
    return select((e0 - e1) / k, e0 * distance, abs(k) < vec3(1e-4));
}

// Source depth-aware four-tap upsample; alpha is the marched view distance.
fn upsample_shafts(uv: vec2f, distance: f32) -> vec3f {
    let size = vec2i(textureDimensions(shaft_light));
    let position = uv * vec2f(size) - 0.5;
    let corner = vec2i(floor(position));
    let fraction = fract(position);
    var accumulated = vec3f(0.0);
    var weight_sum = 1e-6;
    for (var i = 0; i < 4; i++) {
        let offset = vec2i(i & 1, i >> 1);
        let sample = textureLoad(shaft_light, clamp(corner + offset, vec2i(0), size - 1), 0);
        let bilinear = select(1.0 - fraction.x, fraction.x, offset.x == 1)
            * select(1.0 - fraction.y, fraction.y, offset.y == 1);
        let relative = abs(sample.a - distance) / max(distance, 0.5);
        let weight = bilinear / pow(relative * 10.0 + 1.0, 2.0) + 1e-5;
        accumulated += sample.rgb * weight;
        weight_sum += weight;
    }
    return accumulated / weight_sum;
}

@fragment
fn fragment(input: FullscreenVertexOutput) -> @location(0) vec4<f32> {
    let uv = input.uv;
    let unmodified = textureSampleLevel(scene_color, scene_sampler, uv, 0.0).rgb;
    let water_height = water_queries.results[0].x;
    let camera_height = settings.state.x - water_height;
    let camera_depth = max(water_height - settings.state.x, 0.0);
    if settings.projection.z < 0.5 || water_queries.stamp.x == 0u {
        return vec4(unmodified, 1.0);
    }

    let dimensions = vec2<i32>(textureDimensions(scene_depth));
    let surface_band = abs(camera_height) < settings.state.w;
    var under = camera_height < 0.0;
    var line_distance = 1e4;
    var line_direction = 0.0;

    if surface_band {
        let here = lens_medium(uv);
        under = here > 0.5;
        let flips = abs(lens_medium_offset(uv, -8, dimensions) - here)
            + abs(lens_medium_offset(uv, 8, dimensions) - here)
            + abs(lens_medium_offset(uv, -MENISCUS_SEARCH_PIXELS, dimensions) - here)
            + abs(lens_medium_offset(uv, MENISCUS_SEARCH_PIXELS, dimensions) - here);
        if flips > 0.5 {
            for (var i = 1; i < MENISCUS_SEARCH_PIXELS + 1; i = i + 1) {
                let up = abs(lens_medium_offset(uv, -i, dimensions) - here) > 0.5;
                let down = abs(lens_medium_offset(uv, i, dimensions) - here) > 0.5;
                if line_distance > 1e3 && (up || down) {
                    line_distance = f32(i) - 0.5;
                    line_direction = select(1.0, -1.0, up);
                }
            }
        }
    } else if !under {
        return vec4(unmodified, 1.0);
    }

    let band_position = clamp(line_distance / MENISCUS_BAND_PIXELS, 0.0, 1.0);
    let bend_pixels = band_position
        * sqrt(max(1.0 - band_position * band_position, 0.0))
        * MENISCUS_BAND_PIXELS
        * 0.8;
    let bent_uv = uv
        - vec2(
            0.0,
            line_direction * bend_pixels / f32(dimensions.y),
        );
    let base = textureSampleLevel(scene_color, scene_sampler, bent_uv, 0.0).rgb;
    var result = base;

    if under {
        let pixel = clamp(
            vec2<i32>(uv * vec2<f32>(dimensions)),
            vec2(0),
            dimensions - 1,
        );
        let depth = textureLoad(scene_depth, pixel, 0);
        let ray = view_ray(uv);
        let distance = max(
            min(length(ray * -view_z(depth)), 600.0)
                - settings.state.y * length(ray),
            0.0,
        );
        let direction = normalize((settings.camera_world * vec4(ray, 0.0)).xyz);

        let absorption = settings.absorption.xyz;
        let scattering = settings.scattering.xyz;
        let extinction = absorption + scattering;
        let light_direction = -refract(
            -normalize(settings.sun_direction.xyz),
            vec3(0.0, 1.0, 0.0),
            1.0 / IOR,
        );
        let mu = max(light_direction.y, 0.15);
        let g = 0.85;
        let cos_phase = dot(direction, light_direction);
        let phase = (1.0 - g * g) / (4.0 * PI)
            / pow(max(1.0 + g * g - cos_phase * 2.0 * g, 1e-4), 1.5) * 0.75
            + 0.25 / (4.0 * PI);

        let backscatter = scattering * 0.035;
        let multiple_albedo = backscatter * 1.3
            / max(absorption + backscatter, vec3(1e-6));
        let in_sun = settings.sun_color.xyz * 0.96
            * (scattering * phase + multiple_albedo * extinction * (1.0 / PI))
            * integrated_light(mu, extinction, camera_depth, direction.y, distance);
        let in_ambient = settings.sky_irradiance.xyz * PI * 0.9
            * (scattering * (1.0 / (4.0 * PI))
                + multiple_albedo * extinction * (1.0 / PI))
            * integrated_light(0.8, extinction, camera_depth, direction.y, distance);
        let transmittance = exp(-extinction * distance);
        var shafts = vec3f(0.0);
        if settings.projection.w > 0.0 {
            shafts = upsample_shafts(uv, distance);
        }
        result = max(base * transmittance + in_sun + in_ambient + shafts, vec3(0.0));
    }

    // Tidewater's rounded lens edge: a two-pixel dark contact line surrounded
    // by a sky/sun-lit rim inside the nine-pixel refractive band.
    let line = 1.0 - smoothstep(0.0, 2.0, line_distance);
    let rim = (1.0
        - smoothstep(
            MENISCUS_BAND_PIXELS * 0.4,
            MENISCUS_BAND_PIXELS,
            line_distance,
        ))
        * smoothstep(0.5, 2.5, line_distance);
    let with_line = result * (1.0 - line * 0.55)
        + rim
            * 0.15
            * (settings.sky_irradiance.xyz * 2.5 + settings.sun_color.xyz * 0.02);
    return vec4(with_line, 1.0);
}
