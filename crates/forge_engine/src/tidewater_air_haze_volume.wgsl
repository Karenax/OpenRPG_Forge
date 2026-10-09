// SPDX-License-Identifier: MIT
// Derived from Tidewater AirHaze.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
#define_import_path forge::tidewater_air_haze_volume

// The existing camera component's 160-byte ABI, unchanged. Projection stores
// both finite reverse-Z coefficients; no infinite-projection approximation.
struct HazeCamera {
    camera_world: mat4x4f,
    projection: vec4f,
    control: vec4f,
    atmosphere: array<vec4f, 4>,
}

// Render inputs only, not authoring data. All fields must come from this view's
// accepted frame and the resident scene resources before encoding any pass.
struct HazeVolume {
    // density, shafts, enabled, source haze frame (modulo 1024).
    control: vec4f,
    // Terrain origin X/Z, square world size, sunBaked.
    terrain_domain: vec4f,
    // Cloud shadow center X/Z, square world size, strength.
    cloud_rectangle: vec4f,
    // Sun UV, screen ray fade, radial pass index (0..2).
    screen: vec4f,
}

// Three lighting vec4s captured on the GPU from the prepared optical material's
// Frame.js fields. This 48-byte snapshot is NOT the 32-byte map stamp and NOT a
// CPU reconstruction of the 720-byte source uniform. Its producing dispatch
// must precede the march and composite, in the same scene generation.
struct HazeLighting {
    sun_sea: vec4f,
    sun_color: vec4f,
    sky_immersion: vec4f,
}

fn haze_key_sea(lighting: HazeLighting) -> vec4f {
    return lighting.sun_sea;
}

// Same camera projection and gates as AirHaze.update. Read the actual GPU
// lighting snapshot, avoiding a delayed CPU readback of the sun direction.
fn haze_screen(camera: HazeCamera, volume: HazeVolume, light: HazeLighting) -> vec3f {
    let sun = light.sun_sea.xyz;
    let vx = dot(camera.camera_world[0].xyz, sun);
    let vy = dot(camera.camera_world[1].xyz, sun);
    let vz = dot(camera.camera_world[2].xyz, sun);
    var result = vec3f(0.5, 0.5, 0.0);
    if (vz < -0.02) {
        result.x = 0.5 + 0.5 * vx / -vz * camera.projection.x;
        result.y = 0.5 - 0.5 * vy / -vz * camera.projection.y;
        result.z = (1.0 - smoothstep(0.6, 1.15, max(abs(result.x - 0.5), abs(result.y - 0.5))))
            * smoothstep(0.02, 0.25, -vz) * (1.0 - smoothstep(0.3, 0.75, sun.y));
    }
    if (light.sky_immersion.w > 0.5 || volume.control.z < 0.5 || volume.control.y <= 0.0) {
        result.z = 0.0;
    }
    return result;
}

struct HazeRay {
    distance: f32,
    direction: vec3f,
    sky: bool,
}

fn haze_view_ray(uv: vec2f, depth_map: texture_depth_2d, camera: HazeCamera) -> HazeRay {
    let pixel = vec2i(clamp(uv, vec2f(0.0), vec2f(0.9999)) * vec2f(textureDimensions(depth_map)));
    let depth = textureLoad(depth_map, pixel, 0);
    let ray = vec3f((uv.x * 2.0 - 1.0) / camera.projection.x,
                   (1.0 - uv.y * 2.0) / camera.projection.y, -1.0);
    var result: HazeRay;
    result.sky = depth < 1e-7;
    let distance = min(camera.projection.w / (max(depth, 1e-9) + camera.control.w) * length(ray), 60000.0);
    result.distance = select(distance, 60000.0, result.sky);
    result.direction = normalize((camera.camera_world * vec4f(ray, 0.0)).xyz);
    return result;
}
