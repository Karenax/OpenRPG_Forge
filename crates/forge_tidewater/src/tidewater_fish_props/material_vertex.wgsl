#import bevy_pbr::{mesh_functions, mesh_view_bindings as fish_camera,
    view_transformations::position_world_to_clip}
const PI:f32=3.141592653589793;
const TWO_PI:f32=6.283185307179586;
const INV_PI:f32=0.3183098861837907;
const EPS:f32=1e-5;
fn sat(x:f32)->f32{return clamp(x,0.0,1.0);}
struct FishSettings {info:vec4f,flags:vec4f,lod:vec4f,frameIndex:u32};
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> settings:FishSettings;
struct FishSkin {rows:array<vec4f,168>};
@group(#{MATERIAL_BIND_GROUP}) @binding(19) var<storage,read> fishSkin:FishSkin;
// x: exact time bits, y: full uint32 frame index, z: caller-owned clock enabled.
@group(#{MATERIAL_BIND_GROUP}) @binding(56) var<storage,read> fishClock:vec4u;
struct FishFrame {view:mat4x4f,time:f32,frameIndex:u32};
var<private> frame:FishFrame;
struct Input {
 @builtin(instance_index) instance:u32,
 @location(0) position:vec3f,@location(1) normal:vec3f,
 @location(2) rest:vec3f,@location(3) data:vec4f,
};
struct Output {
 @builtin(position) position:vec4f,
 @location(0) P:vec3f,@location(1) normal:vec3f,
 @location(2) rest:vec3f,@location(3) data:vec4f,
 @location(4) length_m:f32,
};
@vertex fn vertex(input:Input)->Output {
 let world=mesh_functions::get_world_from_local(input.instance);
 var output:Output;
 output.P=(world*vec4f(input.position,1.0)).xyz;
 output.position=position_world_to_clip(output.P);
 output.normal=mesh_functions::mesh_normal_local_to_world(input.normal,input.instance);
 output.rest=input.rest;output.data=input.data;
 // Preserve source L at import; author resizing changes the actual world footprint.
 output.length_m=length(world[2].xyz);
 return output;
}
