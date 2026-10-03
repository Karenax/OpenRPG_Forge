// SPDX-License-Identifier: MIT
// Tidewater UnderwaterLighting.js hookShadowPosition, pinned 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
// Full receivers follow local waves; lite receivers use their mean level.
fn underwaterShadowPosition(P: vec3f, pixel: vec2f) -> vec3f {
    if (UW_SHADOW_PARAMS.water_scattering.w < 0.5 ||
        UW_SHADOW_PARAMS.water_absorption.w < 0.5 ||
        P.y >= UW_SHADOW_PARAMS.tiles_m.z + UW_SHADOW_PARAMS.tiles_m.w) {
        return P;
    }
    let sample = uwMapLookup(P.xz, UW_SHADOW_WAVES);
    var height = sample.mean;
    var normal = vec3f(0.0, 1.0, 0.0);
    if (UW_SHADOW_WAVES) {
        height = sample.height;
        normal = normalize(vec3f(-sample.slope.x, 1.0, -sample.slope.y));
    }
    let depth = height - P.y;
    if (depth <= 0.0) { return P; }
    let up = -refract(-UW_SHADOW_PARAMS.sun_and_strength.xyz, normal, 1.0 / 1.333);
    let entry = P + up * (depth / max(up.y, 0.15));
    let px = pixel + f32(uwMapFrame.stamp.x % 64u) * 5.588238;
    let g = fract(52.9829189 * fract(dot(px, vec2f(0.06711056, 0.00583715))));
    let phi = g * 6.28318530718;
    let radius = sqrt(fract(g * 1.618034 + 0.31)) * min(depth * 0.05, 0.5);
    return entry + vec3f(cos(phi), 0.0, sin(phi)) * radius;
}
