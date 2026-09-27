// Forge adapter: developmental plant proof, not lighting/shadow/temporal parity.
#import bevy_pbr::{mesh_functions, view_transformations::position_world_to_clip,
    mesh_view_bindings::view, pbr_types, pbr_functions::{apply_pbr_lighting,main_pass_post_lighting_processing}}

struct ProbeParams {
    iPos: vec4f, iDat: vec4f, wind: vec4f, gustLod: vec4f,
    sunNight: vec4f, lodEndFrame: vec4f,
};
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> probe: ProbeParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var vegDetail: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var smpLinearRepeat: sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var vegLeafTex: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var smpAnisoRepeat: sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var vegImpA: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(6) var smpLinearClamp: sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(7) var vegImpB: texture_2d<f32>;
struct SourceFrame {
    cameraPos:vec3f, time:f32, windDir:vec2f, windSpeed:f32,
    sunDir:vec3f, night:f32, frameIndex:u32,
};
var<private> frame:SourceFrame;
struct SourceDraw { params:vec4f };
var<private> draw:SourceDraw;
// Exact source VegParams struct is inserted by the preparation script.
var<private> vegParams:VegParams;
fn setupSource() {
    frame.cameraPos=view.world_position;
    frame.time=probe.wind.w;
    frame.windDir=probe.wind.xy;
    frame.windSpeed=probe.wind.z;
    frame.sunDir=probe.sunNight.xyz;
    frame.night=probe.sunNight.w;
    frame.frameIndex=u32(probe.lodEndFrame.y);
    vegParams.camPos=view.world_position;
    vegParams.gustOffset=probe.gustLod.xy;
    setupSourceLobes();
    draw.params=vec4f(0.0,probe.gustLod.zw,probe.lodEndFrame.x);
}
struct ProbeVertex {
    @builtin(instance_index) instance:u32,
    @location(0) position:vec3f,
    @location(1) normal:vec3f,
    @location(2) uv:vec2f,
    @location(3) aVeg:vec4f,
    @location(4) aMat:vec4f,
    @location(5) aLobe:vec4f,
};
struct ProbeOut {
    @builtin(position) clip:vec4f,
    @location(0) P:vec3f,
    @location(1) normal:vec3f,
    @location(2) uv:vec2f,
    @location(3) vTrunkY:f32,
    @location(4) vTrunkT:vec3f,
    @location(5) vMat:vec4f,
    @location(6) vIDat:vec4f,
    @location(7) vIPos:vec3f,
    @location(8) vHf:f32,
    @location(9) vImp:vec4f,
    @location(10) vIPos4:vec4f,
};
struct SourceVertex {
    position:vec3f, normal:vec3f, uv:vec2f, model:mat4x4f,
    iPos:vec4f, iDat:vec4f, aVeg:vec4f, aMat:vec4f, aLobe:vec4f,
    useWorld:bool, worldPos:vec3f, worldNormal:vec3f,
};
struct FragInput {
    vs:ProbeOut, P:vec3f, N:vec3f, V:vec3f, uv:vec2f,
    front:bool, pixel:vec2f,
};
struct SourceSurface {
    albedo:vec3f, normal:vec3f, roughness:f32, metalness:f32,
    specularIntensity:f32, translucency:vec3f, ao:f32,
};
