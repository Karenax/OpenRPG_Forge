// SPDX-License-Identifier: MIT
// Copyright 2026 DRG Software Solutions LLC.
// Tidewater 1438b1abfcaee3267092b75573014f4d9b4a983c, src/post/PostFX.js.
// See third_party/tidewater/LICENSE.
struct Controls { grade: vec4f, effects: vec4f, timing: vec4f, frame: vec4u };
fn luminance(c: vec3f) -> f32 { return dot(c, vec3f(0.2126, 0.7152, 0.0722)); }
@group(0) @binding(0) var aeTex: texture_2d<f32>;
@group(0) @binding(1) var<uniform> ctrl: Controls;
@group(0) @binding(2) var<storage, read_write> aeExposure: array<f32>;
var<workgroup> sumL: array<f32, 256>;
var<workgroup> sumW: array<f32, 256>;
@compute @workgroup_size( 256, 1, 1 )
fn main( @builtin( local_invocation_id ) lid: vec3u ) {
	let t = lid.x;
	let size = textureDimensions( aeTex );
	let n = size.x * size.y;
	var accL = 0.0; var accW = 0.0;
	for ( var i = 0u; i < 160u; i++ ) {
		let idx = t + i * 256u;
		if ( idx < n ) {
			let x = idx % size.x; let y = idx / size.x;
			let c = textureLoad( aeTex, vec2i( i32( x ), i32( y ) ), 0 ).rgb;
			let l = log2( max( luminance( c ), 1e-4 ) );
			let uvc = vec2f( ( f32( x ) + 0.5 ) / f32( size.x ), ( f32( y ) + 0.5 ) / f32( size.y ) ) - 0.5;
			let w = max( 1.0 - length( uvc * vec2f( 1.0, 1.4 ) ) * 1.2, 0.15 );
			accL += l * w;
			accW += w;
		}
	}
	sumL[ t ] = accL;
	sumW[ t ] = accW;
	workgroupBarrier();

if (t < 128u) { sumL[t] += sumL[t+128u]; sumW[t] += sumW[t+128u]; }
workgroupBarrier();
if (t < 64u) { sumL[t] += sumL[t+64u]; sumW[t] += sumW[t+64u]; }
workgroupBarrier();
if (t < 32u) { sumL[t] += sumL[t+32u]; sumW[t] += sumW[t+32u]; }
workgroupBarrier();
if (t < 16u) { sumL[t] += sumL[t+16u]; sumW[t] += sumW[t+16u]; }
workgroupBarrier();
if (t < 8u) { sumL[t] += sumL[t+8u]; sumW[t] += sumW[t+8u]; }
workgroupBarrier();
if (t < 4u) { sumL[t] += sumL[t+4u]; sumW[t] += sumW[t+4u]; }
workgroupBarrier();
if (t < 2u) { sumL[t] += sumL[t+2u]; sumW[t] += sumW[t+2u]; }
workgroupBarrier();
if (t < 1u) { sumL[t] += sumL[t+1u]; sumW[t] += sumW[t+1u]; }
workgroupBarrier();
	if ( t == 0u ) {
		let avg = exp2( sumL[ 0 ] / max( sumW[ 0 ], 1e-4 ) );
		// the eye only partly compensates: dark scenes stay darker (dusk and night must not
		// look like day), and at night at most one extra stop
		let ratio = 0.25 / avg;
		let partial = select( ratio, pow( ratio, 0.8 ), ratio > 1.0 );
		let tgt = clamp( partial, 0.6, mix( 6.0, 2.0, ctrl.timing.y ) );
		let cur = aeExposure[ 0 ];
		let rate = select( 1.1, 1.6, tgt > cur );
		let k = 1.0 - exp( - ctrl.timing.x * rate );
		let next = exp2( mix( log2( max( cur, 1e-3 ) ), log2( tgt ), k ) );
		aeExposure[ 0 ] = select( 1.0, next, ctrl.effects.w > 0.5 );
	}
}
