// SPDX-License-Identifier: MIT
// Bevy mesh/camera adapter for the complete source StallKit surface and lighting.
#import bevy_pbr::{mesh_functions, mesh_view_bindings::view,
    view_transformations::position_world_to_clip}
// Shared source shadow helpers use the same constant as the fish adapter.
const TWO_PI:f32=6.283185307179586;
@group(#{MATERIAL_BIND_GROUP}) @binding(9) var<uniform> stallParameters:vec4f;

struct StallVertexInput {
 @builtin(instance_index) instance:u32,
 @location(0) position:vec3f, @location(1) normal:vec3f,
 @location(2) uv:vec2f, @location(3) color:vec4f,
 @location(4) layer:f32, @location(5) uv2:vec2f,
};
struct StallVertexOutput {
 @builtin(position) position:vec4f,
 @location(0) world:vec3f, @location(1) normal:vec3f,
 @location(2) uv:vec2f, @location(3) color:vec4f,
 @location(4) layer:f32, @location(5) uv2:vec2f,
};
@vertex fn vertex(input:StallVertexInput)->StallVertexOutput {
 var out:StallVertexOutput;
 out.world=(mesh_functions::get_world_from_local(input.instance)*vec4f(input.position,1)).xyz;
 out.position=position_world_to_clip(out.world);
 out.normal=mesh_functions::mesh_normal_local_to_world(input.normal,input.instance);
 out.uv=input.uv;out.color=input.color;out.layer=input.layer;out.uv2=input.uv2;
 return out;
}
@fragment fn fragment(input:StallVertexOutput)->@location(0) vec4f {
 let surface=stallSurface(
  StallInput(StallVaryings(input.layer,input.uv2),input.uv,input.color,input.world,normalize(input.normal)),
  StallFrame(stallParameters.x,stallParameters.y),
  StallMaterialParameters(stallParameters.z,stallParameters.w));
 let V=normalize(view.world_position-input.world);
 let light=StallLightingFrame(sceneLighting.sunDirection.xyz,sceneLighting.sunColor.xyz);
 return vec4f(shadeSurface(surface,input.world,V,input.position.xy,light),surface.alpha);
}
