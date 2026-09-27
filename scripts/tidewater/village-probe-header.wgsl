// Development adapter; source vertex/surface expressions are inserted unchanged.
#import bevy_pbr::{mesh_functions, mesh_view_bindings::view,
    view_transformations::position_world_to_clip, pbr_types,
    pbr_functions::{apply_pbr_lighting,main_pass_post_lighting_processing}}
struct Settings { wind:vec4f,color:vec4f,emissive:vec4f,surface:vec4f,flags:vec4f };
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> settings:Settings;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var smpAnisoRepeat:sampler;
struct SourceFrame { time:f32,windDir:vec2f,windSpeed:f32,night:f32 };
var<private> frame:SourceFrame;
var<private> mat:SourceMaterialParams;
struct Input {
    @builtin(instance_index) instance:u32,
    @location(0) position:vec3f, @location(1) normal:vec3f,
    @location(2) uv:vec2f, @location(3) tint:vec3f, @location(4) vdata:vec4f,
};
struct Output {
    @builtin(position) position:vec4f,
    @location(0) P:vec3f, @location(1) normal:vec3f, @location(2) uv:vec2f,
    @location(3) vTint:vec3f, @location(4) vData:vec4f,
};
struct SourceVertex { position:vec3f,normal:vec3f,uv:vec2f,tint:vec3f,vdata:vec4f };
struct FragInput { vs:Output,P:vec3f,N:vec3f,V:vec3f,uv:vec2f,pixel:vec2f,front:bool };
struct Surface { albedo:vec3f,alpha:f32,normal:vec3f,roughness:f32,metalness:f32,
    emissive:vec3f,ao:f32,envIntensity:f32 };
fn setupSource() {
    frame.time=settings.wind.w; frame.windDir=settings.wind.xy; frame.windSpeed=settings.wind.z;
    frame.night=settings.flags.y;
    mat.color=settings.color.xyz; mat.opacity=settings.color.w;
    mat.emissive=settings.emissive.xyz; mat.emissiveIntensity=settings.emissive.w;
    mat.roughness=settings.surface.x; mat.metalness=settings.surface.y;
    mat.alphaTest=settings.surface.z; mat.envMapIntensity=settings.surface.w;
}
