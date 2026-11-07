// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 DRG Software Solutions LLC
// Source: Tidewater revision 1438b1abfcaee3267092b75573014f4d9b4a983c; third_party/tidewater/LICENSE.
// Original SHA-256: bdb725dc9229c4eed8cb24176b27213239dd8a69ea0e77ec80864fcbfef81434
diagnostic( off, derivative_uniformity );
struct Frame {
	view: mat4x4f,
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

struct SprayParams {
	frameSeed: u32,
	cpuStart: u32,
	cpuCount: u32,
	reqCount: u32,
	bodyMat: mat4x4f,
	bodyInv: mat4x4f,
	bodyVel: vec3f,
	bodyOn: f32,
	bodyHull: vec4f,
	bodyHullWL: vec4f,
	bodySheer: vec4f,
	bodyBoxMin: vec3f,
	bodyBoxMax: vec3f,
};

struct OceanParams {
	sizes: array<vec4f, 4>,
	cuts: array<vec4f, 4>,
	sysA: array<vec4f, 2>,
	sysB: array<vec4f, 2>,
	choppiness: f32,
	foamBias: f32,
	foamGain: f32,
	foamDecay: f32,
	foamAdd: f32,
	time: f32,
	depth: f32,
	seed: u32,
};

struct TerrainParams {
	origin: f32,
	size: f32,
	res: f32,
	shoreRes: f32,
	sunBake: vec3f,
	sunBaked: f32,
};

struct ShoreParams {
	period: f32,
	amplitude: f32,
	variation: f32,
	gamma: f32,
	breakSpan: f32,
	curl: f32,
	runup: f32,
	enabled: f32,
	turbidity: f32,
	dirMin: vec2f,
	dirSize: f32,
	dirOn: f32,
};

struct WaterSurfaceParams {
	amplitude: f32,
	slopeScale: f32,
	foamCoverage: f32,
	foamSharpness: f32,
	foamScale: f32,
};

struct ShoreSimParams {
	min: vec2f,
	size: f32,
	dryTime: f32,
	foamLife: f32,
	surfFoamLife: f32,
	residueLife: f32,
	foamGen: f32,
	depositGain: f32,
};

@group(0) @binding(0) var<uniform> frame: Frame;
@group(0) @binding(1) var smpLinearRepeat: sampler;
@group(0) @binding(2) var smpLinearClamp: sampler;
@group(0) @binding(3) var smpLinearMirror: sampler;
@group(0) @binding(4) var smpAnisoRepeat: sampler;
@group(0) @binding(5) var smpAnisoClamp: sampler;
@group(0) @binding(6) var smpAniso4Repeat: sampler;
@group(0) @binding(7) var smpNearestClamp: sampler;
@group(0) @binding(8) var smpNearestRepeat: sampler;
@group(0) @binding(9) var smpShadow: sampler_comparison;
@group(1) @binding(0) var<storage, read_write> sprayPos: array<vec4f>;
@group(1) @binding(1) var<storage, read_write> sprayVel: array<vec4f>;
@group(1) @binding(2) var<storage, read_write> sprayInfo: array<vec4f>;
@group(1) @binding(3) var<storage, read_write> sprayHead: array<atomic<u32>>;
@group(1) @binding(4) var<uniform> sprayParams: SprayParams;
@group(1) @binding(5) var<storage, read> waterQueryResults: array<vec4f>;
@group(1) @binding(6) var oceanDisplacement: texture_2d_array<f32>;
@group(1) @binding(7) var oceanDerivatives: texture_2d_array<f32>;
@group(1) @binding(8) var<uniform> ocean: OceanParams;
@group(1) @binding(9) var terrainHeightTex: texture_2d<f32>;
@group(1) @binding(10) var terrainNormalTex: texture_2d<f32>;
@group(1) @binding(11) var terrainSplatTex: texture_2d<f32>;
@group(1) @binding(12) var terrainDetailTex: texture_2d<f32>;
@group(1) @binding(13) var terrainShoreTex: texture_2d<f32>;
@group(1) @binding(14) var terrainSunShadowTex: texture_2d<f32>;
@group(1) @binding(15) var<uniform> terrainParams: TerrainParams;
@group(1) @binding(16) var shoreDirTex: texture_2d<f32>;
@group(1) @binding(17) var<uniform> shoreP: ShoreParams;
@group(1) @binding(18) var<uniform> waterSurface: WaterSurfaceParams;
@group(1) @binding(19) var shoreSimStateTex: texture_2d<f32>;
@group(1) @binding(20) var shoreSimLaceNearest: texture_2d<f32>;
@group(1) @binding(21) var<uniform> shoreSimP: ShoreSimParams;
@group(1) @binding(22) var<storage, read_write> shoreSimDepositA: array<atomic<u32>>;
@group(1) @binding(23) var<storage, read> sprayReq: array<vec4f>;
// ---- common

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

// ---- sprayCommon

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

// ---- spray

const SPRAY_NG: u32 = 32768u;
const SPRAY_NC: u32 = 8192u;
const SPRAY_N: u32 = 40960u;

// Reserve n ring slots of the GPU part; returns the base (pass it to spraySlot)
fn sprayReserve( n: u32 ) -> u32 { return atomicAdd( &sprayHead[ 0 ], n ); }
fn spraySlot( base: u32, i: u32 ) -> u32 { return ( base + i ) & ( SPRAY_NG - 1u ); }

fn sprayWrite( slot: u32, p: vec3f, v: vec3f, size: f32, kind: f32, life: f32, seed: f32 ) {
	sprayPos[ slot ] = vec4f( p, 0.0 );
	sprayVel[ slot ] = vec4f( v, size );
	sprayInfo[ slot ] = vec4f( kind, life, p.y, seed );
}

fn sprayRand( a: u32, b: u32 ) -> f32 {
	return sprayHash( a + b * 1664525u + sprayParams.frameSeed * 2654435761u );
}

// ---- waterQuery

fn waterQueryResult( slot: u32 ) -> vec4f { return waterQueryResults[ slot ]; }
fn waterQueryHeightAt( slot: u32 ) -> f32 { return waterQueryResults[ slot ].x; }
fn waterQueryCameraState() -> vec4f { return waterQueryResults[ 0 ]; }

// ---- ocean

const OCEAN_CASCADES: i32 = 4;
const OCEAN_FFT_SIZE: f32 = 256.0;

// Sample all cascades' displacement at world xz (explicit mip level).
fn oceanSampleDisplacement( xz: vec2f, level: f32 ) -> vec3f {
	var sum = vec3f( 0.0 );
	for ( var c = 0; c < OCEAN_CASCADES; c++ ) {
		sum += textureSampleLevel( oceanDisplacement, smpLinearRepeat, xz / ocean.sizes[ c ].x, c, level ).xyz;
	}
	return sum;
}

// ... with a weight per cascade
fn oceanSampleDisplacementWeighted( xz: vec2f, level: f32, w: vec4f ) -> vec3f {
	var sum = vec3f( 0.0 );
	for ( var c = 0; c < OCEAN_CASCADES; c++ ) {
		sum += textureSampleLevel( oceanDisplacement, smpLinearRepeat, xz / ocean.sizes[ c ].x, c, level ).xyz * w[ c ];
	}
	return sum;
}

// ---- waterSurfaceAttenuation

fn waterSurfaceCascadeAttenuation( c: i32, depth: f32 ) -> f32 {
	let d0 = array<f32, 4>( 40.00000, 12.56000, 2.66400, 0.56800 );
	let floorAmt = array<f32, 4>( 0.00000, 0.05000, 0.25000, 0.50000 );
	let a = smoothstep( 0.0, d0[ c ], depth );
	return mix( floorAmt[ c ] * smoothstep( 0.0, 0.6, depth ), 1.0, a );
}

// ---- terrain

fn terrainUvOf( xz: vec2f ) -> vec2f {
	return ( xz - terrainParams.origin ) / terrainParams.size;
}

// exact bilinear height at world xz (matches TerrainData.heightAt)
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

// filtered normal (xz components), rock mask, ambient occlusion
fn terrainNormalRock( xz: vec2f ) -> vec4f {
	let s = textureSample( terrainNormalTex, smpLinearClamp, terrainUvOf( xz ) );
	return vec4f( s.xy * 2.0 - 1.0, s.z, s.w );
}
// explicit mip (e.g. in the vertex stage)
fn terrainNormalRockLevel( xz: vec2f, level: f32 ) -> vec4f {
	let s = textureSampleLevel( terrainNormalTex, smpLinearClamp, terrainUvOf( xz ), level );
	return vec4f( s.xy * 2.0 - 1.0, s.z, s.w );
}
fn terrainNormalAt( xz: vec2f ) -> vec3f {
	let nr = terrainNormalRockLevel( xz, 0.0 );
	return normalize( vec3f( nr.x, sqrt( max( 1.0 - nr.x * nr.x - nr.y * nr.y, 0.0025 ) ), nr.y ) );
}

// loose sand, worn ground / paths, gullies (land) or seagrass (seabed), seabed rubble (the
// eroded beach scarp face on land)
fn terrainSplat( xz: vec2f ) -> vec4f {
	return textureSample( terrainSplatTex, smpLinearClamp, terrainUvOf( xz ) );
}
fn terrainSplatLevel( xz: vec2f, level: f32 ) -> vec4f {
	return textureSampleLevel( terrainSplatTex, smpLinearClamp, terrainUvOf( xz ), level );
}

// shore field: (T, dirX, dirZ, exposure), bilinear via loads (float32 data)
fn terrainShoreSample( xz: vec2f ) -> vec4f {
	let res = terrainParams.shoreRes;
	let f = ( xz - terrainParams.origin ) / terrainParams.size * res - 0.5;
	let fc = clamp( f, vec2f( 0.0 ), vec2f( max( res - 1.001, 0.0 ) ) );
	let i = floor( fc );
	let t = fract( fc );
	let ii = vec2i( i );
	let mx = vec2i( max( i32( res ) - 1, 0 ) );
	let a = textureLoad( terrainShoreTex, ii, 0 );
	let b = textureLoad( terrainShoreTex, min( ii + vec2i( 1, 0 ), mx ), 0 );
	let c = textureLoad( terrainShoreTex, min( ii + vec2i( 0, 1 ), mx ), 0 );
	let d = textureLoad( terrainShoreTex, min( ii + vec2i( 1, 1 ), mx ), 0 );
	return mix( mix( a, b, t.x ), mix( c, d, t.x ), t.y );
}

// 0 (in the terrain's shadow) .. 1 (lit) for a world position, soft penumbra that widens with the
// occluder distance
fn terrainSunShadowAt( P: vec3f ) -> f32 {
	let N = f32( textureDimensions( terrainSunShadowTex ).x );
	let st = clamp( terrainUvOf( P.xz ) * N - 0.5, vec2f( 0.0 ), vec2f( N - 1.001 ) );
	let i = vec2i( floor( st ) );
	let t = fract( st );
	let s = mix(
		mix( textureLoad( terrainSunShadowTex, i, 0 ), textureLoad( terrainSunShadowTex, i + vec2i( 1, 0 ), 0 ), t.x ),
		mix( textureLoad( terrainSunShadowTex, i + vec2i( 0, 1 ), 0 ), textureLoad( terrainSunShadowTex, i + vec2i( 1, 1 ), 0 ), t.x ),
		t.y
	);
	let w = s.y * 0.012 + 0.35;
	return mix( 1.0, smoothstep( -w, w, P.y - s.x ), terrainParams.sunBaked );
}

// ---- shore

const SHORE_GRAVITY: f32 = 9.81;
const SHORE_TAU: f32 = 6.283185307179586;
const SHORE_BEACH_SLOPE: f32 = 0.066;
const SHORE_SWASH_UP: f32 = 0.4;
const SHORE_SWASH_DOWN: f32 = 0.55;
const SHORE_SWASH_OVERSHOOT: f32 = 1.2;

struct ShoreSample {
	disp: vec3f,
	nShore: vec3f,
	env: f32,
	foam: f32,
	breaking: f32,
	u: f32,
	dir: vec2f,
	exposure: f32,
	swashLevel: f32,
	swashCovered: f32,
	thick: f32,
	swashFoam: f32,
	runup: f32,
	inland: f32,
	dRdt: f32,
	tau: f32,
	flow: vec2f,
	flowSpeed: f32,
	face: f32,
	roller: f32,
};

struct ShoreMedium { scatter: vec3f, absorb: vec3f };

struct ShorePhase { sh: vec4f, T: f32, dir: vec2f, exposure: f32, along: f32, s: f32 };

struct ShoreBreak {
	db: f32, b: f32, Ash: f32, p: f32, meanP: f32, crestPeak: f32, yc: f32, yt: f32,
	H: f32, wBore: f32, Xi: f32, Hb: f32, ycB: f32, ytB: f32, Xc: f32, Wt: f32,
};

struct ShoreProfile { x: f32, y: f32, b: f32, foam: f32, face: f32, roller: f32 };

struct ShorePair { s0: vec4f, s1: vec4f };

struct ShoreRunup { tau: f32, Rt: f32, inland: f32, RhMax: f32, su: f32, sb: f32, isUp: bool };

fn shoreHash1( x: f32 ) -> f32 { return fract( sin( x * 127.1 + 311.7 ) * 43758.5453 ); }

// Bathymetry along the beach that the travel-time field doesn't resolve: a bar with rip channels
// cut through it every ~100 m (irregular spacing and width). Over the bar the waves are bigger and
// break first and farther out (the peaks); in the channels they are much smaller and roll through
// unbroken almost to the shorebreak, so a set never closes out along the whole beach at once.
// Returns 1 over the bar, less in a channel; rip (0..1) is the channel mask.
struct ShoreBar { k: f32, rip: f32 };
fn shoreBar( along: f32 ) -> ShoreBar {
	let w = sin( along * 0.021 + 1.9 ) * 1.4 + sin( along * 0.009 + 0.3 ) * 0.9;
	let r = sin( along * 0.059 + w );
	let width = 0.8 + sin( along * 0.017 + 4.1 ) * 0.08; // channels of different width
	let rip = smoothstep( width, 0.985, r );
	// the bar itself is uneven: broad peaks and lower shoulders
	let bar = 0.9 + ( sin( along * 0.031 + 7.3 ) * 0.6 + sin( along * 0.083 + 1.1 ) * 0.4 ) * 0.14;
	return ShoreBar( bar * ( 1.0 - rip * 0.58 ), rip );
}

// along-shore phase wobble (in periods): crests bend over the uneven bottom (and run ahead in the
// deeper rip channels, where the waves travel faster)
fn shoreWobble( along: f32 ) -> f32 {
	return sin( along * 0.029 + 0.7 ) * 0.07 + sin( along * 0.083 + 2.1 ) * 0.035
		+ sin( along * 0.19 + 0.4 ) * 0.022 + sin( along * 0.37 + 2.6 ) * 0.011
		+ shoreBar( along ).rip * 0.045;
}

// ------------------------------------------------------------ per-wave height

fn shoreWaveAmp( m: f32, along: f32 ) -> f32 {
	// sets: groups of ~7 waves with larger ones in the middle, plus per-wave randomness
	let waveSet = abs( sin( m * 0.4487989505128276 ) ) * 0.6 + 0.55;
	let rnd = ( shoreHash1( m ) - 0.5 ) * 2.0;
	// along-shore variation so waves peel instead of closing out
	// peaks ~40-50 m wide with lower shoulders between them, different for every wave (the warp keeps
	// them from repeating along the beach); each peak breaks first and peels outward from it
	let warp = sin( along * 0.016 + m * 0.9 ) * 1.6;
	let a1 = sin( along * 0.062 + m * 1.7 + warp );
	let a2 = sin( along * 0.13 + m * 4.1 + 1.3 - warp * 0.7 );
	// (plus a shorter ~20 m variation: bores that rise and sag along the crest, more peel sections)
	let a3 = sin( along * 0.29 + m * 2.3 + warp * 0.5 ) * 0.6 + sin( along * 0.47 + m * 5.9 + 0.8 ) * 0.4;
	let alongV = a1 * 0.6 + a2 * 0.4 + a3 * 0.28;
	return max( shoreP.amplitude * waveSet * ( 1.0 + rnd * shoreP.variation * 0.5 + alongV * shoreP.variation * 0.7 ) * shoreBar( along ).k, 0.02 );
}

// ------------------------------------------------------------ cross-section shape

fn shoreBreakParams( A: f32, d: f32 ) -> ShoreBreak {
	var P: ShoreBreak;
	// break depth for this wave (Green's law shoaling, H = gamma d)
	P.db = pow( A * 3.556 / shoreP.gamma, 0.8 );
	P.b = ( P.db - d ) / ( P.db * shoreP.breakSpan ); // <0 shoaling, 0..1 plunging, >1 bore
	let shoal = pow( 10.0 / clamp( d, 0.35, 10.0 ), 0.25 );
	P.Ash = A * shoal;
	P.p = mix( 1.0, 3.0, smoothstep( -2.5, 0.0, P.b ) );
	P.meanP = 1.0 / sqrt( ( P.p + 0.25 ) * PI );
	P.crestPeak = 1.0 + smoothstep( -1.0, 0.3, P.b ) * 0.22;
	P.yc = P.Ash * 2.0 * ( 1.0 - P.meanP ) * P.crestPeak; // crest height
	P.yt = P.Ash * -2.0 * P.meanP * P.crestPeak; // trough level
	P.H = P.yc - P.yt;
	P.wBore = smoothstep( 0.85, 1.35, P.b );
	P.Xi = P.H * 0.8; // lip throw at impact
	// bore height, limited by the depth and fading out in the last few decimetres
	P.Hb = min( P.H * 0.6, max( d, 0.0 ) * 0.75 ) * ( smoothstep( 0.0, 0.3, d ) * 0.7 + 0.3 );
	P.ycB = mix( P.yc, P.yt * 0.6 + P.Hb, P.wBore ); // crest / roller top
	P.ytB = mix( P.yt, P.yt * 0.6, P.wBore ); // trough
	// the collapsing crest moves to where the lip landed (no shift once the bore has run out of height)
	P.Xc = mix( 0.0, P.Xi - P.Hb * 0.55, P.wBore ) * smoothstep( 0.03, 0.3, P.Hb );
	// horizontal extent of the face: concave tube face while plunging, short convex roller front on the bore
	P.Wt = mix( P.H * mix( 0.25, 0.55, smoothstep( 0.1, 0.9, P.b ) ), P.Hb * 0.6 + 0.08, P.wBore );
	return P;
}

// Whitewater made by the breaking wave at xi (m): the horizontal position relative to the crest's
// rest position, positive shoreward (a parcel's own position, or a fixed world point: the shore
// simulation deposits it where the water actually is, not where the parcel rests).
//  * nothing at all while the lip is in the air: the face and the lip are clear, glassy water
//  * the lip lands in the trough at the plunge point (b ~ 0.9, xi = Xi): whitewater appears there
//    and spreads out from it while the tube collapses behind it
//  * the collapsed tube becomes the roller: whitewater over the front and top of the bore,
//    shedding foam behind it (carried on by ShoreSim)
// whitewater over the face after the plunge: s = 0 at the crest .. 1 at the foot of the face
fn shoreFaceFill( b: f32, s: f32 ) -> f32 {
	// the foot turns white first (where the lip lands), the top of the face last: along a peeling
	// crest the edge of the broken section slants down and forward instead of standing vertical
	let k = ( 1.0 - s ) * 0.45;
	return smoothstep( k + 0.9, k + 1.08, b );
}

fn shoreWhitewater( xi: f32, lam: f32, P: ShoreBreak ) -> f32 {
	let landed = smoothstep( 0.86, 0.99, P.b );
	let spread = sat( ( P.b - 0.9 ) / 0.45 );
	let reach = P.Xi * 0.25 + spread * ( P.Xi * 0.9 + 1.0 ); // radius around the plunge point
	let impact = landed * smoothstep( reach, reach * 0.6, abs( xi - P.Xi ) ) * ( 1.0 - smoothstep( 1.4, 1.9, P.b ) );
	let toe = P.Xc + P.Wt;
	// the collapsing tube turns white from its foot (next to the plunge point) up to the crest
	let faceFill = shoreFaceFill( P.b, sat( ( xi - P.Xc ) / max( P.Wt, 0.05 ) ) );
	let ahead = exp( ( xi - toe ) * -3.0 ) * P.wBore;
	let behind = exp( ( P.Xc - xi ) / lam * -16.0 ) * P.wBore;
	let roller = select( select( max( faceFill, P.wBore ), behind, xi < P.Xc ), ahead, xi > toe );
	return sat( max( impact, roller ) );
}

// the cross-section for break parameters P (see shoreBreakParams)
// u: local phase in [-0.5, 0.5], crest at 0, u < 0 in front (shoreward) of the crest
// Returns x: shoreward displacement, y: height above mean, b: breaking progress (+ foam, face, roller withFoam)
fn shoreProfile( u: f32, lam: f32, P: ShoreBreak, withFoam: bool ) -> ShoreProfile {
	// --- shoaling: peaked (cnoidal-like) crest, the front compressed by a phase skew
	let skew = smoothstep( -3.0, 0.0, P.b ) * 0.55;
	let phi = u - skew * ( 1.0 - cos( u * SHORE_TAU ) ) / SHORE_TAU;
	let c = max( ( cos( phi * SHORE_TAU ) + 1.0 ) * 0.5, 0.0 ); // pow() of a rounding-negative base is NaN
	let yPre = P.Ash * 2.0 * ( pow( c, P.p ) - P.meanP ) * P.crestPeak;
	let Q = smoothstep( -3.0, 0.0, P.b ) * 0.25 + 0.1;
	let xPre = sin( u * SHORE_TAU ) * P.Ash * Q;

	// --- plunging / bore profile. Front: the upper uf of the phase is an elliptic arc from the
	// crest down to the trough (concave tube face -> convex roller front), the rest of the
	// front is trough, stretched to meet the next wave. Back: the shoaling back, decaying
	// exponentially behind the bore.
	let uf = 0.09;
	let inFace = u > - uf;
	let th = clamp( - u / uf, 0.0, 1.0 ) * 1.5707963267948966;
	let fx = mix( 1.0 - cos( th ), sin( th ), P.wBore );
	let fy = mix( 1.0 - sin( th ), cos( th ), P.wBore );
	let sTr = clamp( ( - u - uf ) / ( 0.5 - uf ), 0.0, 1.0 );
	let ul = u * lam;
	let xFront = select( mix( P.Xc + P.Wt, lam * 0.5, sTr ), P.Xc + P.Wt * fx, inFace ) + ul;
	let yFront = select( P.ytB, P.ytB + ( P.ycB - P.ytB ) * fy, inFace );
	let cb = pow( max( ( cos( u * ( SHORE_TAU * 0.85 ) ) + 1.0 ) * 0.5, 0.0 ), P.p );
	let yBack = P.ytB + ( P.ycB - P.ytB ) * mix( cb, exp( u * -7.0 ), P.wBore );
	let xBack = P.Xc * exp( u * -6.0 ) * smoothstep( 0.5, 0.35, u ) + xPre * ( 1.0 - P.wBore );
	let front = u < 0.0;
	let wC = smoothstep( -0.35, 0.25, P.b ) * shoreP.curl;
	var r: ShoreProfile;
	r.x = mix( xPre, select( xBack, xFront, front ), wC );
	r.y = mix( yPre, select( yBack, yFront, front ), wC );
	r.b = P.b;
	if ( withFoam ) {
		// --- whitewater (a function of where the parcel is now, see shoreWhitewater) and, per parcel, the
		// clear face of the plunging wave and the relief of the roller
		r.foam = shoreWhitewater( r.x - u * lam, lam, P );
		let onFace = front && inFace;
		let faceFill = shoreFaceFill( P.b, fx );
		// the clear, concave face of a plunging wave (WaterSurface keeps the foam carried by the water off it)
		r.face = select( 0.0, 1.0, onFace ) * smoothstep( -0.4, 0.0, P.b ) * ( 1.0 - faceFill );
		// turbulent relief of the whitewater roller (m): its front and the top it tumbles over
		r.roller = select( exp( u * -25.0 ), select( 0.0, 1.0, inFace ), front ) * P.wBore * P.Hb * wC;
	}
	return r;
}

// ( x, y, foam, b )
fn shoreShape( u: f32, A: f32, d: f32, lam: f32 ) -> vec4f {
	let s = shoreProfile( u, lam, shoreBreakParams( A, d ), true );
	return vec4f( s.x, s.y, s.foam, s.b );
}

// the cross-section at u and at u + du (for the surface normal) in one call:
// ( x0, y0, foam, b ), ( x1, y1, face, roller )
fn shoreShapePair( u: f32, du: f32, A: f32, d: f32, lam: f32 ) -> ShorePair {
	let P = shoreBreakParams( A, d );
	let s0 = shoreProfile( u, lam, P, true );
	let s1 = shoreProfile( u + du, lam, P, false );
	return ShorePair( vec4f( s0.x, s0.y, s0.foam, s0.b ), vec4f( s1.x, s1.y, s0.face, s0.roller ) );
}

// the surface at a fixed WORLD point (for the Eulerian shore simulation): whitewater there and
// the water height there (the parcel shown at this point rests ~x up-wave: first-order inverse
// of the horizontal displacement, which is large on a breaking wave)
// ( foam, height, displacement of the parcel resting here, b )
fn shoreWorld( u: f32, A: f32, d: f32, lam: f32, env: f32 ) -> vec4f {
	let P = shoreBreakParams( A, d );
	let s0 = shoreProfile( u, lam, P, false );
	let s1 = shoreProfile( u + s0.x * env / lam, lam, P, false );
	return vec4f( shoreWhitewater( - u * lam, lam, P ), s1.y, s0.x, P.b );
}

// the bore that follows the plunge: roller height Hb, horizontal extent of its front Wt, forward
// shift of the crest Xc (to where the lip landed), bore weight (0 while plunging .. 1)
fn shoreBore( A: f32, d: f32 ) -> vec4f {
	let P = shoreBreakParams( A, d );
	return vec4f( P.Hb, P.Wt, P.Xc, P.wBore );
}

// breaking progress b, wave height H, trough level (relative to mean) and how far the lip is thrown
fn shoreCrest( A: f32, d: f32 ) -> vec4f {
	let P = shoreBreakParams( A, d );
	return vec4f( P.b, P.yc - P.yt, P.ytB, P.Xi );
}

// Depth that sets the breaking state of the wave a parcel belongs to: the depth under that wave's
// crest, not under the parcel. Breaking is a property of the wave: a parcel ahead of the crest is
// in shallower water and would otherwise "break" first (whitewater creeping up the foot of a still
// glassy face). Near the troughs it hands over to the local depth, where the neighbouring wave
// takes over (the profile stays continuous at u = +-0.5). u: local phase, lam: local wavelength.
fn shoreBreakDepth( xz: vec2f, dir: vec2f, u: f32, lam: f32, d: f32 ) -> f32 {
	let pc = xz + dir * ( u * lam );
	let dc = frame.seaLevel - terrainHeightAt( pc );
	return mix( dc, d, smoothstep( 0.3, 0.5, abs( u ) ) );
}

// ------------------------------------------------------------ cheap wave direction lookup

// vec3( dir.x, dir.z, exposure ) from the direction texture (bilinear from 4 loads)
fn shoreDirAt( xz: vec2f ) -> vec3f {
	let res = f32( textureDimensions( shoreDirTex ).x );
	let fp = ( xz - shoreP.dirMin ) / shoreP.dirSize * res - 0.5;
	let fc = clamp( fp, vec2f( 0.0 ), vec2f( res - 1.001 ) );
	let i = vec2i( floor( fc ) );
	let t = fract( fc );
	let a = textureLoad( shoreDirTex, i, 0 );
	let b = textureLoad( shoreDirTex, i + vec2i( 1, 0 ), 0 );
	let c = textureLoad( shoreDirTex, i + vec2i( 0, 1 ), 0 );
	let d = textureLoad( shoreDirTex, i + vec2i( 1, 1 ), 0 );
	return mix( mix( a, b, t.x ), mix( c, d, t.x ), t.y ).xyz;
}

// ------------------------------------------------------------ surf zone water

// Optical properties of the water stirred up by breaking waves: suspended sand and fine bubbles
// scatter light (milky, luminous), fine sediment and dissolved matter absorb blue. Returns the
// extra { scatter, absorb } coefficients (1/m) that make the surf zone turquoise, not ocean-clear.
fn shoreSurfMedium( xz: vec2f, depth: f32 ) -> ShoreMedium {
	var k = 0.0;
	if ( depth < 4.5 && depth > -0.2 ) {
		var expo: f32;
		if ( shoreP.dirOn > 0.5 ) { expo = shoreDirAt( xz ).z; } else { expo = sat( length( terrainShoreSample( xz ).yz ) * 1.4 ); }
		k = smoothstep( 4.5, 1.2, depth ) * smoothstep( -0.2, 0.15, depth ) * expo * shoreP.turbidity * shoreP.enabled;
	}
	return ShoreMedium( vec3f( 0.9, 1.0, 0.85 ) * k, vec3f( 0.1, 0.2, 0.62 ) * k );
}

// ------------------------------------------------------------ evaluation at a point

// Local wave phase data at a (Lagrangian) point: shared by evaluate() and the crest finder.
fn shorePhaseAt( xz: vec2f ) -> ShorePhase {
	let sh = terrainShoreSample( xz );
	let T = sh.x;
	let dirE = vec2f( sh.y, sh.z );
	let exposure = length( dirE );
	let dir = dirE / max( exposure, 1e-4 );
	let along = dot( xz, vec2f( - dir.y, dir.x ) );
	let s = ( frame.time - T ) / shoreP.period + shoreWobble( along );
	return ShorePhase( sh, T, dir, exposure, along, s );
}

// ------------------------------------------------------------ light through thin crests

// Water path (m) along the refracted view ray Tv (unit, inside the water) from the surface point
// with rest position lagXZ until the ray leaves through the other side of the wave, or 1e4 if
// it doesn't within a few metres. The upper part of a steep wave is only a few metres thick
// horizontally: the view ray crosses it and exits into the sky behind, which is what makes
// breaking faces and crests glow turquoise. Marches the analytic cross-section (up to 3 steps).
fn shoreCrestPath( p: vec2f, d: f32, T: vec3f ) -> f32 {
	var out = 1e4;
	if ( d >= 6.0 || shoreP.enabled <= 0.0 ) { return out; } // (before the phase lookup)
	let ph = shorePhaseAt( p );
	let tXi = dot( T.xz, ph.dir ); // shoreward component of the ray
	let env = smoothstep( 26.0, 13.0, d ) * sat( ph.exposure * 1.4 ) * shoreP.enabled;
	// rays heading out through the back of the wave (a view from the beach side), near breakers
	if ( env > 0.05 && tXi < -0.05 && d < 6.0 ) {
		let lam = sqrt( clamp( d, 0.3, 25.0 ) * SHORE_GRAVITY ) * shoreP.period;
		let m = floor( ph.s + 0.5 );
		let u = ph.s - m;
		let A = shoreWaveAmp( m, ph.along );
		let P = shoreBreakParams( A, shoreBreakDepth( p, ph.dir, u, lam, d ) );
		let s0 = shoreProfile( u, lam, P, false );
		let y0 = s0.y * env;
		// only the upper part of steep (nearly breaking) waves is thin enough to see through
		if ( y0 > A * 0.2 && P.b > -1.5 ) {
			let xi0 = - u * lam + s0.x * env;
			let slope = T.y / - tXi; // ray rise per metre of horizontal travel
			var prevGap = 0.0;
			var prevDist = 0.0;
			var off = 1.1;
			for ( var k = 0; k < 3; k++ ) {
				let uk = min( u + off / lam, 0.5 );
				let sk = shoreProfile( uk, lam, P, false );
				let dist = abs( xi0 - ( - uk * lam + sk.x * env ) );
				// ray height above the surface there (> 0: the ray has left the water)
				let gap = y0 + slope * dist - sk.y * env;
				if ( gap > 0.0 ) {
					let fr = - prevGap / max( gap - prevGap, 1e-4 );
					out = mix( prevDist, dist, sat( fr ) ) / - tXi;
					break;
				}
				prevGap = gap;
				prevDist = dist;
				off *= 2.6;
			}
		}
	}
	return out;
}

// ------------------------------------------------------------ swash

// Run-up of the most recent wave at a point on the beach (distances in metres up the beach face).
// The run-up is compared with the height of the sand (converted with the nominal beach slope), so
// the front is exact at the waterline and follows the contours of the sand. sh: terrainShoreSample( xz ).
fn shoreSwashRunup( sh: vec4f, along: f32, groundH: f32 ) -> ShoreRunup {
	let Tp = shoreP.period;
	let exposure = length( vec2f( sh.y, sh.z ) );
	let Ts = sh.w;
	let inland = max( groundH - frame.seaLevel, 0.0 ) / SHORE_BEACH_SLOPE;
	let ss = ( frame.time - Ts ) / Tp + shoreWobble( along );
	let ms = floor( ss );
	let tau = ss - ms; // 0..1 time since that wave's bore reached the shoreline
	let Am = shoreWaveAmp( ms, along );
	// vertical run-up ~ H on this gentle beach, converted to a horizontal excursion
	let RhMax = Am * 2.1 * shoreP.runup * sat( exposure * 1.4 ) / SHORE_BEACH_SLOPE;
	// decelerating uprush, then a backwash that starts slowly and accelerates as the sheet drains
	let su = sat( tau / SHORE_SWASH_UP );
	let sb = sat( ( tau - SHORE_SWASH_UP ) / SHORE_SWASH_DOWN );
	let isUp = tau < SHORE_SWASH_UP;
	let Rh = select( 1.0 - pow( sb, 1.6 ), 1.0 - pow( 1.0 - su, 1.5 ), isUp ) * RhMax - 0.3;
	// the front is lobed, not a straight line: each wave runs up a little differently along the beach
	let lobes = sin( along * 0.61 + ms * 2.3 ) * 0.5 + sin( along * 1.73 + ms * 5.1 ) * 0.3 + sin( along * 4.3 + ms * 1.7 ) * 0.2;
	// the backwash never quite exposes the lower beach face: a film of water always covers the
	// first decimetres past the shoreline, so the sea never meets the sand along mesh triangles
	let Rt = max( Rh + lobes * ( max( Rh, 0.0 ) * 0.07 + 0.35 ), 0.35 ) * shoreP.enabled;
	return ShoreRunup( tau, Rt, inland, RhMax, su, sb, isUp );
}

// Water film thickness clipped at the leading edge of the swash sheet, for the water shader's
// edge fade: min( thickness, distance to the front (m) * 0.08 ). Only evaluated where the film is
// thin, so the sheet ends on the analytic front instead of the mesh triangles at ~no cost.
fn shoreSwashEdge( p: vec2f, t: f32 ) -> vec4f {
	var out = vec4f( t, 1e3, 0.0, 0.0 );
	if ( t < 0.2 ) {
		let g = terrainHeightAt( p );
		if ( g > frame.seaLevel ) {
			let ph = shorePhaseAt( p );
			let r = shoreSwashRunup( ph.sh, ph.along, g );
			let front = r.Rt - r.inland;
			out = vec4f( min( t, front * 0.08 ), front, r.tau, r.Rt );
		}
	}
	return out;
}

fn shoreSwashClip( p: vec2f, t: f32 ) -> f32 {
	return shoreSwashEdge( p, t ).x;
}

// ------------------------------------------------------------ evaluate()
// Returns displacement relative to (xz, seaLevel), foam, breaking indicator and the
// swash surface level (absolute height) for this point. shoreEvaluate (withNormal, the water mesh) also
// gives face: 1 on the clear concave face of a plunging wave, roller: relief of the whitewater (m).
// shoreEvaluateWorld: the shore simulation's view (a fixed world point instead of a water parcel): foam and
// height are those of the water shown at xz, the displacement is not meaningful.

fn shoreEvaluate( xz: vec2f, depth: f32, groundH: f32 ) -> ShoreSample {
	let ph = shorePhaseAt( xz );
	let sh = ph.sh;
	let exposure = ph.exposure;
	let along = ph.along;
	let dir = ph.dir;
	let Tp = shoreP.period;

	let d = depth;
	let c = sqrt( clamp( d, 0.3, 25.0 ) * SHORE_GRAVITY );
	let lam = c * Tp;

	let s = ph.s;
	let m = floor( s + 0.5 );
	let u = s - m;

	// wave height with smooth hand-over between consecutive waves at the trough
	let A0 = shoreWaveAmp( m, along );
	let An = shoreWaveAmp( m + sign( u ), along );
	let A = mix( A0, An, smoothstep( 0.32, 0.5, abs( u ) ) * 0.5 );
	// the breaking state of the whole wave comes from the depth under its crest
	let dB = shoreBreakDepth( xz, dir, u, lam, d );

	// offshore fade-in (FFT covers deep water) and fade on land (the swash sheet takes over there)
	let env = smoothstep( 26.0, 13.0, d ) * smoothstep( -0.25, 0.05, d ) * sat( exposure * 1.4 ) * shoreP.enabled;

	// finite difference along the propagation direction for the normal
	let e = 0.15;
	var face = 0.0;
	var roller = 0.0;
	let pair = shoreShapePair( u, - e / lam, A, dB, lam );
	let s0 = pair.s0;
	let s1 = pair.s1;
	face = s1.z * env;
	roller = s1.w * env;

	var disp = vec3f( dir.x * s0.x, s0.y, dir.y * s0.x ) * env;

	var nShore = vec3f( 0.0, 1.0, 0.0 );
	{
		let dX = e + ( s1.x - s0.x ) * env;
		let dY = ( s1.y - s0.y ) * env;
		let tAlong = vec3f( dir.x * dX, dY, dir.y * dX );
		let tAcross = vec3f( - dir.y, 0.0, dir.x );
		// epsilon: never a zero vector; never facing down (the mesh doesn't overhang, the lip sheet does)
		let n = normalize( cross( tAcross, tAlong ) + vec3f( 0.0, 1e-4, 0.0 ) );

		// the whitewater roller is not a smooth tube: lumps of foam tumble along its front and over
		// its top (relief of a few decimetres, with its slope in the normal). Noise, not a sum of sines:
		// regular bumps along the crest read as a row of identical puffs. Bigger, fewer lumps in some
		// stretches, a lower, smoother churn in others (different for every wave).
		let sx = u * lam; // rest position along the wave direction (m, seaward)
		let t = frame.time;
		let mW = floor( ph.s + 0.5 );
		let lumpy = sat( perlin2( vec2f( along * 0.045, mW * 3.7 ) ) * 1.2 + 0.55 );
		let amp = roller * mix( 0.25, 0.7, lumpy );
		let q1 = vec2f( along * 0.28, sx * 0.7 - t * 0.8 );
		let q2 = vec2f( along * 0.8 + 11.3, sx * 1.6 - t * 1.5 );
		let eL = 0.25;
		let L0 = perlin2( q1 ) * 0.7 + perlin2( q2 ) * 0.3;
		let La = perlin2( q1 + vec2f( eL * 0.28, 0.0 ) ) * 0.7 + perlin2( q2 + vec2f( eL * 0.8, 0.0 ) ) * 0.3;
		let Ls = perlin2( q1 + vec2f( 0.0, eL * 0.7 ) ) * 0.7 + perlin2( q2 + vec2f( 0.0, eL * 1.6 ) ) * 0.3;
		// lumps stand up from the roller (rounded caps, flatter troughs between them)
		let lump = max( L0 * 1.5 + 0.2, -0.3 );
		disp.y += lump * amp;
		let dAlong = select( 0.0, ( La - L0 ) / eL * 1.5, L0 * 1.5 + 0.2 > -0.3 );
		let dSx = select( 0.0, ( Ls - L0 ) / eL * 1.5, L0 * 1.5 + 0.2 > -0.3 );
		let dShore = - dSx; // d/d(shoreward) = - d/dsx
		let g = ( vec2f( - dir.y, dir.x ) * dAlong + dir * dShore ) * amp * n.y;
		nShore = normalize( vec3f( n.x - g.x, max( n.y, 0.04 ), n.z - g.y ) );
	}

	// ---- swash: run-up of the most recent wave on the sand
	let swr = shoreSwashRunup( sh, along, groundH );
	let front = swr.Rt - swr.inland; // signed distance to the leading edge (m), > 0 under the sheet
	let covered = front > 0.0;
	// leading edge velocity along the slope (m/s), positive = uphill
	let dRdt = select( pow( max( swr.sb, 1e-3 ), 0.6 ) * ( - 1.6 / SHORE_SWASH_DOWN ), pow( 1.0 - swr.su, 0.5 ) * ( 1.5 / SHORE_SWASH_UP ), swr.isUp ) * swr.RhMax / Tp;
	// thin sheet (a few cm, thickening behind the leading edge). The mesh sheet overshoots the
	// leading edge a little; the water shader cuts it exactly on the front (swashClip), so the edge
	// doesn't follow the mesh triangles.
	let fm = front + SHORE_SWASH_OVERSHOOT;
	let thick = clamp( min( fm * 0.3, max( fm - 0.1, 0.0 ) * ( SHORE_BEACH_SLOPE * 0.22 ) + 0.03 ), -0.1, 0.12 );
	let swashLevel = groundH + thick;
	// bubbly foam line riding the leading edge all the way up (left behind as the swash mark)
	let uprush = smoothstep( 0.46, 0.32, swr.tau );
	let edge = smoothstep( 0.8, 0.0, front ) * smoothstep( -0.05, 0.05, front ) * uprush * smoothstep( 0.0, 1.0, swr.Rt );
	let swashFoam = edge * 0.9;

	// ---- depth-averaged water velocity (along dir), for foam advection
	// waves / bores: shallow-water particle velocity c * eta / h; swash sheet: the tip moves at
	// dR/dt, slower toward the shoreline during uprush, faster there while it drains
	let eta = disp.y;
	let uWave = clamp( c * eta / ( max( d, 0.0 ) + max( eta, d * -0.8 ) + 0.15 ), -2.5, 4.0 );
	let rel = sat( swr.inland / max( swr.Rt, 0.5 ) );
	let uSwash = dRdt * select( ( 1.0 - rel ) * 0.6 + 0.9, clamp( ( swr.inland + 3.0 ) / ( swr.Rt + 3.0 ), 0.15, 1.0 ), swr.isUp );
	let wSwash = smoothstep( 0.3, 0.05, d );
	let uFlow = mix( uWave, uSwash, wSwash ) * select( 0.0, 1.0, covered || d > 0.02 );

	var o: ShoreSample;
	// the churn of a bore is uneven along the crest: dense in some stretches, torn into patches and
	// lace in others (different for every wave, drifting slowly along it)
	let wwPatch = smoothstep( -0.5, 0.45, perlin2( vec2f( along * 0.06 + frame.time * 0.05, m * 2.9 + 0.4 ) ) );
	o.disp = disp; o.nShore = nShore; o.env = env; o.foam = s0.z * env * mix( 0.3, 1.0, wwPatch ); o.breaking = s0.w; o.u = u; o.dir = dir;
	o.exposure = exposure; o.swashLevel = swashLevel; o.swashCovered = select( 0.0, 1.0, covered ); o.thick = thick;
	o.swashFoam = swashFoam; o.runup = swr.Rt; o.inland = swr.inland; o.dRdt = dRdt; o.tau = swr.tau;
	o.flow = dir * uFlow; o.flowSpeed = uFlow; o.face = face; o.roller = roller;
	return o;
}


fn shoreEvaluateNoNormal( xz: vec2f, depth: f32, groundH: f32 ) -> ShoreSample {
	let ph = shorePhaseAt( xz );
	let sh = ph.sh;
	let exposure = ph.exposure;
	let along = ph.along;
	let dir = ph.dir;
	let Tp = shoreP.period;

	let d = depth;
	let c = sqrt( clamp( d, 0.3, 25.0 ) * SHORE_GRAVITY );
	let lam = c * Tp;

	let s = ph.s;
	let m = floor( s + 0.5 );
	let u = s - m;

	// wave height with smooth hand-over between consecutive waves at the trough
	let A0 = shoreWaveAmp( m, along );
	let An = shoreWaveAmp( m + sign( u ), along );
	let A = mix( A0, An, smoothstep( 0.32, 0.5, abs( u ) ) * 0.5 );
	// the breaking state of the whole wave comes from the depth under its crest
	let dB = shoreBreakDepth( xz, dir, u, lam, d );

	// offshore fade-in (FFT covers deep water) and fade on land (the swash sheet takes over there)
	let env = smoothstep( 26.0, 13.0, d ) * smoothstep( -0.25, 0.05, d ) * sat( exposure * 1.4 ) * shoreP.enabled;

	// finite difference along the propagation direction for the normal
	let e = 0.15;
	var face = 0.0;
	var roller = 0.0;
	let s0 = shoreShape( u, A, dB, lam );

	var disp = vec3f( dir.x * s0.x, s0.y, dir.y * s0.x ) * env;

	var nShore = vec3f( 0.0, 1.0, 0.0 );


	// ---- swash: run-up of the most recent wave on the sand
	let swr = shoreSwashRunup( sh, along, groundH );
	let front = swr.Rt - swr.inland; // signed distance to the leading edge (m), > 0 under the sheet
	let covered = front > 0.0;
	// leading edge velocity along the slope (m/s), positive = uphill
	let dRdt = select( pow( max( swr.sb, 1e-3 ), 0.6 ) * ( - 1.6 / SHORE_SWASH_DOWN ), pow( 1.0 - swr.su, 0.5 ) * ( 1.5 / SHORE_SWASH_UP ), swr.isUp ) * swr.RhMax / Tp;
	// thin sheet (a few cm, thickening behind the leading edge). The mesh sheet overshoots the
	// leading edge a little; the water shader cuts it exactly on the front (swashClip), so the edge
	// doesn't follow the mesh triangles.
	let fm = front + SHORE_SWASH_OVERSHOOT;
	let thick = clamp( min( fm * 0.3, max( fm - 0.1, 0.0 ) * ( SHORE_BEACH_SLOPE * 0.22 ) + 0.03 ), -0.1, 0.12 );
	let swashLevel = groundH + thick;
	// bubbly foam line riding the leading edge all the way up (left behind as the swash mark)
	let uprush = smoothstep( 0.46, 0.32, swr.tau );
	let edge = smoothstep( 0.8, 0.0, front ) * smoothstep( -0.05, 0.05, front ) * uprush * smoothstep( 0.0, 1.0, swr.Rt );
	let swashFoam = edge * 0.9;

	// ---- depth-averaged water velocity (along dir), for foam advection
	// waves / bores: shallow-water particle velocity c * eta / h; swash sheet: the tip moves at
	// dR/dt, slower toward the shoreline during uprush, faster there while it drains
	let eta = disp.y;
	let uWave = clamp( c * eta / ( max( d, 0.0 ) + max( eta, d * -0.8 ) + 0.15 ), -2.5, 4.0 );
	let rel = sat( swr.inland / max( swr.Rt, 0.5 ) );
	let uSwash = dRdt * select( ( 1.0 - rel ) * 0.6 + 0.9, clamp( ( swr.inland + 3.0 ) / ( swr.Rt + 3.0 ), 0.15, 1.0 ), swr.isUp );
	let wSwash = smoothstep( 0.3, 0.05, d );
	let uFlow = mix( uWave, uSwash, wSwash ) * select( 0.0, 1.0, covered || d > 0.02 );

	var o: ShoreSample;
	// the churn of a bore is uneven along the crest: dense in some stretches, torn into patches and
	// lace in others (different for every wave, drifting slowly along it)
	let wwPatch = smoothstep( -0.5, 0.45, perlin2( vec2f( along * 0.06 + frame.time * 0.05, m * 2.9 + 0.4 ) ) );
	o.disp = disp; o.nShore = nShore; o.env = env; o.foam = s0.z * env * mix( 0.3, 1.0, wwPatch ); o.breaking = s0.w; o.u = u; o.dir = dir;
	o.exposure = exposure; o.swashLevel = swashLevel; o.swashCovered = select( 0.0, 1.0, covered ); o.thick = thick;
	o.swashFoam = swashFoam; o.runup = swr.Rt; o.inland = swr.inland; o.dRdt = dRdt; o.tau = swr.tau;
	o.flow = dir * uFlow; o.flowSpeed = uFlow; o.face = face; o.roller = roller;
	return o;
}


fn shoreEvaluateWorld( xz: vec2f, depth: f32, groundH: f32 ) -> ShoreSample {
	let ph = shorePhaseAt( xz );
	let sh = ph.sh;
	let exposure = ph.exposure;
	let along = ph.along;
	let dir = ph.dir;
	let Tp = shoreP.period;

	let d = depth;
	let c = sqrt( clamp( d, 0.3, 25.0 ) * SHORE_GRAVITY );
	let lam = c * Tp;

	let s = ph.s;
	let m = floor( s + 0.5 );
	let u = s - m;

	// wave height with smooth hand-over between consecutive waves at the trough
	let A0 = shoreWaveAmp( m, along );
	let An = shoreWaveAmp( m + sign( u ), along );
	let A = mix( A0, An, smoothstep( 0.32, 0.5, abs( u ) ) * 0.5 );
	// the breaking state of the whole wave comes from the depth under its crest
	let dB = shoreBreakDepth( xz, dir, u, lam, d );

	// offshore fade-in (FFT covers deep water) and fade on land (the swash sheet takes over there)
	let env = smoothstep( 26.0, 13.0, d ) * smoothstep( -0.25, 0.05, d ) * sat( exposure * 1.4 ) * shoreP.enabled;

	// finite difference along the propagation direction for the normal
	let e = 0.15;
	var face = 0.0;
	var roller = 0.0;
	let r = shoreWorld( u, A, dB, lam, env );
	let s0 = vec4f( r.z, r.y, r.x, r.w );

	var disp = vec3f( dir.x * s0.x, s0.y, dir.y * s0.x ) * env;

	var nShore = vec3f( 0.0, 1.0, 0.0 );


	// ---- swash: run-up of the most recent wave on the sand
	let swr = shoreSwashRunup( sh, along, groundH );
	let front = swr.Rt - swr.inland; // signed distance to the leading edge (m), > 0 under the sheet
	let covered = front > 0.0;
	// leading edge velocity along the slope (m/s), positive = uphill
	let dRdt = select( pow( max( swr.sb, 1e-3 ), 0.6 ) * ( - 1.6 / SHORE_SWASH_DOWN ), pow( 1.0 - swr.su, 0.5 ) * ( 1.5 / SHORE_SWASH_UP ), swr.isUp ) * swr.RhMax / Tp;
	// thin sheet (a few cm, thickening behind the leading edge). The mesh sheet overshoots the
	// leading edge a little; the water shader cuts it exactly on the front (swashClip), so the edge
	// doesn't follow the mesh triangles.
	let fm = front + SHORE_SWASH_OVERSHOOT;
	let thick = clamp( min( fm * 0.3, max( fm - 0.1, 0.0 ) * ( SHORE_BEACH_SLOPE * 0.22 ) + 0.03 ), -0.1, 0.12 );
	let swashLevel = groundH + thick;
	// bubbly foam line riding the leading edge all the way up (left behind as the swash mark)
	let uprush = smoothstep( 0.46, 0.32, swr.tau );
	let edge = smoothstep( 0.8, 0.0, front ) * smoothstep( -0.05, 0.05, front ) * uprush * smoothstep( 0.0, 1.0, swr.Rt );
	let swashFoam = edge * 0.9;

	// ---- depth-averaged water velocity (along dir), for foam advection
	// waves / bores: shallow-water particle velocity c * eta / h; swash sheet: the tip moves at
	// dR/dt, slower toward the shoreline during uprush, faster there while it drains
	let eta = disp.y;
	let uWave = clamp( c * eta / ( max( d, 0.0 ) + max( eta, d * -0.8 ) + 0.15 ), -2.5, 4.0 );
	let rel = sat( swr.inland / max( swr.Rt, 0.5 ) );
	let uSwash = dRdt * select( ( 1.0 - rel ) * 0.6 + 0.9, clamp( ( swr.inland + 3.0 ) / ( swr.Rt + 3.0 ), 0.15, 1.0 ), swr.isUp );
	let wSwash = smoothstep( 0.3, 0.05, d );
	let uFlow = mix( uWave, uSwash, wSwash ) * select( 0.0, 1.0, covered || d > 0.02 );

	var o: ShoreSample;
	// the churn of a bore is uneven along the crest: dense in some stretches, torn into patches and
	// lace in others (different for every wave, drifting slowly along it)
	let wwPatch = smoothstep( -0.5, 0.45, perlin2( vec2f( along * 0.06 + frame.time * 0.05, m * 2.9 + 0.4 ) ) );
	o.disp = disp; o.nShore = nShore; o.env = env; o.foam = s0.z * env * mix( 0.3, 1.0, wwPatch ); o.breaking = s0.w; o.u = u; o.dir = dir;
	o.exposure = exposure; o.swashLevel = swashLevel; o.swashCovered = select( 0.0, 1.0, covered ); o.thick = thick;
	o.swashFoam = swashFoam; o.runup = swr.Rt; o.inland = swr.inland; o.dRdt = dRdt; o.tau = swr.tau;
	o.flow = dir * uFlow; o.flowSpeed = uFlow; o.face = face; o.roller = roller;
	return o;
}


// ---- waterQueryHeight

fn waterQueryDispAt( x0: vec2f, depth: f32 ) -> vec3f {
	var d = vec3f( 0.0 );
	d += textureSampleLevel( oceanDisplacement, smpLinearRepeat, x0 / ocean.sizes[ 0 ].x, 0, 0.0 ).xyz * waterSurfaceCascadeAttenuation( 0, depth );
	d += textureSampleLevel( oceanDisplacement, smpLinearRepeat, x0 / ocean.sizes[ 1 ].x, 1, 0.0 ).xyz * waterSurfaceCascadeAttenuation( 1, depth );
	d += textureSampleLevel( oceanDisplacement, smpLinearRepeat, x0 / ocean.sizes[ 2 ].x, 2, 0.0 ).xyz * waterSurfaceCascadeAttenuation( 2, depth );
	d += textureSampleLevel( oceanDisplacement, smpLinearRepeat, x0 / ocean.sizes[ 3 ].x, 3, 2.0 ).xyz * waterSurfaceCascadeAttenuation( 3, depth );

	d *= waterSurface.amplitude;
	d += shoreEvaluateNoNormal( x0, depth, terrainHeightAt( x0 ) ).disp;

	return d;
}

fn waterQueryHeightAtXZ( p: vec2f ) -> f32 {
	let depth = frame.seaLevel - terrainHeightAt( p );
	var x0 = p;
	for ( var i = 0; i < 2; i++ ) {
		x0 = p - waterQueryDispAt( x0, depth ).xz;
	}
	return frame.seaLevel + waterQueryDispAt( x0, depth ).y;
}

// ---- waterQueryAll

// ---- shoreSim

const SHORE_SIM_RES: f32 = 768.0;
const SHORE_SIM_LACE_TILE: f32 = 3.5;

fn shoreSimUvOf( xz: vec2f ) -> vec2f {
	return ( xz - shoreSimP.min ) / shoreSimP.size;
}

fn shoreSimInside( uv: vec2f ) -> f32 {
	return smoothstep( 0.0, 0.02, uv.x ) * smoothstep( 1.0, 0.98, uv.x ) * smoothstep( 0.0, 0.02, uv.y ) * smoothstep( 1.0, 0.98, uv.y );
}

// bilinear state at texture coordinate uv, from 4 loads
fn shoreSimStateAt( uv: vec2f ) -> vec4f {
	let fp = clamp( uv * SHORE_SIM_RES - 0.5, vec2f( 0.0 ), vec2f( SHORE_SIM_RES - 1.001 ) );
	let i = vec2i( floor( fp ) );
	let t = fract( fp );
	let a = textureLoad( shoreSimStateTex, i, 0 );
	let b = textureLoad( shoreSimStateTex, i + vec2i( 1, 0 ), 0 );
	let c = textureLoad( shoreSimStateTex, i + vec2i( 0, 1 ), 0 );
	let d = textureLoad( shoreSimStateTex, i + vec2i( 1, 1 ), 0 );
	return mix( mix( a, b, t.x ), mix( c, d, t.x ), t.y );
}

// raw state (foam, wetness, residue amount, lace offset), faded out at the region border
fn shoreSimState( xz: vec2f ) -> vec4f {
	let uv = shoreSimUvOf( xz );
	var out = vec4f( 0.0 );
	if ( uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0 ) {
		out = shoreSimStateAt( uv ) * shoreSimInside( uv );
	}
	return out;
}

// vec4( foam amount on the water, sand wetness, foam amount left on the sand, flow speed ).
// Used by the water (x, w), the underwater lighting (x) and the terrain (y, z; for the lacy look
// of the foam left on the sand use shoreSimSandFoam()). No sampler bindings.
fn shoreSimSample( xz: vec2f ) -> vec4f {
	return shoreSimState( xz );
}

// bilinear lace lookup from 4 loads of the nearest-filtered copy (no sampler binding needed)
fn shoreSimLaceLoad( q: vec2f ) -> vec4f {
	let fp = q / SHORE_SIM_LACE_TILE * 512.0 - 0.5;
	let i = vec2i( floor( fp ) );
	let t = fract( fp );
	let m = vec2i( 511 );
	let a = textureLoad( shoreSimLaceNearest, i & m, 0 );
	let b = textureLoad( shoreSimLaceNearest, ( i + vec2i( 1, 0 ) ) & m, 0 );
	let c = textureLoad( shoreSimLaceNearest, ( i + vec2i( 0, 1 ) ) & m, 0 );
	let d = textureLoad( shoreSimLaceNearest, ( i + vec2i( 1, 1 ) ) & m, 0 );
	return mix( mix( a, b, t.x ), mix( c, d, t.x ), t.y );
}

// Foam left on the sand (0..1): thin bubble lines and single bubbles where the draining water
// left its foam, popping patch by patch as it dries. Static on the sand (world space). s:
// shoreSimSample( xz ). (h: the ground height at xz, unused.)
fn shoreSimSandFoam( xz: vec2f, s: vec4f, h: f32 ) -> f32 {
	let r = s.z;
	// (screen-space footprint first: derivatives before the branch)
	let fp = length( fwidth( xz ) ) / SHORE_SIM_LACE_TILE * 512.0;
	var out = 0.0;
	if ( r > 0.01 ) {
		let lace = shoreSimLaceLoad( xz + 11.3 );
		// fade to the average where a pixel covers several strands (no mipmaps on the load path)
		let near = smoothstep( 3.0, 1.2, fp );
		let keep = smoothstep( lace.w * 0.55, lace.w * 0.55 + 0.08, r ); // staggered popping
		// thin bubble lines where the strands were, a little wider where more foam was left
		let lw = r * 0.1 + 0.06;
		let strand = ( 1.0 - smoothstep( lw, lw + 0.07, lace.x ) ) * ( lace.z * 0.5 + 0.6 );
		let lines = max( strand * smoothstep( 0.02, 0.25, r ) * keep, lace.y * keep * 0.8 );
		out = mix( smoothstep( 0.08, 0.6, r ) * 0.12, lines, near ) * 0.85;
	}
	return out;
}

// ---- shoreSimDeposit

fn shoreSimDepositAt( xz: vec2f, n: u32 ) {
	let uv = shoreSimUvOf( xz );
	if ( uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0 ) {
		let ij = vec2u( uv * SHORE_SIM_RES );
		atomicAdd( &shoreSimDepositA[ ij.y * 768u + ij.x ], n );
	}
}


// Push a particle out of the body (hull or box) through the nearest face; the velocity into it
// (relative to the body) is removed and the rest damped (the water runs off as a film); drops
// that hit it die soon after.
fn sprayCollideBody( p: ptr<function, vec3f>, v: ptr<function, vec3f>, age: f32, life: ptr<function, f32>, isMist: bool ) {
	if ( sprayParams.bodyOn < 0.5 ) { return; }
	var q = ( sprayParams.bodyInv * vec4f( *p, 1.0 ) ).xyz;
	let H = sprayParams.bodyHull; let W = sprayParams.bodyHullWL; let S = sprayParams.bodySheer;
	// hull: the outline at this height (waterline -> sheer), half breadth at q.z (elliptic bow)
	let sheer = mix( S.x, S.y, sat( ( q.z - H.x ) / max( H.z - H.x, 0.01 ) ) );
	let fh = sat( q.y / max( sheer, 0.1 ) );
	let zSh = mix( W.x, H.y, fh ); let zSt = mix( W.y, H.z, fh ); let HB = mix( W.z, H.w, fh );
	let bowL = max( zSt - zSh, 0.01 );
	let e = sat( ( q.z - zSh ) / bowL );
	let c = sqrt( max( 1.0 - e * e, 0.02 ) );
	let hb = HB * c;
	let inHull = q.z > H.x && q.z < zSt && abs( q.x ) < hb && q.y < sheer && q.y > S.z;
	let b0 = sprayParams.bodyBoxMin; let b1 = sprayParams.bodyBoxMax;
	let inBox = q.x > b0.x && q.x < b1.x && q.y > b0.y && q.y < b1.y && q.z > b0.z && q.z < b1.z;
	if ( inHull || inBox ) {
		var n = vec3f( 0.0, 1.0, 0.0 );
		if ( inHull ) {
			let sx = select( - 1.0, 1.0, q.x > 0.0 );
			if ( hb - abs( q.x ) < sheer - q.y ) {
				// through the side; on the bow the side faces forward too (- d hb / dz)
				n = normalize( vec3f( sx, 0.0, HB * e / ( c * bowL ) ) );
				q.x = sx * ( hb + 0.02 );
			} else {
				q.y = sheer + 0.02;
			}
		} else {
			let dx = min( q.x - b0.x, b1.x - q.x );
			let dz = min( q.z - b0.z, b1.z - q.z );
			let dy = b1.y - q.y;
			if ( dy < dx && dy < dz ) {
				q.y = b1.y + 0.02;
			} else if ( dx < dz ) {
				let right = q.x > ( b0.x + b1.x ) * 0.5;
				n = vec3f( select( - 1.0, 1.0, right ), 0.0, 0.0 );
				q.x = select( b0.x - 0.02, b1.x + 0.02, right );
			} else {
				let front = q.z > ( b0.z + b1.z ) * 0.5;
				n = vec3f( 0.0, 0.0, select( - 1.0, 1.0, front ) );
				q.z = select( b0.z - 0.02, b1.z + 0.02, front );
			}
		}
		let nw = normalize( ( sprayParams.bodyMat * vec4f( n, 0.0 ) ).xyz );
		var vr = *v - sprayParams.bodyVel;
		let vn = dot( vr, nw );
		if ( vn < 0.0 ) { vr -= nw * vn; }
		*v = sprayParams.bodyVel + vr * 0.5;
		*p = ( sprayParams.bodyMat * vec4f( q, 1.0 ) ).xyz;
		// water that hits it wets it and runs off (gone); mist flows around it and settles
		*life = min( *life, select( age, age + 0.25, isMist ) );
	}
}

@compute @workgroup_size( 64, 1, 1 )
fn main( @builtin( global_invocation_id ) gid: vec3u ) {
	let i = gid.x;
	if ( i >= SPRAY_N ) { return; }

	// ---- CPU-owned slots: spawn from this frame's requests
	if ( i >= 32768u ) {
		let k = ( i - 32768u + 8192u - sprayParams.cpuStart ) % 8192u;
		if ( k < sprayParams.cpuCount ) {
			var r = 0u;
			for ( var j = 0u; j < sprayParams.reqCount; j++ ) {
				r = j;
				if ( f32( k ) < sprayReq[ j * 4u ].w ) { break; }
			}
			let r0 = sprayReq[ r * 4u ];
			let r1 = sprayReq[ r * 4u + 1u ];
			let r2 = sprayReq[ r * 4u + 2u ];
			let r3 = sprayReq[ r * 4u + 3u ];
			let h0 = sprayRand( i, 11u ); let h1 = sprayRand( i, 12u ); let h2 = sprayRand( i, 13u );
			let h3 = sprayRand( i, 14u ); let h4 = sprayRand( i, 15u ); let h5 = sprayRand( i, 16u );
			let h6 = sprayRand( i, 17u );
			let along = mix( r0.xyz, r1.xyz, h0 );
			let jit = vec3f( h1 - 0.5, h2 - 0.5, h3 - 0.5 ) * ( r3.y * 2.0 );
			let vj = vec3f( h4 - 0.5, h5 - 0.5, h6 - 0.5 ) * ( r3.x * 2.0 );
			let size = r1.w * ( 1.0 + ( h2 - 0.5 ) * r3.w );
			let life = r3.z * ( h3 * 0.5 + 0.75 );
			sprayWrite( i, along + jit, r2.xyz + vj, size, r2.w, life, h5 );
		}
	}

	// ---- integrate live particles
	let P = sprayPos[ i ];
	let Vv = sprayVel[ i ];
	let info = sprayInfo[ i ];
	if ( info.y > 0.0 ) {
		let dt = frame.dt;
		var p = P.xyz;
		var v = Vv.xyz;
		let age = P.w + dt;
		let kind = info.x;

		// wind near the surface (~70% of the 10 m wind), gusty
		let gust = sin( frame.time * 0.7 + p.x * 0.05 ) * 0.25 + 0.85;
		let wind = vec3f( frame.windDir.x, 0.0, frame.windDir.y ) * ( frame.windSpeed * 0.7 * gust );
		// drag toward the wind: small drops follow the air, mist drifts with it
		// mist first keeps moving with the air the wave pushes ahead of it, then joins the wind
		let isMist = kind > 0.5 && kind < 1.5;
		let tau = sprayByKind( kind, 3.0, 0.35, 5.0, 1.8, 2.5 ) * select( 1.0, exp( age * - 1.2 ) * 4.0 + 1.0, isMist );
		let grav = sprayByKind( kind, 9.81, 0.3, 9.81, 8.5, 9.81 );
		let turb = vec3f(
			sin( age * 2.1 + fract( info.w ) * 40.0 ),
			sin( age * 1.7 + fract( info.w ) * 17.0 ) * 0.5,
			cos( age * 1.9 + fract( info.w ) * 29.0 ) ) * sprayByKind( kind, 0.0, 0.5, 0.0, 0.3, 0.1 );
		v += ( ( wind - v ) / tau + turb ) * dt;
		v.y -= grav * dt;
		p += v * dt;

		var life = info.y;
		sprayCollideBody( &p, &v, age, &life, isMist );

		// water surface and ground below
		let hw = waterQueryHeightAtXZ( p.xz );
		let ground = terrainHeightAt( p.xz );
		let top = max( hw, ground );

		// drops die in the water / on the sand; mist and foam skim over it
		if ( p.y < top && v.y < 0.0 && age > 0.04 ) {
			if ( sprayByKind( kind, 1.0, 0.0, 1.0, 1.0, 1.0 ) > 0.5 ) {
				life = 0.0;
				// fell into the water (not onto the sand): its bubbles add to the foam there
				if ( hw > ground + 0.02 ) {
					shoreSimDepositAt( p.xz, u32( sprayByKind( kind, 1.0, 0.0, 3.0, 4.0, 2.0 ) ) );
				}
			} else {
				p.y = top + 0.02;
				v.y = max( v.y, 0.0 );
				v = vec3f( v.x * 0.95, v.y, v.z * 0.95 );
			}
		}

		if ( age > life ) { life = 0.0; }

		// mist and spray clouds grow as they dilute
		let size = Vv.w * ( 1.0 + dt * sprayByKind( kind, 0.0, 0.22, 0.0, 0.3, 0.7 ) );
		sprayPos[ i ] = vec4f( p, age );
		sprayVel[ i ] = vec4f( v, size );
		sprayInfo[ i ] = vec4f( info.x, life, hw, info.w );
	}
}
