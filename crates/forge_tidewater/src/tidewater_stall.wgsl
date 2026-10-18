// SPDX-License-Identifier: MIT
// Bevy adapter for Tidewater StallKit texture arrays.  This is intentionally a
// bounded array-material preview, not the upstream standard-material implementation.
#import bevy_pbr::{mesh_functions, mesh_view_bindings::view,
    view_transformations::position_world_to_clip}

@group(#{MATERIAL_BIND_GROUP}) @binding(0) var surfA: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var repeatSampler: sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var surfN: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var surfR: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var propA: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var propN: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(6) var propR: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(7) var signs: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(8) var clampSampler: sampler;
struct FishStandSceneLighting {
 sunDirection: vec4f,
 sunColor: vec4f,
 environmentSH: array<vec4f, 9>,
};
@group(#{MATERIAL_BIND_GROUP}) @binding(18) var<uniform> sceneLighting: FishStandSceneLighting;

// Tidewater StallKit's pinned slate/chalk noise, used by layer -3 only.
fn skHash(p: vec3f) -> f32 { return fract(sin(dot(p, vec3f(127.1, 311.7, 74.7))) * 43758.5453); }
fn skNoise(p: vec3f) -> f32 {
 let i = floor(p); let f = fract(p); let u = f * f * (3.0 - 2.0 * f);
 let a = mix(mix(skHash(i), skHash(i + vec3f(1, 0, 0)), u.x), mix(skHash(i + vec3f(0, 1, 0)), skHash(i + vec3f(1, 1, 0)), u.x), u.y);
 let b = mix(mix(skHash(i + vec3f(0, 0, 1)), skHash(i + vec3f(1, 0, 1)), u.x), mix(skHash(i + vec3f(0, 1, 1)), skHash(i + vec3f(1, 1, 1)), u.x), u.y);
 return mix(a, b, u.z);
}
fn skFbm(p: vec3f) -> f32 { return skNoise(p) * 0.55 + skNoise(p * 2.13 + 7.1) * 0.3 + skNoise(p * 4.7 + 3.3) * 0.15; }

struct Input {
 @builtin(instance_index) instance: u32, @location(0) position: vec3f, @location(1) normal: vec3f,
 @location(2) uv: vec2f, @location(3) color: vec4f, @location(4) layer: f32, @location(5) uv2: vec2f,
};
struct Output {
 @builtin(position) position: vec4f, @location(0) world: vec3f, @location(1) normal: vec3f,
 @location(2) uv: vec2f, @location(3) color: vec4f, @location(4) layer: f32, @location(5) uv2: vec2f,
};
@vertex fn vertex(input: Input) -> Output {
 var out: Output;
 out.world = (mesh_functions::get_world_from_local(input.instance) * vec4f(input.position, 1.0)).xyz;
 out.position = position_world_to_clip(out.world);
 out.normal = mesh_functions::mesh_normal_local_to_world(input.normal, input.instance);
 out.uv = input.uv; out.color = input.color; out.layer = input.layer; out.uv2 = input.uv2; return out;
}
@fragment fn fragment(input: Output) -> @location(0) vec4f {
 let layer = i32(round(input.layer)); var albedo = input.color.rgb;
 let geometry_normal = normalize(input.normal);
 var normal = geometry_normal;
 // StallKit's normal maps are tangent-space. Derive the same frame from world
 // position and UV outside the layer branches so derivatives stay uniform.
 let dp1 = dpdx(input.world); let dp2 = dpdy(input.world);
 let duv1 = dpdx(input.uv); let duv2 = dpdy(input.uv);
 let duv2x = dpdx(input.uv2); let duv2y = dpdy(input.uv2);
 let dp2perp = cross(dp2, geometry_normal);
 let dp1perp = cross(geometry_normal, dp1);
 let tangent = dp2perp * duv1.x + dp1perp * duv2.x;
 let bitangent = dp2perp * duv1.y + dp1perp * duv2.y;
 let t_inv = inverseSqrt(max(max(dot(tangent, tangent), dot(bitangent, bitangent)), 1e-24));
 let tbn = mat3x3f(tangent * t_inv, -bitangent * t_inv, geometry_normal);
 var ao = 1.0;
 if (layer >= 100) {
   let i = layer - 100; albedo *= textureSampleGrad(propA, repeatSampler, input.uv, i, duv1, duv2).rgb;
   let tn = textureSampleGrad(propN, repeatSampler, input.uv, i, duv1, duv2).xyz * 2.0 - 1.0;
   normal = normalize(tbn * vec3f(tn.xy, max(tn.z, 0.05)));
   ao = textureSampleGrad(propR, repeatSampler, input.uv, i, duv1, duv2).r;
 } else if (layer >= 0) {
   albedo *= textureSampleGrad(surfA, repeatSampler, input.uv, layer, duv1, duv2).rgb;
   let tn = textureSampleGrad(surfN, repeatSampler, input.uv, layer, duv1, duv2).xyz * 2.0 - 1.0;
   let strength = select(1.0, 0.35, layer == 2);
   normal = normalize(tbn * vec3f(tn.xy * strength, max(tn.z, 0.05)));
   ao = textureSampleGrad(surfR, repeatSampler, input.uv, layer, duv1, duv2).r;
 } else if (layer == -3) {
   let paint = textureSampleGrad(signs, clampSampler, input.uv2, 0, duv2x, duv2y);
   let dust = skFbm(vec3f(input.uv2 * vec2f(18.0, 9.0), 0.0));
   let chalk = paint.a * mix(0.55, 1.0, skNoise(vec3f(input.uv2 * 900.0, 1.0)));
   albedo = mix(vec3f(0.028, 0.034, 0.031) + dust * 0.035, vec3f(0.72, 0.72, 0.68), chalk);
 } else if (layer == -2 || layer == -7) {
   let sign = textureSampleGrad(signs, clampSampler, input.uv2, 0, duv2x, duv2y); albedo = mix(albedo, sign.rgb, sign.a);
 }
 // The pinned village probe supplies the source sun and SH coefficients. Use
 // its diffuse terms without inventing a second light direction for the stall.
 let d = normal;
 let sh = sceneLighting.environmentSH[0].rgb * 0.282095
    + sceneLighting.environmentSH[1].rgb * 0.488603 * d.y
    + sceneLighting.environmentSH[2].rgb * 0.488603 * d.z
    + sceneLighting.environmentSH[3].rgb * 0.488603 * d.x
    + sceneLighting.environmentSH[4].rgb * 1.092548 * d.x * d.y
    + sceneLighting.environmentSH[5].rgb * 1.092548 * d.y * d.z
    + sceneLighting.environmentSH[6].rgb * 0.315392 * (3.0 * d.z * d.z - 1.0)
    + sceneLighting.environmentSH[7].rgb * 1.092548 * d.x * d.z
    + sceneLighting.environmentSH[8].rgb * 0.546274 * (d.x * d.x - d.y * d.y);
 let indirect = max(sh, vec3f(0.0)) * ao;
 let direct = sceneLighting.sunColor.rgb
    * max(dot(normal, sceneLighting.sunDirection.xyz), 0.0) * 0.318309886;
 return vec4f(albedo * (direct + indirect), input.color.a);
}
