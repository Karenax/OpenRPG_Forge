// SPDX-License-Identifier: MIT
// Adapted from Tidewater src/ocean/UnderwaterLighting.js at 1438b1ab,
// copyright 2026 DRG Software Solutions LLC. See third_party/tidewater/LICENSE.
// Lite mode for village and other solid props: attenuate direct and ambient
// light using the coarse mean water level, without sampling photon maps.
struct VillageUnderwaterParams {
    sun_and_strength: vec4f,
    tiles_m: vec4f,
    wave_tiles_m: vec4f,
    water_absorption: vec4f,
    water_scattering: vec4f,
};
@group(#{MATERIAL_BIND_GROUP}) @binding(47) var villageOceanRepeat: sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(48) var<uniform> villageUnderwaterParams: VillageUnderwaterParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(49) var villageOceanLevel: texture_2d_array<f32>;

fn villageMeanWaterLevel(xz: vec2f) -> f32 {
    return uwMapLookup(xz, false).mean;
}

fn villageUnderwaterDirect(P: vec3f) -> vec3f {
    if (villageUnderwaterParams.water_scattering.w < 0.5 ||
        P.y >= villageUnderwaterParams.tiles_m.z + villageUnderwaterParams.tiles_m.w) {
        return vec3f(1.0);
    }
    let depth = max(villageMeanWaterLevel(P.xz) - P.y, 0.0);
    if (depth <= 0.0) { return vec3f(1.0); }
    let under = smoothstep(0.0, 0.08, depth);
    let ray = refract(-villageUnderwaterParams.sun_and_strength.xyz,
        vec3f(0.0, 1.0, 0.0), 1.0 / 1.333);
    let mu = max(-ray.y, 0.15);
    let extinction = villageUnderwaterParams.water_absorption.xyz
        + villageUnderwaterParams.water_scattering.xyz;
    return mix(vec3f(1.0), exp(-extinction * depth / mu), under);
}

fn villageUnderwaterAmbient(P: vec3f) -> vec3f {
    if (villageUnderwaterParams.water_scattering.w < 0.5 ||
        P.y >= villageUnderwaterParams.tiles_m.z + villageUnderwaterParams.tiles_m.w) {
        return vec3f(1.0);
    }
    let depth = max(villageMeanWaterLevel(P.xz) - P.y, 0.0);
    let under = smoothstep(0.0, 0.1, depth);
    let extinction = villageUnderwaterParams.water_absorption.xyz
        + villageUnderwaterParams.water_scattering.xyz;
    let attenuation = exp(-extinction * depth * 1.2) * 0.85
        + vec3f(0.0, 0.02, 0.04) * exp(-0.1 * depth);
    return mix(vec3f(1.0), attenuation, under);
}
