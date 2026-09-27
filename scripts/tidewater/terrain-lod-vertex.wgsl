// Forge vertex-stage adapter; source functions are inserted by preparation.
#import bevy_pbr::{forward_io::VertexOutput, mesh_view_bindings::view,
    view_transformations::position_world_to_clip}
struct TerrainLodParams { morph:array<vec4f,9> };
@group(#{MATERIAL_BIND_GROUP}) @binding(100) var terrainHeightTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(101) var<uniform> terrainLod:TerrainLodParams;
struct HeightDomain { origin:f32,size:f32,res:f32 };
var<private> terrainParams:HeightDomain;
struct LodInput {
    @builtin(instance_index) instance:u32,
    @location(0) position:vec3f,
    @location(1) normal:vec3f,
    @location(2) node:vec4f,
};
@vertex
fn vertex(v:LodInput)->VertexOutput {
    terrainParams.origin=-1024.0; terrainParams.size=2048.0; terrainParams.res=2048.0;
    let snapped=terrainLodSnapped(v.node,v.position.xz);
    let cv=terrainLodMorph(v.node,v.position.xz,view.world_position,terrainHeightAt(snapped));
    let P=vec3f(cv.worldXZ.x,terrainHeightAt(cv.worldXZ),cv.worldXZ.y);
    var o:VertexOutput;
    o.position=position_world_to_clip(P);
    o.world_position=vec4f(P,1.0); o.world_normal=vec3f(0.0,1.0,0.0);
#ifdef VERTEX_OUTPUT_INSTANCE_INDEX
    o.instance_index=v.instance;
#endif
    return o;
}
