// SPDX-License-Identifier: MIT
// Source-derived Tidewater 1438b1abfcaee3267092b75573014f4d9b4a983c, ContactShadows.js.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
// Read the existing SceneDepth texture BEFORE this view's opaque depth copy.
// This is previous OPAQUE depth, never the TAAU final depth including water.
struct ContactShadowFrame {
 view:mat4x4f, viewProj:mat4x4f, prevViewProjNoJitter:mat4x4f,
 cameraPos:vec3f, near:f32,
 sunDir:vec3f, seaLevel:f32,
 sunColor:vec3f, strength:f32,
 resolution:vec2f, frameIndex:u32, padding:u32,
};
@group(#{MATERIAL_BIND_GROUP}) @binding(56) var contactDepth:texture_depth_2d;
@group(#{MATERIAL_BIND_GROUP}) @binding(57) var<uniform> contactFrame:ContactShadowFrame;

// 0 (occluded) .. 1 visibility of the key light
fn hookContactShadow( P: vec3f, N: vec3f ) -> f32 {
	let L = contactFrame.sunDir;
	let fwd = -vec3f( contactFrame.view[ 0 ][ 2 ], contactFrame.view[ 1 ][ 2 ], contactFrame.view[ 2 ][ 2 ] );
	let w0 = dot( P - contactFrame.cameraPos, fwd );
	// depth change per pixel on this surface (before any branch)
	let slope = max( abs( dpdx( w0 ) ), abs( dpdy( w0 ) ) );
	var vis = 1.0;
	let lit = dot( contactFrame.sunColor, contactFrame.sunColor ) > 1e-8 && dot( N, L ) > 0.02;
	if ( contactFrame.strength > 0.0 && w0 < 32.0 && lit && P.y > contactFrame.seaLevel - 0.3 ) {

		let texSize = vec2f( textureDimensions( contactDepth ) );
		let len = smoothstep( 2.0, 30.0, w0 ) * 0.7 + 0.3;
		let q0 = contactFrame.prevViewProjNoJitter * vec4f( P, 1.0 );
		let qd = contactFrame.prevViewProjNoJitter * vec4f( L * len, 0.0 );
		let bias = slope * 2.0 + w0 * 0.002 + 0.01;
		// screen coordinate of P (the hook has no fragment coordinate)
		let cc = contactFrame.viewProj * vec4f( P, 1.0 );
		let pix = floor( ( cc.xy / cc.w * vec2f( 0.5, -0.5 ) + 0.5 ) * contactFrame.resolution );
		let jit = fract( interleavedGradientNoise( pix ) + f32( contactFrame.frameIndex ) * 0.618034 );
		var hit = 2.0; // ray parameter of the first occluded sample (> 1: none)
		// groups of independent loads (latency hiding), early exit between groups; far away the ray
		// covers few pixels and the first group is enough. Depths compare in reversed-Z device depth
		// (d ~ near / w): the bias and thickness in metres scale by near / w^2.
		if ( hit > 1.0 ) {
			{
				let s = ( jit + 0.0 ) / 8.0;
				let u = s * s * 0.85 + s * 0.15; // denser near the contact
				let q = q0 + qd * u;
				let iw = 1.0 / q.w;
				let uv = q.xy * iw * vec2f( 0.5, -0.5 ) + 0.5;
				let d = textureLoad( contactDepth, vec2i( min( clamp( uv, vec2f( 0.0 ), vec2f( 1.0 ) ) * texSize, texSize - 1.0 ) ), 0 );
				let k2 = contactFrame.near * iw * iw;
				let diff = d - q.z * iw; // > 0: the depth buffer is in front of the ray
				let thick = bias + 0.06 + u * len * 0.3;
				let occ = diff > bias * k2 && diff < thick * k2 && uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0;
				hit = min( hit, select( 2.0, u, occ ) );
			}
			{
				let s = ( jit + 1.0 ) / 8.0;
				let u = s * s * 0.85 + s * 0.15; // denser near the contact
				let q = q0 + qd * u;
				let iw = 1.0 / q.w;
				let uv = q.xy * iw * vec2f( 0.5, -0.5 ) + 0.5;
				let d = textureLoad( contactDepth, vec2i( min( clamp( uv, vec2f( 0.0 ), vec2f( 1.0 ) ) * texSize, texSize - 1.0 ) ), 0 );
				let k2 = contactFrame.near * iw * iw;
				let diff = d - q.z * iw; // > 0: the depth buffer is in front of the ray
				let thick = bias + 0.06 + u * len * 0.3;
				let occ = diff > bias * k2 && diff < thick * k2 && uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0;
				hit = min( hit, select( 2.0, u, occ ) );
			}
			{
				let s = ( jit + 2.0 ) / 8.0;
				let u = s * s * 0.85 + s * 0.15; // denser near the contact
				let q = q0 + qd * u;
				let iw = 1.0 / q.w;
				let uv = q.xy * iw * vec2f( 0.5, -0.5 ) + 0.5;
				let d = textureLoad( contactDepth, vec2i( min( clamp( uv, vec2f( 0.0 ), vec2f( 1.0 ) ) * texSize, texSize - 1.0 ) ), 0 );
				let k2 = contactFrame.near * iw * iw;
				let diff = d - q.z * iw; // > 0: the depth buffer is in front of the ray
				let thick = bias + 0.06 + u * len * 0.3;
				let occ = diff > bias * k2 && diff < thick * k2 && uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0;
				hit = min( hit, select( 2.0, u, occ ) );
			}
			{
				let s = ( jit + 3.0 ) / 8.0;
				let u = s * s * 0.85 + s * 0.15; // denser near the contact
				let q = q0 + qd * u;
				let iw = 1.0 / q.w;
				let uv = q.xy * iw * vec2f( 0.5, -0.5 ) + 0.5;
				let d = textureLoad( contactDepth, vec2i( min( clamp( uv, vec2f( 0.0 ), vec2f( 1.0 ) ) * texSize, texSize - 1.0 ) ), 0 );
				let k2 = contactFrame.near * iw * iw;
				let diff = d - q.z * iw; // > 0: the depth buffer is in front of the ray
				let thick = bias + 0.06 + u * len * 0.3;
				let occ = diff > bias * k2 && diff < thick * k2 && uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0;
				hit = min( hit, select( 2.0, u, occ ) );
			}
		}
		if ( hit > 1.0 && w0 < 12.0 ) {
			{
				let s = ( jit + 4.0 ) / 8.0;
				let u = s * s * 0.85 + s * 0.15; // denser near the contact
				let q = q0 + qd * u;
				let iw = 1.0 / q.w;
				let uv = q.xy * iw * vec2f( 0.5, -0.5 ) + 0.5;
				let d = textureLoad( contactDepth, vec2i( min( clamp( uv, vec2f( 0.0 ), vec2f( 1.0 ) ) * texSize, texSize - 1.0 ) ), 0 );
				let k2 = contactFrame.near * iw * iw;
				let diff = d - q.z * iw; // > 0: the depth buffer is in front of the ray
				let thick = bias + 0.06 + u * len * 0.3;
				let occ = diff > bias * k2 && diff < thick * k2 && uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0;
				hit = min( hit, select( 2.0, u, occ ) );
			}
			{
				let s = ( jit + 5.0 ) / 8.0;
				let u = s * s * 0.85 + s * 0.15; // denser near the contact
				let q = q0 + qd * u;
				let iw = 1.0 / q.w;
				let uv = q.xy * iw * vec2f( 0.5, -0.5 ) + 0.5;
				let d = textureLoad( contactDepth, vec2i( min( clamp( uv, vec2f( 0.0 ), vec2f( 1.0 ) ) * texSize, texSize - 1.0 ) ), 0 );
				let k2 = contactFrame.near * iw * iw;
				let diff = d - q.z * iw; // > 0: the depth buffer is in front of the ray
				let thick = bias + 0.06 + u * len * 0.3;
				let occ = diff > bias * k2 && diff < thick * k2 && uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0;
				hit = min( hit, select( 2.0, u, occ ) );
			}
			{
				let s = ( jit + 6.0 ) / 8.0;
				let u = s * s * 0.85 + s * 0.15; // denser near the contact
				let q = q0 + qd * u;
				let iw = 1.0 / q.w;
				let uv = q.xy * iw * vec2f( 0.5, -0.5 ) + 0.5;
				let d = textureLoad( contactDepth, vec2i( min( clamp( uv, vec2f( 0.0 ), vec2f( 1.0 ) ) * texSize, texSize - 1.0 ) ), 0 );
				let k2 = contactFrame.near * iw * iw;
				let diff = d - q.z * iw; // > 0: the depth buffer is in front of the ray
				let thick = bias + 0.06 + u * len * 0.3;
				let occ = diff > bias * k2 && diff < thick * k2 && uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0;
				hit = min( hit, select( 2.0, u, occ ) );
			}
			{
				let s = ( jit + 7.0 ) / 8.0;
				let u = s * s * 0.85 + s * 0.15; // denser near the contact
				let q = q0 + qd * u;
				let iw = 1.0 / q.w;
				let uv = q.xy * iw * vec2f( 0.5, -0.5 ) + 0.5;
				let d = textureLoad( contactDepth, vec2i( min( clamp( uv, vec2f( 0.0 ), vec2f( 1.0 ) ) * texSize, texSize - 1.0 ) ), 0 );
				let k2 = contactFrame.near * iw * iw;
				let diff = d - q.z * iw; // > 0: the depth buffer is in front of the ray
				let thick = bias + 0.06 + u * len * 0.3;
				let occ = diff > bias * k2 && diff < thick * k2 && uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0;
				hit = min( hit, select( 2.0, u, occ ) );
			}
		}

		// the sun's penumbra is millimetres at this range: a hard shadow, faded out toward the end
		// of the ray (no cut-off line at its length)
		vis = select( 1.0, smoothstep( 0.55, 1.0, hit ), hit <= 1.0 );
		vis = 1.0 - ( 1.0 - vis ) * contactFrame.strength * smoothstep( 32.0, 24.0, w0 );

	}
	return vis;
}
