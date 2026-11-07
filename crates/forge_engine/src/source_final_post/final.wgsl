#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput
struct Controls { grade: vec4f, effects: vec4f, timing: vec4f, frame: vec4u };
// SPDX-License-Identifier: MIT

// Copyright 2026 DRG Software Solutions LLC.

// Adapted from Tidewater src/post/PostFX.js and engine/render/wgsl/common.js.

// Upstream 1438b1abfcaee3267092b75573014f4d9b4a983c; third_party/tidewater/LICENSE.

// Callable final HDR grading, ACES, sRGB and dither. Lens/bloom/RCAS input

// and actual exposure must be supplied by the renderer, not baked outputs.

struct SourcePostParams {

 aoStrength: f32, bloom: f32, vignette: f32, saturation: f32,

 contrast: f32, warmth: f32, grain: f32, sharpen: f32,

};



fn RRTAndODTFit( v: vec3f ) -> vec3f {

	let a = v * ( v + 0.0245786 ) - 0.000090537;

	let b = v * ( 0.983729 * v + 0.4329510 ) + 0.238081;

	return a / b;

}



fn acesFilmicToneMapping( colorIn: vec3f, exposure: f32 ) -> vec3f {

	let ACESInputMat = transpose( mat3x3f(

		0.59719, 0.35458, 0.04823,

		0.07600, 0.90834, 0.01566,

		0.02840, 0.13383, 0.83777 ) );

	let ACESOutputMat = transpose( mat3x3f(

		1.60475, -0.53108, -0.07367,

		-0.10208, 1.10813, -0.00605,

		-0.00327, -0.07276, 1.07602 ) );

	var color = colorIn * exposure / 0.6;

	color = ACESInputMat * color;

	color = RRTAndODTFit( color );

	color = ACESOutputMat * color;

	return clamp( color, vec3f( 0.0 ), vec3f( 1.0 ) );

}



fn postHash( p: vec2u, f: u32 ) -> f32 {

	var x = p.x * 1664525u + p.y * 1013904223u + f * 2654435761u;

	x ^= x >> 16u; x *= 0x7feb352du; x ^= x >> 15u; x *= 0x846ca68bu; x ^= x >> 16u;

	return f32( x >> 8u ) / 16777216.0;

}



fn luminance( c: vec3f ) -> f32 { return dot( c, vec3f( 0.2126, 0.7152, 0.0722 ) ); }



fn linearToSrgb( c: vec3f ) -> vec3f {

	return select( 1.055 * pow( c, vec3f( 1.0 / 2.4 ) ) - 0.055, c * 12.92, c <= vec3f( 0.0031308 ) );

}



fn sourcePostGrade(hdrColor: vec3f, uv: vec2f, px: vec2u, frameIndex: u32, autoExposure: f32, exposure: f32, post: SourcePostParams) -> vec4f {

	var c = hdrColor * autoExposure;

	// white balance nudge + saturation + contrast around mid grey (in linear HDR)

	c = c * vec3f( 1.0 + post.warmth, 1.0, 1.0 - post.warmth );

	let l = luminance( c );

	c = mix( vec3f( l ), c, post.saturation );

	c = pow( max( c, vec3f( 0.0 ) ) / 0.18, vec3f( post.contrast ) ) * 0.18;

	// vignette (elliptical, soft)

	let dv = ( uv - 0.5 ) * vec2f( 1.0, 0.8 );

	let v = 1.0 - smoothstep( 0.25, 0.75, length( dv ) ) * post.vignette;

	c = c * v;

	// fine film grain (luminance-weighted): triangular white noise on the real pixel grid, new every

	// frame (a fixed pattern on a fixed grid showed as diagonal hatching in dark gradients)

	let fi = frameIndex;

	let n = ( postHash( px, fi ) + postHash( px + vec2u( 7919u, 104729u ), fi ) - 1.0 ) * 0.5;

	c = c + c * ( n * post.grain );

	// renderOutput: ACES filmic tone mapping with the exposure, sRGB transfer

	let t = acesFilmicToneMapping( c, exposure );

	// +-1 LSB triangular dither before the 8-bit output: no banding in the sky gradients

	let dq = ( postHash( px + vec2u( 31337u, 271u ), fi ) + postHash( px + vec2u( 1013u, 65537u ), fi ) - 1.0 ) / 255.0;

	return vec4f( linearToSrgb( t ) + vec3f( dq ), 1.0 );

}


@group(0) @binding(0) var hdr: texture_2d<f32>;
@group(0) @binding(1) var bloomTex: texture_2d<f32>;
@group(0) @binding(2) var smp: sampler;
@group(0) @binding(3) var<uniform> ctrl: Controls;
@group(0) @binding(4) var<storage, read> aeExposure: array<f32>;
// ViewTarget remains linear; Bevy's sRGB output attachment encodes once.
// Preserve negative dither values through the linear branch before output clamp.
fn sourceSrgbToLinear(c: vec3f) -> vec3f {
    return select(pow(max((c+0.055)/1.055,vec3f(0.0)),vec3f(2.4)), c/12.92, c<=vec3f(0.04045));
}
@group(0) @binding(5) var sceneDepth: texture_depth_2d;
fn tm( c: vec3f ) -> vec3f { return c / ( max( c.r, max( c.g, c.b ) ) + 1.0 ); }
fn loadResolved( p: vec2i ) -> vec3f { return textureLoad( hdr, p, 0 ).rgb; }

// RCAS (AMD FSR1 robust contrast-adaptive sharpening) on the resolved image: TAA converges to a
// slightly soft image, RCAS restores the detail without halos (the lobe is limited by local
// contrast). Done on a tonemapped proxy of the HDR values and inverted afterwards.
fn sourceRcas( uvIn: vec2f ) -> vec3f {
	let size = vec2i( textureDimensions( hdr ) );
	let pc = clamp( vec2i( uvIn * vec2f( size ) ), vec2i( 1 ), size - 2 );
	let e = tm( loadResolved( pc ) );
	let b = tm( loadResolved( pc + vec2i( 0, -1 ) ) ); let d = tm( loadResolved( pc + vec2i( -1, 0 ) ) );
	let f = tm( loadResolved( pc + vec2i( 1, 0 ) ) ); let h = tm( loadResolved( pc + vec2i( 0, 1 ) ) );
	let mn4 = min( min( b, d ), min( f, h ) );
	let mx4 = max( max( b, d ), max( f, h ) );
	let hitMin = min( mn4, e ) / ( mx4 * 4.0 + 1e-5 );
	let hitMax = ( vec3f( 1.0 ) - max( mx4, e ) ) / ( mn4 * 4.0 - 4.0 );
	let lobeRGB = max( - hitMin, hitMax );
	// the sky (depth 0) is only sharpened lightly: clouds and cirrus are soft by nature, and the lobe
	// turned their sampling noise into fine grain (sceneDepth: the scene depth, bound by the blur module)
	let dS = textureLoad( sceneDepth, clamp( vec2i( uvIn * vec2f( textureDimensions( sceneDepth ) ) ), vec2i( 0 ), vec2i( textureDimensions( sceneDepth ) ) - 1 ), 0 );
	let skyK = select( 1.0, 0.3, dS < 1e-7 );
	let lobe = max( -0.1875, min( max( lobeRGB.r, max( lobeRGB.g, lobeRGB.b ) ), 0.0 ) ) * ctrl.timing.z * skyK;
	let r = max( ( ( b + d + f + h ) * lobe + e ) / ( lobe * 4.0 + 1.0 ), vec3f( 0.0 ) );
	// back to HDR (inverse of the max-channel Reinhard)
	return r / max( 1.0 - max( r.r, max( r.g, r.b ) ), 1e-3 );
}


@fragment fn fragment(in: FullscreenVertexOutput) -> @location(0) vec4f {
    let c = sourceRcas(in.uv)
          + textureSampleLevel(bloomTex,smp,in.uv,0.0).rgb * ctrl.effects.y;
    let post = SourcePostParams(1.0,ctrl.effects.y,ctrl.grade.x,ctrl.grade.y,
                              ctrl.grade.z,ctrl.grade.w,ctrl.effects.x,0.0);
    let graded = sourcePostGrade(c,in.uv,vec2u(in.position.xy),ctrl.frame.x,
                                aeExposure[0],ctrl.effects.z,post);
    return vec4f(sourceSrgbToLinear(graded.rgb),1.0);
}
