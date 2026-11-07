// SPDX-License-Identifier: MIT
// Tidewater UnderwaterLighting.js / Caustics.js, pinned 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
// Two array layers preserve the source's four independent 512² RGBA16F maps.
struct UnderwaterMapFrame { origins: vec4f, stamp: vec4u };
@group(2) @binding(0) var<storage, read_write> uwBakeFrame: UnderwaterMapFrame;
@group(2) @binding(1) var uwOutWaves: texture_storage_2d_array<rgba16float, write>;
@group(2) @binding(2) var uwOutLevel: texture_storage_2d_array<rgba16float, write>;
struct ForgeHazeLighting {
    sun_sea: vec4f,
    sun_color: vec4f,
    sky_immersion: vec4f,
}
@group(2) @binding(3) var<storage, read_write> forgeHazeLighting: ForgeHazeLighting;

struct UnderwaterLongWaves { height: f32, slope: vec2f, foam: f32 };
fn underwaterLongWaves(xz: vec2f, texel: f32) -> UnderwaterLongWaves {
    let ground = terrainHeightAt(xz);
    let seaDepth = frame.seaLevel - ground;
    var h = 0.0;
    var slope = vec2f(0.0);
    for (var c = 0; c < 3; c++) {
        let uv = xz / ocean.sizes[c].x;
        let att = waterSurfaceCascadeAttenuation(c, seaDepth);
        let lvl = max(2.0, log2(texel * 256.0 / ocean.sizes[c].x));
        h += textureSampleLevel(oceanDisplacement, smpLinearRepeat, uv, c, lvl).y * att;
        if (c < 2) {
            slope += textureSampleLevel(oceanDerivatives, smpLinearRepeat, uv, c, lvl).xy * att;
        }
    }
    let sw = shoreEvaluate(xz, seaDepth, ground);
    h += sw.disp.y;
    slope += -sw.nShore.xz / max(sw.nShore.y, 0.25);
    let foam = sw.foam + shoreSimSample(xz).x * 0.8;
    h = h * smoothstep(0.0, 1.0, seaDepth) - smoothstep(0.0, -0.4, seaDepth) * 10.0;
    return UnderwaterLongWaves(frame.seaLevel + h, slope, foam);
}
fn underwaterMeanLevel(xz: vec2f) -> f32 {
    var h = 0.0;
    for (var c = 0; c < 2; c++) {
        h += textureSampleLevel(oceanDisplacement, smpLinearRepeat,
            xz / ocean.sizes[c].x, c, 3.0).y;
    }
    h *= smoothstep(0.0, 3.0, frame.seaLevel - terrainHeightAt(xz));
    return frame.seaLevel + h;
}
@compute @workgroup_size(8, 8, 1)
fn forge_underwater_light_maps(@builtin(global_invocation_id) gid: vec3u) {
    setupUnderwaterHeightFrame();
    let origin0 = floor(frame.cameraPos.xz / 0.25) * 0.25 - 64.0;
    let origin1 = floor(frame.cameraPos.xz / 2.0) * 2.0 - 512.0;
    // Publish origins and the actual optical GPU timestamp in the same dispatch.
    // Every workgroup derives its coordinates independently; no intergroup read.
    if (all(gid == vec3u(0))) {
        uwBakeFrame.origins = vec4f(origin0, origin1);
        uwBakeFrame.stamp = vec4u(frame.frameIndex, bitcast<u32>(frame.time), 512u, 2u);
        // Capture fields unpacked from the actual prepared optical GPU uniform.
        // Only this invocation writes them; the following render pass reads the
        // snapshot after this entire compute dispatch, on the same device.
        forgeHazeLighting.sun_sea = vec4f(frame.sunDir, frame.seaLevel);
        forgeHazeLighting.sun_color = vec4f(frame.sunColor, 0.0);
        forgeHazeLighting.sky_immersion = vec4f(frame.skyIrradiance, frame.cameraUnderwater);
    }
    let layer = i32(gid.z);
    let texel = select(0.25, 2.0, layer == 1);
    let origin = select(origin0, origin1, layer == 1);
    let xz = (vec2f(gid.xy) + 0.5) * texel + origin;
    let lw = underwaterLongWaves(xz, texel);
    textureStore(uwOutWaves, vec2i(gid.xy), layer,
        vec4f(lw.height - frame.seaLevel, lw.slope, clamp(lw.foam, 0.0, 1.0)));
    let detail = seaDetailSample(xz);
    let dk = mix(0.55, 1.25, detail.gust) * (1.0 - detail.slick * 0.6);
    textureStore(uwOutLevel, vec2i(gid.xy), layer,
        vec4f(underwaterMeanLevel(xz) - frame.seaLevel, dk, 0.0, 1.0));
}
