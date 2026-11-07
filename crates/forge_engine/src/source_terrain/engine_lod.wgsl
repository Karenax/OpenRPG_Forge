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

fn terrainHeightAt( xz: vec2f ) -> f32 {
	let res = terrainParams.res;
	let f = ( xz - terrainParams.origin ) / terrainParams.size * res - 0.5;
	let fc = clamp( f, vec2f( 0.0 ), vec2f( res - 1.001 ) );
	let i = floor( fc );
	let t = fract( fc );
	let ii = vec2i( i );
	let a = textureLoad( terrainHeightTex, ii, 0 ).x;
	let b = textureLoad( terrainHeightTex, ii + vec2i( 1, 0 ), 0 ).x;
	let c = textureLoad( terrainHeightTex, ii + vec2i( 0, 1 ), 0 ).x;
	let d = textureLoad( terrainHeightTex, ii + vec2i( 1, 1 ), 0 ).x;
	let h = mix( mix( a, b, t.x ), mix( c, d, t.x ), t.y );
	// outside the domain: deep ocean floor
	let outside = f.x < 0.0 || f.y < 0.0 || f.x > res - 1.0 || f.y > res - 1.0;
	return select( h, -90.0, outside );
}
struct TerrainLodVertex { worldXZ: vec2f, spacing: f32, morphK: f32, lod: f32, size: f32 };

// Morph in world space on the LOD's own vertex lattice (spacing h). Quarter nodes of a
// partially subdivided parent carry the parent's LOD, so their extra vertices first snap
// onto that lattice; every node covering a point then computes the same position.
fn terrainLodSnapped( node: vec4f, grid: vec2f ) -> vec2f {
	let h = terrainLod.morph[ i32( node.w ) ].z;
	let p = node.xy + grid * node.z;
	return floor( p / h + 1e-3 ) * h;
}

fn terrainLodMorph( node: vec4f, grid: vec2f, viewPos: vec3f, y0: f32 ) -> TerrainLodVertex {
	let lod = i32( node.w );
	let m = terrainLod.morph[ lod ];
	let h = m.z;
	let p = node.xy + grid * node.z;
	let idx = floor( p / h + 1e-3 );
	let snapped = idx * h;
	let dist = length( viewPos - vec3f( snapped.x, y0, snapped.y ) );
	let morphK = clamp( ( dist - m.x ) * m.y, 0.0, 1.0 );
	let odd = fract( idx * 0.5 ) * 2.0;
	var o: TerrainLodVertex;
	o.worldXZ = snapped - odd * h * morphK;
	o.spacing = h * ( morphK + 1.0 );
	o.morphK = morphK;
	o.lod = node.w;
	o.size = node.z;
	return o;
}
