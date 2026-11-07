// SPDX-License-Identifier: MIT
// Tidewater LocalLights.js, 1438b1ab; copyright 2026 DRG Software Solutions LLC.
struct VillageLocalLightParams {
    positions: array<vec4f, 8>, colors: array<vec4f, 8>, directions: array<vec4f, 8>,
    count: i32, flash_on: f32, flash_cone: vec2f,
    flash_position: vec3f, flash_direction: vec3f, flash_color: vec3f,
}
@group(#{MATERIAL_BIND_GROUP}) @binding(54) var<storage, read> villageLocal: VillageLocalLightParams;

fn villageLocalSpot(cd: f32, inner: f32, outer: f32) -> f32 {
    let m = smoothstep(outer, inner, cd);
    return max(m * m, smoothstep(outer - 0.55, outer, cd) * 0.05);
}

// Local emitters bypass sun/cloud/contact-shadow/caustic modulation, as upstream.
fn villageLocalLights(s: LightingSurface, P: vec3f, N: vec3f, V: vec3f) -> vec3f {
    let diffuse = s.albedo * (1.0 - s.metalness);
    let f0 = mix(vec3f(0.04) * s.specularIntensity, s.albedo, s.metalness);
    let f90 = mix(s.specularIntensity, 1.0, s.metalness);
    let rough = clamp(s.roughness, 0.03, 1.0);
    var result = vec3f(0.0);
    for (var i = 0; i < villageLocal.count; i++) {
        let p = villageLocal.positions[i];
        let d = p.xyz - P;
        let d2 = dot(d, d);
        if (d2 < p.w) {
            let c = villageLocal.colors[i];
            let axis = villageLocal.directions[i];
            let L = d * inverseSqrt(max(d2, 1e-6));
            let x = d2 / p.w;
            let win = clamp(1.0 - x * x, 0.0, 1.0);
            let spot = villageLocalSpot(dot(-L, axis.xyz), c.w, axis.w);
            let distance = d2 * inverseSqrt(max(d2, 1e-6));
            let under = clamp((villageUnderwaterParams.tiles_m.z - min(P.y, p.y))
                / max(abs(p.y - P.y), 1e-3), 0.0, 1.0);
            let transmission = exp(-(villageUnderwaterParams.water_absorption.xyz
                + villageUnderwaterParams.water_scattering.xyz) * (distance * under));
            let color = c.xyz * transmission * (win * win * spot / (d2 + 0.15));
            let irradiance = max(dot(N, L), 0.0) * color;
            result += irradiance * diffuse * INV_PI
                + irradiance * BRDF_GGX(L, V, N, f0, f90, rough);
        }
    }
    return result;
}
