// SPDX-License-Identifier: MIT
// Copyright 2026 DRG Software Solutions LLC.
// Tidewater 1438b1abfcaee3267092b75573014f4d9b4a983c, src/post/PostFX.js.
// See third_party/tidewater/LICENSE.
#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput
@group(0) @binding(0) var bSrc: texture_2d<f32>;
@group(0) @binding(1) var smpLinearClamp: sampler;
fn luminance(c: vec3f) -> f32 { return dot(c, vec3f(0.2126, 0.7152, 0.0722)); }

fn bTap( uv: vec2f, texel: vec2f, x: f32, y: f32 ) -> vec3f { return textureSampleLevel( bSrc, smpLinearClamp, uv + texel * vec2f( x, y ), 0.0 ).rgb; }
fn karis( c: vec3f ) -> vec3f { return c / ( luminance( c ) + 1.0 ); }

@fragment fn fragment( in: FullscreenVertexOutput ) -> @location(0) vec4f {
	let uv = in.uv;
	let texel = 1.0 / vec2f( textureDimensions( bSrc ) );
	let a = bTap( uv, texel, -2.0, -2.0 ); let b = bTap( uv, texel, 0.0, -2.0 ); let c = bTap( uv, texel, 2.0, -2.0 );
	let d = bTap( uv, texel, -2.0, 0.0 ); let e = bTap( uv, texel, 0.0, 0.0 ); let f = bTap( uv, texel, 2.0, 0.0 );
	let g = bTap( uv, texel, -2.0, 2.0 ); let h = bTap( uv, texel, 0.0, 2.0 ); let i = bTap( uv, texel, 2.0, 2.0 );
	let j = bTap( uv, texel, -1.0, -1.0 ); let k = bTap( uv, texel, 1.0, -1.0 ); let l = bTap( uv, texel, -1.0, 1.0 ); let m = bTap( uv, texel, 1.0, 1.0 );

	let s = e * 0.125 + ( a + c + g + i ) * 0.03125 + ( b + d + f + h ) * 0.0625 + ( j + k + l + m ) * 0.125;
	return vec4f( s, 1.0 );
}
