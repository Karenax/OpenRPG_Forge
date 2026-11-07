// SPDX-License-Identifier: MIT
// Adapted from Tidewater spray sprites, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; see third_party/tidewater/LICENSE.
diagnostic(off, derivative_uniformity);
#import bevy_pbr::{mesh_view_bindings::view,view_transformations::position_world_to_clip}
struct Frame {
	sourceView: mat4x4f,
	proj: mat4x4f,
	viewProj: mat4x4f,
	invView: mat4x4f,
	invProj: mat4x4f,
	invViewProj: mat4x4f,
	viewProjNoJitter: mat4x4f,
	prevViewProjNoJitter: mat4x4f,
	cameraPos: vec3f,
	near: f32,
	prevCameraPos: vec3f,
	far: f32,
	resolution: vec2f,
	invResolution: vec2f,
	outputResolution: vec2f,
	jitter: vec2f,
	frameIndex: u32,
	time: f32,
	dt: f32,
	seaLevel: f32,
	sunDir: vec3f,
	night: f32,
	sunColor: vec3f,
	exposure: f32,
	skyIrradiance: vec3f,
	cameraUnderwater: f32,
	horizonColor: vec3f,
	cameraWaterHeight: f32,
	waterAbsorption: vec3f,
	windSpeed: f32,
	waterScattering: vec3f,
	envIntensity: f32,
	windDir: vec2f,
	reversedDepth: f32,
	pad0: f32,
	debug: vec4f,
};
struct SprayMaterialParams {
	color: vec3f,
	opacity: f32,
	emissive: vec3f,
	roughness: f32,
	metalness: f32,
	alphaTest: f32,
	intensity: f32,
	maxDistance: f32,
};

var<private> frame:Frame;
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> sprayFrame:Frame;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var<storage,read> sprayPosR:array<vec4f>;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var<storage,read> sprayVelR:array<vec4f>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var<storage,read> sprayInfoR:array<vec4f>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var<storage,read> breakersCrest:array<vec4f>;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var sprayPuff:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(6) var smpLinearRepeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(7) var sprayDots:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(8) var spraySceneDepth:texture_depth_2d;
@group(#{MATERIAL_BIND_GROUP}) @binding(9) var<uniform> mat:SprayMaterialParams;

const PI: f32 = 3.141592653589793;
const TWO_PI: f32 = 6.283185307179586;
const INV_PI: f32 = 0.3183098861837907;
const EPS: f32 = 1e-5;

fn sat( x: f32 ) -> f32 { return clamp( x, 0.0, 1.0 ); }
fn sat3( x: vec3f ) -> vec3f { return clamp( x, vec3f( 0.0 ), vec3f( 1.0 ) ); }
fn pow2( x: f32 ) -> f32 { return x * x; }
fn pow4( x: f32 ) -> f32 { let y = x * x; return y * y; }
fn pow5( x: f32 ) -> f32 { let y = x * x; return y * y * x; }
fn luminance( c: vec3f ) -> f32 { return dot( c, vec3f( 0.2126, 0.7152, 0.0722 ) ); }
fn remap( x: f32, a: f32, b: f32, c: f32, d: f32 ) -> f32 { return c + ( x - a ) * ( d - c ) / ( b - a ); }
fn remapClamp( x: f32, a: f32, b: f32, c: f32, d: f32 ) -> f32 { return mix( c, d, sat( ( x - a ) / ( b - a ) ) ); }
fn rotate2( v: vec2f, a: f32 ) -> vec2f { let c = cos( a ); let s = sin( a ); return vec2f( c * v.x - s * v.y, s * v.x + c * v.y ); }

// ---- depth (reversed-Z: 1 at the near plane, 0 at far / infinity)

// positive view-space distance along the view axis from a depth-buffer value
fn viewDepth( d: f32 ) -> f32 {
	let v = frame.invProj * vec4f( 0.0, 0.0, d, 1.0 );
	return - v.z / v.w;
}

// uv (0..1, y down) + depth -> world / view position
fn ndcFromUv( uv: vec2f, d: f32 ) -> vec4f { return vec4f( uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, d, 1.0 ); }
fn worldFromDepth( uv: vec2f, d: f32 ) -> vec3f {
	let p = frame.invViewProj * ndcFromUv( uv, d );
	return p.xyz / p.w;
}
fn viewFromDepth( uv: vec2f, d: f32 ) -> vec3f {
	let p = frame.invProj * ndcFromUv( uv, d );
	return p.xyz / p.w;
}
// world direction of the camera ray through uv
fn viewRay( uv: vec2f ) -> vec3f {
	let p = frame.invViewProj * ndcFromUv( uv, 0.5 );
	return normalize( p.xyz / p.w - frame.cameraPos );
}
// world position -> uv (y down) and depth
fn projectToUv( P: vec3f ) -> vec3f {
	let c = frame.viewProjNoJitter * vec4f( P, 1.0 );
	let n = c.xyz / c.w;
	return vec3f( n.x * 0.5 + 0.5, 0.5 - n.y * 0.5, n.z );
}
fn isSky( d: f32 ) -> bool { return d <= 0.0; }

// ---- hashes

fn pcg( v: u32 ) -> u32 {
	let state = v * 747796405u + 2891336453u;
	let word = ( ( state >> ( ( state >> 28u ) + 4u ) ) ^ state ) * 277803737u;
	return ( word >> 22u ) ^ word;
}
fn pcg3( v0: vec3u ) -> vec3u {
	var v = v0 * 1664525u + 1013904223u;
	v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
	v ^= v >> vec3u( 16u );
	v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
	return v;
}
fn u32ToUnit( h: u32 ) -> f32 { return f32( h >> 8u ) * ( 1.0 / 16777216.0 ) + ( 0.5 / 16777216.0 ); }
// float hash in [0, 1) of a float seed (TSL hash())
fn hash11( p: f32 ) -> f32 { return u32ToUnit( pcg( bitcast<u32>( p ) ^ 0x9e3779b9u ) ); }
fn hash21( p: vec2f ) -> f32 { return u32ToUnit( pcg( bitcast<u32>( p.x ) ^ pcg( bitcast<u32>( p.y ) ) ) ); }
fn hash31( p: vec3f ) -> f32 { return u32ToUnit( pcg3( bitcast<vec3u>( p ) ).x ); }
fn hash22( p: vec2f ) -> vec2f {
	let h = pcg3( vec3u( bitcast<vec2u>( p ), 0x51ed270bu ) );
	return vec2f( u32ToUnit( h.x ), u32ToUnit( h.y ) );
}
fn hash33( p: vec3f ) -> vec3f {
	let h = pcg3( bitcast<vec3u>( p ) );
	return vec3f( u32ToUnit( h.x ), u32ToUnit( h.y ), u32ToUnit( h.z ) );
}
fn hashU( a: u32, b: u32 ) -> f32 { return u32ToUnit( pcg( a ^ pcg( b ) ) ); }
fn ihash3( p: vec3i ) -> vec3u { return pcg3( bitcast<vec3u>( p ) ); }

// Jimenez interleaved gradient noise at a pixel (0..1)
fn interleavedGradientNoise( px: vec2f ) -> f32 { return fract( 52.9829189 * fract( dot( px, vec2f( 0.06711056, 0.00583715 ) ) ) ); }

// i-th of n points of a Vogel disc (unit radius), rotated by phi
fn vogelDiskSample( i: i32, n: i32, phi: f32 ) -> vec2f {
	let r = sqrt( ( f32( i ) + 0.5 ) / f32( n ) );
	let theta = f32( i ) * 2.399963229728653 + phi;
	return vec2f( cos( theta ), sin( theta ) ) * r;
}

// ---- gradient noise: MaterialX (three's MaterialXNoise.js) bit for bit — the same Jenkins
// lookup3 hash, gradients, quintic fade and gradient scales (0.6616 in 2D, 0.982 in 3D), so the
// procedural patterns land where they did in the three.js version.

fn _mxRotl( x: u32, k: u32 ) -> u32 { return ( x << k ) | ( x >> ( 32u - k ) ); }
fn _mxFinal( a0: u32, b0: u32, c0: u32 ) -> u32 {
	var a = a0; var b = b0; var c = c0;
	c ^= b; c -= _mxRotl( b, 14u );
	a ^= c; a -= _mxRotl( c, 11u );
	b ^= a; b -= _mxRotl( a, 25u );
	c ^= b; c -= _mxRotl( b, 16u );
	a ^= c; a -= _mxRotl( c, 4u );
	b ^= a; b -= _mxRotl( a, 14u );
	c ^= b; c -= _mxRotl( b, 24u );
	return c;
}
fn mxHash2( x: i32, y: i32 ) -> u32 { let s = 0xdeadbeefu + ( 2u << 2u ) + 13u; return _mxFinal( s + u32( x ), s + u32( y ), s ); }
fn mxHash3( x: i32, y: i32, z: i32 ) -> u32 { let s = 0xdeadbeefu + ( 3u << 2u ) + 13u; return _mxFinal( s + u32( x ), s + u32( y ), s + u32( z ) ); }
fn _mxGrad2( hash: u32, x: f32, y: f32 ) -> f32 {
	let h = hash & 7u;
	let u = select( y, x, h < 4u );
	let v = 2.0 * select( x, y, h < 4u );
	return select( u, - u, ( h & 1u ) != 0u ) + select( v, - v, ( h & 2u ) != 0u );
}
fn _gradDot3( h: u32, p: vec3f ) -> f32 {
	let hh = h & 15u;
	let u = select( p.y, p.x, hh < 8u );
	let v = select( select( p.z, p.x, hh == 12u || hh == 14u ), p.y, hh < 4u );
	return select( u, - u, ( hh & 1u ) != 0u ) + select( v, - v, ( hh & 2u ) != 0u );
}
fn _fade3( t: vec3f ) -> vec3f { return t * t * t * ( t * ( t * 6.0 - 15.0 ) + 10.0 ); }
fn _h3( i: vec3i ) -> u32 { return mxHash3( i.x, i.y, i.z ); }

fn perlin3( p: vec3f ) -> f32 {
	let fl = floor( p );
	let i = vec3i( fl );
	let f = p - fl;
	let u = _fade3( f );
	let n000 = _gradDot3( _h3( i ), f );
	let n100 = _gradDot3( _h3( i + vec3i( 1, 0, 0 ) ), f - vec3f( 1.0, 0.0, 0.0 ) );
	let n010 = _gradDot3( _h3( i + vec3i( 0, 1, 0 ) ), f - vec3f( 0.0, 1.0, 0.0 ) );
	let n110 = _gradDot3( _h3( i + vec3i( 1, 1, 0 ) ), f - vec3f( 1.0, 1.0, 0.0 ) );
	let n001 = _gradDot3( _h3( i + vec3i( 0, 0, 1 ) ), f - vec3f( 0.0, 0.0, 1.0 ) );
	let n101 = _gradDot3( _h3( i + vec3i( 1, 0, 1 ) ), f - vec3f( 1.0, 0.0, 1.0 ) );
	let n011 = _gradDot3( _h3( i + vec3i( 0, 1, 1 ) ), f - vec3f( 0.0, 1.0, 1.0 ) );
	let n111 = _gradDot3( _h3( i + vec3i( 1, 1, 1 ) ), f - vec3f( 1.0, 1.0, 1.0 ) );
	let x0 = mix( mix( n000, n100, u.x ), mix( n010, n110, u.x ), u.y );
	let x1 = mix( mix( n001, n101, u.x ), mix( n011, n111, u.x ), u.y );
	return mix( x0, x1, u.z ) * 0.982;
}
fn perlin2( p: vec2f ) -> f32 {
	let fl = floor( p );
	let X = i32( fl.x ); let Y = i32( fl.y );
	let fx = p.x - fl.x; let fy = p.y - fl.y;
	let u = _fade3( vec3f( fx, fy, 0.0 ) );
	let v0 = _mxGrad2( mxHash2( X, Y ), fx, fy );
	let v1 = _mxGrad2( mxHash2( X + 1, Y ), fx - 1.0, fy );
	let v2 = _mxGrad2( mxHash2( X, Y + 1 ), fx, fy - 1.0 );
	let v3 = _mxGrad2( mxHash2( X + 1, Y + 1 ), fx - 1.0, fy - 1.0 );
	let s1 = 1.0 - u.x;
	return ( ( 1.0 - u.y ) * ( v0 * s1 + v1 * u.x ) + u.y * ( v2 * s1 + v3 * u.x ) ) * 0.6616;
}
fn mx_noise_float3( p: vec3f ) -> f32 { return perlin3( p ); }
fn mx_noise_float2( p: vec2f ) -> f32 { return perlin2( p ); }
// MaterialX vec3 noise: one hash per corner, its three low bytes pick the gradients
fn _mxGrad3v( h: u32, p: vec3f ) -> vec3f { return vec3f( _gradDot3( h & 0xffu, p ), _gradDot3( ( h >> 8u ) & 0xffu, p ), _gradDot3( ( h >> 16u ) & 0xffu, p ) ); }
fn mx_noise_vec3( p: vec3f ) -> vec3f {
	let fl = floor( p );
	let i = vec3i( fl );
	let f = p - fl;
	let u = _fade3( f );
	let n000 = _mxGrad3v( _h3( i ), f );
	let n100 = _mxGrad3v( _h3( i + vec3i( 1, 0, 0 ) ), f - vec3f( 1.0, 0.0, 0.0 ) );
	let n010 = _mxGrad3v( _h3( i + vec3i( 0, 1, 0 ) ), f - vec3f( 0.0, 1.0, 0.0 ) );
	let n110 = _mxGrad3v( _h3( i + vec3i( 1, 1, 0 ) ), f - vec3f( 1.0, 1.0, 0.0 ) );
	let n001 = _mxGrad3v( _h3( i + vec3i( 0, 0, 1 ) ), f - vec3f( 0.0, 0.0, 1.0 ) );
	let n101 = _mxGrad3v( _h3( i + vec3i( 1, 0, 1 ) ), f - vec3f( 1.0, 0.0, 1.0 ) );
	let n011 = _mxGrad3v( _h3( i + vec3i( 0, 1, 1 ) ), f - vec3f( 0.0, 1.0, 1.0 ) );
	let n111 = _mxGrad3v( _h3( i + vec3i( 1, 1, 1 ) ), f - vec3f( 1.0, 1.0, 1.0 ) );
	let x0 = mix( mix( n000, n100, u.x ), mix( n010, n110, u.x ), u.y );
	let x1 = mix( mix( n001, n101, u.x ), mix( n011, n111, u.x ), u.y );
	return mix( x0, x1, u.z ) * 0.982;
}
fn mx_fractal_noise_float3( p: vec3f, octaves: i32, lacunarity: f32, diminish: f32 ) -> f32 {
	var r = 0.0; var amp = 1.0; var q = p;
	for ( var i = 0; i < octaves; i++ ) { r += amp * perlin3( q ); amp *= diminish; q *= lacunarity; }
	return r;
}
fn mx_cell_noise_float3( p: vec3f ) -> f32 { let i = vec3i( floor( p ) ); return f32( mxHash3( i.x, i.y, i.z ) ) / f32( 0xffffffffu ); }
fn mx_cell_noise_float2( p: vec2f ) -> f32 { let i = vec2i( floor( p ) ); return f32( mxHash2( i.x, i.y ) ) / f32( 0xffffffffu ); }
// distances to the nearest two feature points (F1, F2), jitter 0..1
fn mx_worley_noise_vec2_3( p: vec3f, jitter: f32 ) -> vec2f {
	let i = vec3i( floor( p ) );
	let f = fract( p );
	var d1 = 1e9; var d2 = 1e9;
	for ( var z = -1; z <= 1; z++ ) { for ( var y = -1; y <= 1; y++ ) { for ( var x = -1; x <= 1; x++ ) {
		let c = vec3i( x, y, z );
		let h = ihash3( i + c );
		let o = vec3f( f32( c.x ), f32( c.y ), f32( c.z ) ) + ( vec3f( u32ToUnit( h.x ), u32ToUnit( h.y ), u32ToUnit( h.z ) ) - 0.5 ) * jitter + 0.5 - f;
		let d = dot( o, o );
		if ( d < d1 ) { d2 = d1; d1 = d; } else if ( d < d2 ) { d2 = d; }
	} } }
	return sqrt( vec2f( d1, d2 ) );
}
fn mx_worley_noise_vec2_2( p: vec2f, jitter: f32 ) -> vec2f {
	let i = vec2i( floor( p ) );
	let f = fract( p );
	var d1 = 1e9; var d2 = 1e9;
	for ( var y = -1; y <= 1; y++ ) { for ( var x = -1; x <= 1; x++ ) {
		let c = vec2i( x, y );
		let h = ihash3( vec3i( i + c, 0 ) );
		let o = vec2f( f32( c.x ), f32( c.y ) ) + ( vec2f( u32ToUnit( h.x ), u32ToUnit( h.y ) ) - 0.5 ) * jitter + 0.5 - f;
		let d = dot( o, o );
		if ( d < d1 ) { d2 = d1; d1 = d; } else if ( d < d2 ) { d2 = d; }
	} }
	return sqrt( vec2f( d1, d2 ) );
}

// ---- normals

// orthonormal basis around n (Frisvad / Duff et al.)
fn basis( n: vec3f ) -> mat3x3f {
	let s = select( -1.0, 1.0, n.z >= 0.0 );
	let a = -1.0 / ( s + n.z );
	let b = n.x * n.y * a;
	let t = vec3f( 1.0 + s * n.x * n.x * a, s * b, -s * n.x );
	let bt = vec3f( b, s + n.y * n.y * a, -n.y );
	return mat3x3f( t, bt, n );
}

// Perturb a world-space normal by a height field sampled with screen-space derivatives
// (Mikkelsen, "Bump Mapping Unparametrized Surfaces"; TSL perturbNormal equivalent).
// dhdx / dhdy: dpdx / dpdy of the height (in metres) at this pixel.
fn perturbNormalByHeight( P: vec3f, N: vec3f, dhdx: f32, dhdy: f32, strength: f32 ) -> vec3f {
	let dPdx = dpdx( P );
	let dPdy = dpdy( P );
	let r1 = cross( dPdy, N );
	let r2 = cross( N, dPdx );
	let det = dot( dPdx, r1 );
	let grad = sign( det ) * ( dhdx * r1 + dhdy * r2 ) * strength;
	return normalize( abs( det ) * N - grad );
}

// tangent-space normal map sample (xy in -1..1) applied with a derivative-based TBN
fn perturbNormalByMap( P: vec3f, N: vec3f, uv: vec2f, mapN: vec3f ) -> vec3f {
	let dp1 = dpdx( P ); let dp2 = dpdy( P );
	let duv1 = dpdx( uv ); let duv2 = dpdy( uv );
	let dp2perp = cross( dp2, N ); let dp1perp = cross( N, dp1 );
	let T = dp2perp * duv1.x + dp1perp * duv2.x;
	let B = dp2perp * duv1.y + dp1perp * duv2.y;
	let invmax = inverseSqrt( max( max( dot( T, T ), dot( B, B ) ), 1e-20 ) );
	return normalize( mat3x3f( T * invmax, B * invmax, N ) * mapN );
}

// ---- color

fn srgbToLinear( c: vec3f ) -> vec3f {
	return select( pow( ( c + 0.055 ) / 1.055, vec3f( 2.4 ) ), c / 12.92, c <= vec3f( 0.04045 ) );
}
fn linearToSrgb( c: vec3f ) -> vec3f {
	return select( 1.055 * pow( c, vec3f( 1.0 / 2.4 ) ) - 0.055, c * 12.92, c <= vec3f( 0.0031308 ) );
}


const SPRAY_DROPLET: f32 = 0.0;
const SPRAY_MIST: f32 = 1.0;
const SPRAY_LIGAMENT: f32 = 2.0;
const SPRAY_SPRAY: f32 = 3.0;
const SPRAY_SHEET: f32 = 4.0;

// PCG hash of a uint -> [0, 1)
fn sprayHash( seed: u32 ) -> f32 {
	let state = seed * 747796405u + 2891336453u;
	let word = ( ( state >> ( ( state >> 28u ) + 4u ) ) ^ state ) * 277803737u;
	return f32( ( word >> 22u ) ^ word ) * ( 1.0 / 4294967296.0 );
}

// select a per-kind constant
fn sprayByKind( kind: f32, a: f32, b: f32, c: f32, d: f32, e: f32 ) -> f32 {
	return select( select( select( select( e, d, kind < 3.5 ), c, kind < 2.5 ), b, kind < 1.5 ), a, kind < 0.5 );
}

// Henyey-Greenstein phase (1/sr)
fn sprayPhaseHG( cosT: f32, g: f32 ) -> f32 {
	let g2 = g * g;
	return ( ( 1.0 - g2 ) / ( 4.0 * PI ) ) / pow( max( 1.0 + g2 - 2.0 * g * cosT, 1e-4 ), 1.5 );
}


fn breakersSprayShadow( p: vec3f, seedTag: f32 ) -> f32 {
	var out = 1.0;
	let idx = floor( seedTag ) - 1.0;
	if ( idx >= 0.0 && idx < 1154.0 ) {
		let k = u32( idx ) * 3u;
		let c0 = breakersCrest[ k ];
		let c1 = breakersCrest[ k + 1u ];
		let c2 = breakersCrest[ k + 2u ];
		let L = frame.sunDir;
		let d2 = c2.xy;
		let Ld = dot( L.xz, d2 ); // < 0: the sun is on the sea side of the wave
		if ( c2.w > 0.5 && Ld < -0.02 ) {
			let root = c0.xyz;
			let b = c0.w;
			let H = c1.w;
			let q = clamp( b / 0.9, 0.0, 1.0 ) * ( 1.0 - smoothstep( 1.0, 1.3, b ) );
			// vertical plane through the crest (moved forward under the overhanging lip)
			let plane = root.xz + d2 * ( H * 0.8 * q * 0.6 );
			let s = dot( p.xz - plane, d2 ); // > 0: in front of it
			let tau = s / - Ld;
			let yRay = p.y + L.y * tau; // height of the ray toward the sun where it crosses the plane
			let top = root.y + 0.05;
			let shade = smoothstep( top, top - 0.4, yRay ) * smoothstep( -0.1, 0.1, s ) * smoothstep( 14.0, 6.0, s );
			out = 1.0 - shade * 0.55;
		}
	}
	return out;
}

fn sprayCloudShadow( xz: vec2f ) -> f32 { return cloudsShadow( xz ); }
fn sprayWaveShadow( p: vec3f, tag: f32 ) -> f32 { return breakersSprayShadow( p, tag ); }


fn setupFrame(){frame=sprayFrame;frame.sourceView=view.view_from_world;frame.proj=view.clip_from_view;frame.invView=view.world_from_view;frame.invProj=view.view_from_clip;frame.invViewProj=view.world_from_clip;frame.cameraPos=view.world_position;}
struct SpriteVertex{@builtin(position) clip:vec4f,@location(0) P:vec3f,@location(1) vUV:vec2f,@location(2) vCol:vec4f,@location(3) vMisc:vec4f,@location(4) vFwd:vec4f};
@vertex fn vertex(@location(0) position:vec3f,@location(1) slot:u32)->SpriteVertex{
 setupFrame();var o:SpriteVertex;

	let posA = sprayPosR[ slot ];
	let velA = sprayVelR[ slot ];
	let info = sprayInfoR[ slot ];
	let p = posA.xyz;
	let age = posA.w;
	let vel = velA.xyz;
	let kind = info.x;
	let life = info.y;
	let isDrop = kind < 0.5;
	let isLig = kind > 1.5 && kind < 2.5;
	let isMist = kind > 0.5 && kind < 1.5;
	let water = isDrop || isLig; // clear water: drops and ligaments
	let alive = life > 0.0 && age < life;

	let toCam = frame.cameraPos - p;
	let dist = max( length( toCam ), 0.05 );
	let Vd = toCam / dist;

	// pixel footprint at this distance: drops are drawn at least ~1.3 px wide
	let p11 = frame.proj[ 1 ][ 1 ];
	let pixel = dist * 2.0 / ( p11 * frame.resolution.y );
	let r0 = velA.w; // radius (m)
	// dense spray is thrown out of the splash as a compact mass and spreads (grows from ~half size)
	let tAge = age / max( life, 1e-3 );
	let r = select( r0, r0 * mix( 0.45, 1.0, smoothstep( 0.0, 0.25, tAge ) ), kind > 2.5 && kind < 3.5 );
	let size = max( r, pixel * 1.3 );

	// motion blur along the velocity projected on the view plane; ligaments are elongated anyway
	let vPerp = vel - Vd * dot( vel, Vd );
	let speed = length( vPerp );
	let stretchLen = speed * sprayByKind( kind, 0.025, 0.0, 0.025, 0.03333333333333333, 0.016666666666666666 );
	let elong = select( 0.0, r * 2.0, isLig );
	let up = select( vec3f( 0.0, 1.0, 0.0 ), vPerp / max( speed, 1e-3 ), speed > 1e-3 );
	// clouds: random rotation that slowly turns
	let rot = fract( info.w ) * 6.283 + age * ( fract( info.w ) - 0.5 );
	let camRight = normalize( cross( vec3f( 0.0, 1.0, 0.0 ), Vd ) + vec3f( 1e-5, 0.0, 0.0 ) );
	let camUp = cross( Vd, camRight );
	let mRight = camRight * cos( rot ) + camUp * sin( rot );
	let mUp = camUp * cos( rot ) - camRight * sin( rot );
	// torn sheets (dense spray) are drawn along their motion too, tilted a little at random and
	// stretched by their speed: fibrous, streaked silhouettes instead of round puffs
	let isSheet = kind > 2.5;
	let isClear = kind > 3.5; // clear sheet (bow sheet)
	let side = normalize( cross( Vd, up ) );
	let tilt = ( fract( info.w * 7.31 ) - 0.5 ) * 0.7;
	let sUp = up * cos( tilt ) + side * sin( tilt );
	let sSide = side * cos( tilt ) - up * sin( tilt );
	// mist streams with the air: drawn along its motion, stretched by its speed (wisps, not discs)
	let alongMotion = isSheet || ( isMist && speed > 0.3 );
	let axisY = select( select( mUp, sUp, alongMotion ), up, water );
	let axisX = select( select( mRight, sSide, alongMotion ), side, water );
	let sheetLen = select( 0.0, size * clamp( speed * 0.08, 0.0, 0.8 ), isSheet );
	let mistLen = select( 0.0, size * clamp( ( speed - 0.3 ) * 0.35, 0.0, 1.4 ), isMist );
	let halfY = size + stretchLen * 0.5 + elong + sheetLen + mistLen;
	let halfX = select( size, size * 1.05, isSheet );
	let corner = position.xy;
	let world = p + axisX * ( corner.x * halfX ) + axisY * ( corner.y * halfY );

	// coverage that conserves the water's cross-section: the drop's projected area (and the time it
	// spends on each pixel of its streak) spread over the drawn footprint. For the gaussian
	// footprint exp( -k d^2 ) the peak is k r (r + elong) / ( halfX halfY ). Clouds: lost to the clamp.
	let kShape = select( 3.5, 4.5, isLig );
	let cover = select( sat( r / size ), min( r * ( r + elong ) * kShape / ( halfX * halfY ), 1.0 ), water );

	// ---- lighting (per particle)
	let L = frame.sunDir;
	let cosT = dot( - Vd, L ); // 1 = looking toward the sun through the particle
	let cosA = dot( Vd, L );
	let sunVis = sprayCloudShadow( p.xz ) * sprayWaveShadow( p, info.w );
	let sun = frame.sunColor * sunVis;
	// clear water (drop, ligament): the bright sky it refracts and reflects, a strong forward lobe
	// (diffraction + refraction) when backlit, a small glint from any side
	let cWater = frame.skyIrradiance * 0.9 + sun * ( sprayPhaseHG( cosT, 0.85 ) * 1.2 + 0.12 );
	// dense spray: multiply scattered, white from any side (a diffuse sphere: its far side is in its
	// own shadow), plus a forward lobe
	let lambert = ( sqrt( max( 1.0 - cosA * cosA, 0.0 ) ) + ( PI - acos( clamp( cosA, -1.0, 1.0 ) ) ) * cosA ) / PI;
	// (the forward lobe is passed on separately: thin, torn parts glow with it, thick parts shade it)
	let cSpray = sun * ( ( lambert * 0.65 + 0.35 ) / PI ) + frame.skyIrradiance * 1.15;
	let fSpray = sun * ( sprayPhaseHG( cosT, 0.6 ) * 0.9 );
	// mist: a thin veil of fine drops, strongly forward scattering, tinted by the sky
	let cMist = sun * ( 0.25 / PI ) + frame.skyIrradiance * 0.9;
	let fMist = sun * sprayPhaseHG( cosT, 0.75 );
	// clear sheet: thin water, the sky it shows and a little sun off its surface; the sun shining
	// through it (forward lobe) is passed on: the thin torn parts glow when backlit
	let cClear = frame.skyIrradiance * 0.95 + sun * 0.05;
	let fClear = sun * ( sprayPhaseHG( cosT, 0.8 ) * 1.1 );
	let col = select( select( select( cSpray, cClear, isClear ), cMist, isMist ), cWater, water );
	o.vFwd = vec4f( select( select( select( fSpray, fClear, isClear ), fMist, isMist ), vec3f( 0.0 ), water ), halfX / pixel );

	// opacity over the particle's life
	let t = age / max( life, 1e-3 );
	let fadeIn = smoothstep( 0.0, sprayByKind( kind, 0.02, 0.2, 0.02, 0.12, 0.02 ), t );
	let fadeOut = 1.0 - smoothstep( sprayByKind( kind, 0.8, 0.45, 0.8, 0.7, 0.5 ), 1.0, t );
	let baseA = sprayByKind( kind, 0.55, 0.06, 0.8, 0.66, 0.22 );
	// far: fade out; very near the eye: sheets and mist would fill the screen (and cost a lot of overdraw)
	let distFade = smoothstep( mat.maxDistance, mat.maxDistance * 0.55, dist ) * select( smoothstep( 0.6, 3.0, dist ), 1.0, water );
	let a = baseA * fadeIn * fadeOut * cover * distFade * mat.intensity;

	o.vUV = corner;
	o.vCol = vec4f( col, a );
	// x: kind + 0.45 * life fraction (the kind tests below have 0.5 of margin)
	o.vMisc = vec4f( kind + sat( t ) * 0.45, info.z, size, fract( info.w ) );

	// dead particles collapse off-screen
o.P=select(vec3f(0.0,-1e5,0.0),world,alive && a > 1e-4);o.clip=position_world_to_clip(o.P);return o;}
struct SpriteInput{P:vec3f,pixel:vec2f,vs:SpriteVertex};
struct SpriteResult{color:vec4f};
fn shade(in:SpriteInput)->SpriteResult{var r:SpriteResult;

	let uv = in.vs.vUV;
	let vMisc = in.vs.vMisc;
	let kind = vMisc.x;
	let isDrop = kind < 0.5;
	let isLig = kind > 1.5 && kind < 2.5;
	let isSheet = kind > 2.5;
	let isClear = kind > 3.5;
	let t = sat( fract( kind ) / 0.45 ); // life fraction
	let r2 = dot( uv, uv );
	let sd = vec2f( vMisc.w, vMisc.w * 1.7 );
	// drop: gaussian streak; ligament: a slightly sharper, beaded blob
	let drop = max( exp( r2 * - 3.5 ) - 0.03, 0.0 );
	let lig = max( exp( r2 * - 4.5 ) * ( sin( uv.y * 5.0 + vMisc.w * 40.0 ) * 0.2 + 0.9 ) - 0.03, 0.0 );
	// torn sheet: noise streaked along the motion (uv.y), eroded from its edges inward and more and
	// more as it ages, so it tears into strands and fragments instead of shrinking. Thick parts are
	// dense white water, the torn edges thin and translucent.
	let env = sat( 1.0 - r2 );
	let fib = textureSample( sprayPuff, smpLinearRepeat, vec2f( uv.x * 0.5, uv.y * 0.26 ) + sd ).x;
	let fine = textureSample( sprayPuff, smpLinearRepeat, vec2f( uv.x * 1.2, uv.y * 0.6 ) + sd * 2.3 ).x;
	let field = fib * 0.6 + fine * 0.4 + ( env - 0.55 ) * 0.75 - smoothstep( 0.8, 1.0, r2 );
	// a torn sheet only a few pixels across can't show its tears: a solid white dot, and a cluster of
	// them reads as cauliflower puffs. Small on screen, it is drawn thinner and more torn: a far
	// splash-up is a ragged, see-through burst
	let farK = smoothstep( 14.0, 3.0, in.vs.vFwd.w ) * select( 0.0, 1.0, isSheet && ! isClear );
	let erode = mix( 0.26, 0.7, t ) + farK * 0.16;
	let dens = sat( ( field - erode ) * 3.0 );
	// torn edges are thin, translucent water: soft and see-through, the core dense white
	let torn = smoothstep( erode - 0.04, erode + 0.2, field ) * ( dens * 0.45 + 0.55 );
	// ... which breaks up into a cluster of drops: many small dots in a ragged envelope that thins
	// out as it ages (sub-pixel drops average out through the mipmaps: never a solid blob)
	let dots = textureSample( sprayDots, smpLinearRepeat, uv * vec2f( 0.5, 0.32 ) + sd * 3.7 ).x;
	let swarm = dots * smoothstep( 0.25, 0.55, fib + env * 0.45 - t * 0.2 ) * ( 1.0 - t * 0.5 );
	let sheet = max( torn * ( 1.0 - smoothstep( 0.15, 0.6, t ) ), swarm );
	// mist: a soft veil of low-frequency noise that drifts and thins, never a disc
	let m1 = textureSample( sprayPuff, smpLinearRepeat, uv * 0.2 + sd ).x;
	let m2 = textureSample( sprayPuff, smpLinearRepeat, uv * 0.55 + sd * 3.1 ).x;
	let veil = smoothstep( 0.28, 0.8, m1 * 0.7 + m2 * 0.3 + env * 0.3 - 0.12 ) * sqrt( env ) * ( 1.0 - t * 0.4 );
	let shape = select( select( select( veil, sheet, isSheet ), lig, isLig ), drop, isDrop );
	// self-shadowing inside thick sheets; the forward-scattered sun lights up the thin parts
	let shade = select( 1.0, 1.0 - dens * 0.15, isSheet && ! isClear );
	let glow = select( select( 1.0, 1.0 - dens * 0.6, isSheet ), 1.0 - dens * 0.3, isClear );

	// soft intersections: opaque scene depth and the water surface under the particle
	// (depth via exact texel loads; reversed-Z, view z negative in front of the camera)
	let dsz = vec2f( textureDimensions( spraySceneDepth ) );
	let q = vec2i( clamp( in.pixel * frame.invResolution, vec2f( 0.0 ), vec2f( 0.9999 ) ) * dsz );
	let sceneZ = - viewDepth( textureLoad( spraySceneDepth, q, 0 ) );
	let posViewZ = ( frame.sourceView * vec4f( in.P, 1.0 ) ).z;
	let soft = vMisc.z * 1.5 + 0.03;
	let fadeScene = sat( ( posViewZ - sceneZ ) / soft );
	let fadeWater = sat( ( in.P.y - vMisc.y ) / ( soft * 0.6 ) + 0.15 );
	let aOut = in.vs.vCol.w * shape * fadeScene * fadeWater * ( 1.0 - farK * 0.4 );
	if ( aOut < 0.002 ) { discard; }
	r.color = vec4f( ( in.vs.vCol.rgb * shade + in.vs.vFwd.xyz * glow ) * aOut, aOut );

return r;}
@fragment fn fragment(v:SpriteVertex)->@location(0) vec4f{setupFrame();var i:SpriteInput;i.P=v.P;i.pixel=v.clip.xy;i.vs=v;return shade(i).color;}

struct CloudsParams {
	viewRight: vec3f,
	viewUp: vec3f,
	viewFwd: vec3f,
	viewTan: vec2f,
	viewValid: f32,
	shadowCenter: vec2f,
	shadowSize: f32,
	shadowStrength: f32,
};

@group(#{MATERIAL_BIND_GROUP}) @binding(40) var<uniform> cloudsParams:CloudsParams;

@group(#{MATERIAL_BIND_GROUP}) @binding(41) var cloudsShadowMap:texture_2d<f32>;

// cloud shadow transmittance (1 = clear) at a world position. Manual bilinear filtering of an
// unfilterable texture: costs no sampler in the (sampler hungry) scene materials.
fn cloudsShadowTap( i: vec2i ) -> f32 { return textureLoad( cloudsShadowMap, clamp( i, vec2i( 0 ), vec2i( 255 ) ), 0 ).x; }
fn cloudsShadow( worldXZ: vec2f ) -> f32 {
	let uv = ( worldXZ - cloudsParams.shadowCenter ) / cloudsParams.shadowSize + 0.5;
	let st = uv * 256.0 - 0.5;
	let i0 = vec2i( floor( st ) );
	let fr = fract( st );
	let s = mix( mix( cloudsShadowTap( i0 ), cloudsShadowTap( i0 + vec2i( 1, 0 ) ), fr.x ), mix( cloudsShadowTap( i0 + vec2i( 0, 1 ) ), cloudsShadowTap( i0 + vec2i( 1, 1 ) ), fr.x ), fr.y );
	let e = abs( uv - 0.5 );
	let inside = smoothstep( 0.5, 0.42, max( e.x, e.y ) );
	return mix( 1.0, s, cloudsParams.shadowStrength * inside );
}
