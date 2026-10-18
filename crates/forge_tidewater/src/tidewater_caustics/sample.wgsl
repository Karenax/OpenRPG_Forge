// SPDX-License-Identifier: MIT
// Adapted from Tidewater src/ocean/Caustics.js at 1438b1ab,
// copyright 2026 DRG Software Solutions LLC. See third_party/tidewater/LICENSE.
// Material-side sampler: the wave/foam/detail inputs come from the scene's
// underwater-light maps, not from this photon-map pass.
struct CausticSampleParams {
    sun_and_strength: vec4f,
    tiles_m: vec4f,
    wave_tiles_m: vec4f,
    water_absorption: vec4f,
    water_scattering: vec4f,
};
@group(#{MATERIAL_BIND_GROUP}) @binding(45) var causticsFineTex: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(46) var causticsBroadTex: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(47) var causticsRepeat: sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(48) var<uniform> causticsParams: CausticSampleParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(49) var causticsWaves: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(50) var causticsLevel: texture_2d_array<f32>;

fn causticsStretch(g: vec2f, min_len: f32) -> vec2f {
    return g * max(min_len / max(length(g), 1e-9), 1.0);
}

fn causticsFetchFine(uv: vec2f, level: f32, dx: vec2f, dy: vec2f) -> vec4f {
    let minimum = exp2(level) / 512.0;
    let tile = causticsParams.tiles_m.x;
    return textureSampleGrad(causticsFineTex, causticsRepeat, uv,
        causticsStretch(dx / tile, minimum), causticsStretch(dy / tile, minimum));
}

fn causticsFetchBroad(uv: vec2f, dx: vec2f, dy: vec2f) -> vec4f {
    let minimum = exp2(1.5) / 256.0;
    let tile = causticsParams.tiles_m.y;
    return textureSampleGrad(causticsBroadTex, causticsRepeat, uv,
        causticsStretch(dx / tile, minimum), causticsStretch(dy / tile, minimum));
}

// The direct-light multiplier for a submerged receiver. It is evaluated with
// explicit world-space gradients so branches in the material remain legal.
fn causticsSampleBaked(
    P: vec3f, depth: f32, slope: vec2f, foam: f32,
    dx: vec2f, dy: vec2f, detail_k: f32,
) -> vec3f {
    return causticsSample(P, depth, slope, foam, dx, dy, detail_k, false);
}

// RefractionPass uses the same photons without chromatic dispersion.
fn causticsSampleBakedMono(
    P: vec3f, depth: f32, slope: vec2f, foam: f32,
    dx: vec2f, dy: vec2f, detail_k: f32,
) -> vec3f {
    return causticsSample(P, depth, slope, foam, dx, dy, detail_k, true);
}

fn causticsSample(
    P: vec3f, depth: f32, slope: vec2f, foam: f32,
    dx: vec2f, dy: vec2f, detail_k: f32, mono: bool,
) -> vec3f {
    let n = normalize(vec3f(-slope.x, 1.0, -slope.y));
    let Ls = refract(-causticsParams.sun_and_strength.xyz, n, 1.0 / 1.333);
    let entry = P.xz - Ls.xz * (depth / max(-Ls.y, 0.15));
    let blur = clamp(depth * 0.4 - 0.2, 0.0, 3.0);
    let focal_mix = clamp((depth - 1.2) / 2.8, 0.0, 1.0);
    let fine_tile = causticsParams.tiles_m.x;
    let fine_uv = entry / fine_tile;
    let green_planes = causticsFetchFine(fine_uv, blur, dx, dy);
    let green = mix(green_planes.x, green_planes.y, focal_mix);

    var red = green;
    var blue = green;
    if (!mono) {
        let dispersion = normalize(Ls.xz + vec2f(1e-4, 0.0)) * (depth * 0.0035);
        let red_planes = causticsFetchFine(fine_uv + dispersion / fine_tile, blur, dx, dy);
        let blue_planes = causticsFetchFine(fine_uv - dispersion / fine_tile, blur, dx, dy);
        red = mix(red_planes.x, red_planes.y, focal_mix);
        blue = mix(blue_planes.x, blue_planes.y, focal_mix);
    }

    let broad_planes = causticsFetchBroad(entry / causticsParams.tiles_m.y, dx, dy);
    let broad = mix(broad_planes.x, broad_planes.y, clamp(depth / 9.0, 0.0, 1.0));
    let intensity = vec3f(red, green, blue) * mix(1.0, broad, 0.6);
    let strength = smoothstep(0.03, 0.5, depth) * exp(-0.06 * depth)
        * causticsParams.sun_and_strength.w * detail_k * (1.0 - clamp(foam, 0.0, 1.0));
    let lit = mix(vec3f(1.0), intensity, strength);
    return lit * (1.0 - clamp(foam, 0.0, 1.0) * 0.6);
}

fn terrainUnderwaterDirect(P: vec3f, N: vec3f) -> vec3f {
    let dx = dpdx(P.xz);
    let dy = dpdy(P.xz);
    if (causticsParams.water_scattering.w < 0.5 ||
        P.y >= causticsParams.tiles_m.z + causticsParams.tiles_m.w) {
        return vec3f(1.0);
    }
    let waves = uwMapLookup(P.xz, true);
    let depth = max(waves.height - P.y, 0.0);
    if (depth <= 0.0) { return vec3f(1.0); }
    let under = smoothstep(0.0, 0.08, depth);
    let ray = refract(-causticsParams.sun_and_strength.xyz, vec3f(0.0, 1.0, 0.0), 1.0 / 1.333);
    let mu = max(-ray.y, 0.15);
    let extinction = causticsParams.water_absorption.xyz + causticsParams.water_scattering.xyz;
    let attenuation = exp(-extinction * depth / mu);
    let caustics = causticsSampleBaked(P, depth, waves.slope, waves.foam, dx, dy, waves.detailK);
    return mix(vec3f(1.0), attenuation * caustics, under);
}

fn terrainUnderwaterAmbient(P: vec3f) -> vec3f {
    if (causticsParams.water_scattering.w < 0.5 ||
        P.y >= causticsParams.tiles_m.z + causticsParams.tiles_m.w) {
        return vec3f(1.0);
    }
    let depth = max(uwMapLookup(P.xz, false).mean - P.y, 0.0);
    let under = smoothstep(0.0, 0.1, depth);
    let extinction = causticsParams.water_absorption.xyz + causticsParams.water_scattering.xyz;
    let attenuation = exp(-extinction * depth * 1.2) * 0.85
        + vec3f(0.0, 0.02, 0.04) * exp(-0.1 * depth);
    return mix(vec3f(1.0), attenuation, under);
}
