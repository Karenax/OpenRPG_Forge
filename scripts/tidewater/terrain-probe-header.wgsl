// Source surface adapter only: default Bevy vertex and provisional PBR lighting.
#import bevy_pbr::{forward_io::VertexOutput, mesh_view_bindings::view, pbr_types,
    pbr_functions::{apply_pbr_lighting,main_pass_post_lighting_processing}}
struct TerrainSettings { wind:vec4f,sun:vec4f,domain:vec4f,flags:vec4f };
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var terrainNormalTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var smpLinearClamp:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var terrainSplatTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var terrainDetailTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var smpAniso4Repeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var smpLinearRepeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(6) var<uniform> settings:TerrainSettings;
struct SourceFrame { cameraPos:vec3f,time:f32,windDir:vec2f,windSpeed:f32,sunDir:vec3f };
var<private> frame:SourceFrame;
var<private> terrainParams:TerrainParams;
var<private> mat:SourceMaterialParams;
struct FragInput { P:vec3f,N:vec3f,V:vec3f,pixel:vec2f };
struct SourceSurface { albedo:vec3f,normal:vec3f,emissive:vec3f,
    roughness:f32,metalness:f32,specularIntensity:f32,ao:f32,envIntensity:f32 };
