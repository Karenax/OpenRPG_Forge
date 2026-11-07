// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 DRG Software Solutions LLC
// Source commit 1438b1abfcaee3267092b75573014f4d9b4a983c; see third_party/tidewater/LICENSE.
struct ShadowParams{origin:f32,size:f32,sunBake:vec3f};
@group(0) @binding(0) var terrainShadowHeights:texture_2d<f32>;
@group(0) @binding(1) var smpLinearClamp:sampler;
@group(0) @binding(2) var terrainSunShadowOut:texture_storage_2d<rgba16float,write>;
@group(0) @binding(3) var<uniform> terrainParams:ShadowParams;
@compute @workgroup_size( 8, 8, 1 )
fn main( @builtin( global_invocation_id ) gid: vec3u ) {
	if ( gid.x >= 512u || gid.y >= 512u ) { return; }
	let ij = vec2f( gid.xy );
	let xz = ( ij + 0.5 ) / 512.0 * terrainParams.size + terrainParams.origin;
	let L = terrainParams.sunBake;
	let lxz = max( length( L.xz ), 1e-4 );
	let dir = L.xz / lxz;
	let tanEl = L.y / lxz;
	var top = -1e4;
	var occ = 0.0;
	var t = f32( 24 );
	var dt = f32( 6 );
	for ( var i = 0; i < 24; i++ ) {
		let q = xz + dir * t;
		let uv = ( q - terrainParams.origin ) / terrainParams.size;
		let hq = textureSampleLevel( terrainShadowHeights, smpLinearClamp, uv, log2( max( dt * 0.25000000, 1.0 ) ) ).x;
		let v = hq - t * tanEl;
		if ( v > top ) {
			top = v;
			occ = t;
		}
		dt *= 1.22;
		t += dt;
	}
	textureStore( terrainSunShadowOut, vec2u( gid.xy ), vec4f( top, occ, 0.0, 1.0 ) );
}
