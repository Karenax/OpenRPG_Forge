// SPDX-License-Identifier: MIT
// Source surface ABI and constants, DRG Software Solutions LLC, 1438b1ab.
const PI: f32 = 3.141592653589793;
const TWO_PI: f32 = 6.283185307179586;
const INV_PI: f32 = 0.3183098861837907;
const EPS: f32 = 1e-5;
fn sat(x:f32)->f32 { return clamp(x,0.0,1.0); }
struct SourceSurface {
    albedo:vec3f, normal:vec3f, roughness:f32, metalness:f32,
    specularIntensity:f32, translucency:vec3f, ao:f32,
};
struct ForgeSceneLighting { sunDirection:vec4f, sunColor:vec4f, environmentSH:array<vec4f,9> };
@group(#{MATERIAL_BIND_GROUP}) @binding(254) var<storage,read> sceneLighting:ForgeSceneLighting;
@group(#{MATERIAL_BIND_GROUP}) @binding(255) var<uniform> sourceDomain:vec4f;
struct SourceSceneFrame {
    camera:vec4f, wind:vec4f, gust:vec4f,
    previous_camera:vec4f, previous_wind:vec4f, previous_gust:vec4f,
    clouds:CloudsParams,
};
@group(#{MATERIAL_BIND_GROUP}) @binding(256) var<storage,read> sourceFrame:SourceSceneFrame;
