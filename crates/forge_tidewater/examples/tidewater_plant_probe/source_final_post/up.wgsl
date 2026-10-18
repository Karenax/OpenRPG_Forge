// SPDX-License-Identifier: MIT
// Copyright 2026 DRG Software Solutions LLC.
// Tidewater 1438b1abfcaee3267092b75573014f4d9b4a983c, src/post/PostFX.js.
// See third_party/tidewater/LICENSE.
#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput
@group(0) @binding(0) var bSrc: texture_2d<f32>;
@group(0) @binding(1) var smpLinearClamp: sampler;
@group(0) @binding(2) var bBase: texture_2d<f32>;
fn luminance(c: vec3f) -> f32 { return dot(c, vec3f(0.2126, 0.7152, 0.0722)); }

fn bTap( uv: vec2f, texel: vec2f, x: f32, y: f32 ) -> vec3f { return textureSampleLevel( bSrc, smpLinearClamp, uv + texel * vec2f( x, y ), 0.0 ).rgb; }
fn karis( c: vec3f ) -> vec3f { return c / ( luminance( c ) + 1.0 ); }

@fragment fn fragment( in: FullscreenVertexOutput ) -> @location(0) vec4f {
	let uv = in.uv;
	let texel = 1.0 / vec2f( textureDimensions( bSrc ) );
	let s = ( bTap( uv, texel, 0.0, 0.0 ) * 4.0
		+ ( bTap( uv, texel, -1.0, 0.0 ) + bTap( uv, texel, 1.0, 0.0 ) + bTap( uv, texel, 0.0, -1.0 ) + bTap( uv, texel, 0.0, 1.0 ) ) * 2.0
		+ ( bTap( uv, texel, -1.0, -1.0 ) + bTap( uv, texel, 1.0, -1.0 ) + bTap( uv, texel, -1.0, 1.0 ) + bTap( uv, texel, 1.0, 1.0 ) ) ) / 16.0;
	return vec4f( textureSampleLevel( bBase, smpLinearClamp, uv, 0.0 ).rgb + s, 1.0 );
}
