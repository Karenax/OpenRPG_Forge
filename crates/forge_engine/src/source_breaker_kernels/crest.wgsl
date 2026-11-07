// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 DRG Software Solutions LLC
// Source: Tidewater revision 1438b1abfcaee3267092b75573014f4d9b4a983c; third_party/tidewater/LICENSE.
// Original SHA-256: 5cb3282bab2855924cc0b9e689e33a4ee2202ded8634ca0823f88730c86ef105
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

struct BreakersParams {
	cameraPos: vec3f,
	spray: f32,
	sheet: f32,
	emitRange: f32,
	amplitude: f32,
	budget: f32,
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
@group(1) @binding(0) var terrainHeightTex: texture_2d<f32>;
@group(1) @binding(1) var terrainNormalTex: texture_2d<f32>;
@group(1) @binding(2) var terrainSplatTex: texture_2d<f32>;
@group(1) @binding(3) var terrainDetailTex: texture_2d<f32>;
@group(1) @binding(4) var terrainShoreTex: texture_2d<f32>;
@group(1) @binding(5) var terrainSunShadowTex: texture_2d<f32>;
@group(1) @binding(6) var<uniform> terrainParams: TerrainParams;
@group(1) @binding(7) var shoreDirTex: texture_2d<f32>;
@group(1) @binding(8) var<uniform> shoreP: ShoreParams;
@group(1) @binding(9) var oceanDisplacement: texture_2d_array<f32>;
@group(1) @binding(10) var oceanDerivatives: texture_2d_array<f32>;
@group(1) @binding(11) var<uniform> ocean: OceanParams;
@group(1) @binding(12) var<storage, read_write> sprayPos: array<vec4f>;
@group(1) @binding(13) var<storage, read_write> sprayVel: array<vec4f>;
@group(1) @binding(14) var<storage, read_write> sprayInfo: array<vec4f>;
@group(1) @binding(15) var<storage, read_write> sprayHead: array<atomic<u32>>;
@group(1) @binding(16) var<uniform> sprayParams: SprayParams;
@group(1) @binding(17) var<uniform> breakersP: BreakersParams;
@group(1) @binding(18) var<storage, read> breakersStations: array<vec4f>;
@group(1) @binding(19) var<storage, read_write> breakersCrestW: array<vec4f>;
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


const BRK_GRAVITY: f32 = 9.81;

fn breakersFftDisp( p: vec2f, depth: f32 ) -> vec3f {
	var d = vec3f( 0.0 );
	d += textureSampleLevel( oceanDisplacement, smpLinearRepeat, p / 157.0, 1, 0.0 ).xyz * mix( 0.05 * smoothstep( 0.0, 0.6, depth ), 1.0, smoothstep( 0.0, 12.56, depth ) );
	d += textureSampleLevel( oceanDisplacement, smpLinearRepeat, p / 33.3, 2, 2.127976554986342 ).xyz * mix( 0.25 * smoothstep( 0.0, 0.6, depth ), 1.0, smoothstep( 0.0, 2.6639999999999997, depth ) );
	d += textureSampleLevel( oceanDisplacement, smpLinearRepeat, p / 7.1, 3, 4.357607802552922 ).xyz * mix( 0.5 * smoothstep( 0.0, 0.6, depth ), 1.0, smoothstep( 0.0, 0.568, depth ) );
	return d * breakersP.amplitude;
}


struct BrkCtx {
	root: vec3f,
	d3: vec3f,
	tg: vec3f,
	Yi: f32,
	Wt: f32,
	wB: f32,
	trough: f32,
	xr: f32,
	Xi: f32,
	q: f32,
};

struct BrkPN { p: vec3f, n: vec3f };

// A point on the front of the wave, on the water surface (the same curve as ShoreWaves' profile:
// concave tube face while plunging, convex roller front of the bore), and its outward normal.
// s: 0 = crest top .. 1 = foot. Everything the breaker throws starts just outside it.
fn breakersFacePoint( C: BrkCtx, sp: f32 ) -> BrkPN {
	let up = vec3f( 0.0, 1.0, 0.0 );
	let th = sp * 1.5707963267948966;
	let ct = cos( th );
	let st = sin( th );
	let fx = mix( 1.0 - ct, st, C.wB );
	let fy = mix( 1.0 - st, ct, C.wB );
	let n = normalize( C.d3 * ( C.Yi * mix( ct, st, C.wB ) ) + up * ( C.Wt * mix( st, ct, C.wB ) + 0.02 ) );
	let p = C.root + C.d3 * ( C.Wt * fx );
	return BrkPN( vec3f( p.x, C.trough + C.Yi * fy, p.z ), n );
}

// the plunge point: in the trough ahead of the face while the tube is open; once the bore front
// has formed over it, on the lower part of that front
fn breakersPlunge( C: BrkCtx, r: f32, r2: f32 ) -> BrkPN {
	let f = breakersFacePoint( C, r2 * 0.45 + 0.55 );
	let t = C.root + C.d3 * ( C.xr + ( r - 0.5 ) * 0.5 );
	let inTrough = vec3f( t.x, C.trough + 0.03, t.z );
	let open = C.xr > C.Wt + 0.1;
	return BrkPN( select( f.p + f.n * 0.04, inTrough, open ), select( f.n, vec3f( 0.0, 1.0, 0.0 ), open ) );
}

// the leading edge of the falling lip
fn breakersTipAt( C: BrkCtx, r: f32 ) -> vec3f {
	return C.root + C.d3 * ( C.Xi * C.q * ( r * 0.12 + 0.88 ) ) - vec3f( 0.0, 1.0, 0.0 ) * ( C.Yi * C.q * C.q );
}

fn breakersAlong( C: BrkCtx, r: f32 ) -> vec3f {
	return C.tg * ( ( r - 0.5 ) * 0.69 );
}

fn breakersCount( rate: f32, gain: f32, seed: u32, salt: u32 ) -> u32 {
	return u32( floor( rate * gain + sprayRand( seed, salt ) ) );
}

fn breakersEmit( i: u32, slot: u32, root: vec3f, dir: vec2f, b: f32, H: f32, trough: f32, c: f32, m: f32, depth: f32, bore: vec4f ) {
	let camFade = smoothstep( breakersP.emitRange, breakersP.emitRange * 0.35, length( root - breakersP.cameraPos ) );
	let gain = breakersP.spray * camFade * frame.dt;
	// the many sub-pixel drops and ligaments take the ring budget; the few, visible spray and mist
	// sprites always get their full rate
	let gainD = gain * breakersP.budget;
	let d3 = vec3f( dir.x, 0.0, dir.y );
	let tg = vec3f( - dir.y, 0.0, dir.x );
	let up = vec3f( 0.0, 1.0, 0.0 );
	let Hc = clamp( H, 0.15, 3.0 );
	// (the energy released goes as H^2.5, but what reads on screen saturates: bigger breakers make
	// bigger, longer-lived structures, not ever more particles)
	let E = pow( Hc, 1.7 );
	let Hb = max( bore.x, 0.0 );
	let Eb = pow( min( Hb, 2.0 ) / 0.6, 1.7 );
	let Wt = bore.y;
	let q = clamp( b / 0.9, 0.0, 1.0 );
	let Xi = H * 0.8;
	let Yi = max( root.y - trough, 0.05 );
	let seed = i * 7919u + u32( m - floor( m / 64.0 ) * 64.0 ) * 31u;
	// integer part: which crest made the particle (its wave shadow, see sprayShadow); fraction: random
	let tag = f32( i * 2u + slot + 1u );
	let offshore = max( - dot( frame.windDir, dir ), 0.0 ) * frame.windSpeed;
	let wB = bore.w;
	let xr = Xi - bore.z; // horizontal distance from the crest top
	let C = BrkCtx( root, d3, tg, Yi, Wt, wB, trough, xr, Xi, q );

	// ---- stages of the breaker (weights 0..1)
	let lipShed = smoothstep( 0.5, 0.75, b ) * ( 1.0 - smoothstep( 0.86, 0.93, b ) );
	// the splash-up is a fast burst (~0.15-0.2 s) as the lip hits the trough; its mist lingers
	let impact = smoothstep( 0.87, 0.92, b ) * ( 1.0 - smoothstep( 1.0, 1.14, b ) );
	let haze = smoothstep( 0.9, 1.0, b ) * ( 1.0 - smoothstep( 1.2, 1.5, b ) );
	let spit = smoothstep( 0.98, 1.08, b ) * ( 1.0 - smoothstep( 1.15, 1.3, b ) );
	let roller = smoothstep( 1.05, 1.3, b ) * smoothstep( 0.06, 0.25, Hb );
	let clash = roller * smoothstep( 0.1, 0.2, depth ) * smoothstep( 0.7, 0.35, depth );
	let drift = smoothstep( 9.0, 15.0, offshore ) * smoothstep( -0.4, 0.1, b ) * ( 1.0 - smoothstep( 0.75, 0.9, b ) );

	// ---- drops and ligaments (ballistic)
	let n0 = breakersCount( lipShed * 9.0 * E, gainD, seed, 1u ); // drops off the lip
	let n1 = n0 + breakersCount( lipShed * 5.0 * E, gainD, seed, 2u ); // ligaments off the lip
	let n2 = n1 + breakersCount( impact * 240.0 * E, gainD, seed, 3u ); // splash-up drops
	let n3 = n2 + breakersCount( impact * 90.0 * E, gainD, seed, 4u ); // splash-up ligaments (torn strands)
	let n4 = n3 + breakersCount( roller * 45.0 * Eb, gainD, seed, 5u ); // roller front (breaks up its silhouette)
	let n5 = n4 + breakersCount( clash * 40.0 * Eb, gainD, seed, 6u ); // bore / backwash collision
	let n6 = n5 + breakersCount( drift * 24.0 * Hc, gainD, seed, 7u ); // spindrift
	if ( n6 > 0u ) {
		let base = sprayReserve( n6 );
		for ( var j = 0u; j < n6; j++ ) {
			let ring = spraySlot( base, j );
			let h = seed + j * 13u;
			let r0 = sprayRand( h, 11u );
			let r1 = sprayRand( h, 12u );
			let r2 = sprayRand( h, 13u );
			let r3 = sprayRand( h, 14u );
			let r4 = sprayRand( h, 15u );
			let isLip = j < n1;
			let isImp = j < n3;
			let isRol = j < n4;
			let isCl = j < n5;
			let lig = ( j >= n0 && j < n1 ) || ( j >= n2 && j < n3 );
			// lip: moving with the jet (thrown forward a little faster than the wave, falling)
			let vLip = d3 * ( c * ( r2 * 0.2 + 1.05 ) ) - up * ( q * sqrt( Yi * 19.62 ) * ( r3 * 0.3 + 0.7 ) ) + tg * ( ( r4 - 0.5 ) * 0.6 );
			// splash-up: most drops stay low, some reach ~1.3 H; thrown up and forward, out of the surface
			let pl = breakersPlunge( C, r1, r2 );
			let vUp = sqrt( Hc * ( r3 * r3 * 1.0 + 0.3 ) * 19.62 );
			let vImp = up * vUp + pl.n * ( r4 * 1.5 ) + d3 * ( c * ( r2 * 0.6 + 0.15 ) - 0.3 ) + tg * ( ( r4 - 0.5 ) * 2.0 );
			// roller: tossed forward and up by the tumbling front (from its upper part)
			let fr = breakersFacePoint( C, r1 * r1 * 0.7 ); // mostly from the tumbling top
			let vRol = d3 * ( c * ( r2 * 0.3 + 0.95 ) ) + up * ( ( r3 * 1.6 + 0.6 ) * sqrt( Hb / 0.5 ) ) + fr.n * 0.5 + tg * ( ( r4 - 0.5 ) * 1.2 );
			// bore running into the backwash: thrown straight up from its front
			let fc = breakersFacePoint( C, r1 * 0.5 + 0.3 );
			let vCl = up * ( ( r3 * 1.6 + 0.8 ) * sqrt( Hb / 0.4 ) ) + d3 * ( r2 * 2.0 - 0.6 ) + tg * ( ( r4 - 0.5 ) * 1.5 );
			// spindrift: fine drops blown back over the crest
			let vDr = d3 * ( - ( offshore * ( r2 * 0.3 + 0.35 ) ) ) + up * ( r3 * 1.5 + 0.8 ) + tg * ( ( r4 - 0.5 ) * 0.5 );
			let p = select( select( select( select( root + up * 0.04, fc.p + fc.n * 0.03, isCl ), fr.p + fr.n * 0.03, isRol ), pl.p, isImp ), breakersTipAt( C, r1 ), isLip ) + breakersAlong( C, r0 );
			let v = select( select( select( select( vDr, vCl, isCl ), vRol, isRol ), vImp, isImp ), vLip, isLip );
			// radius (m): drops of a few mm (smaller off the roller, finest in the spindrift), ligaments ~1-2 cm
			// heavy-tailed drop sizes (many fine drops, a few big ones): r = r0 (1 - u)^-0.7
			let tail = pow( 1.0 - r4 * 0.98, -0.7 );
			let rDrop = min( select( select( select( 0.0005, 0.001, isLip ), 0.0008, isRol ), 0.001, isImp || isCl ) * tail, 0.012 );
			let rLig = 0.005 + r4 * r4 * select( 0.008, 0.016, isImp );
			let kind = select( SPRAY_DROPLET, SPRAY_LIGAMENT, lig );
			sprayWrite( ring, p, v, select( rDrop, rLig, lig ), kind, 2.5, tag + r0 * 0.999 );
		}
	}

	// ---- dense spray (clouds of drops: the white of the splash-up), a puff out of the barrel
	let c0 = breakersCount( impact * 110.0 * E, gain, seed, 21u );
	let c1 = c0 + breakersCount( spit * 10.0 * E, gain, seed, 22u );
	let c2 = c1 + breakersCount( roller * 0.6 * Eb, gain, seed, 23u ); // (rare: a row of them reads as cotton puffs)
	let c3 = c2 + breakersCount( clash * 2.0 * Eb, gain, seed, 24u );
	if ( c3 > 0u ) {
		let base = sprayReserve( c3 );
		for ( var j = 0u; j < c3; j++ ) {
			let ring = spraySlot( base, j );
			let h = seed + j * 29u;
			let r0 = sprayRand( h, 41u );
			let r1 = sprayRand( h, 42u );
			let r2 = sprayRand( h, 43u );
			let r3 = sprayRand( h, 44u );
			let isImp = j < c0;
			let isSpit = j < c1;
			let isRol = j < c2;
			// torn sheets of the splash-up (half-width): many small ones, a few big
			// (heavy-tailed: many small torn sheets, a few big ones; never a row of equal puffs)
			let sz = select( select( select( 0.05 + r2 * r2 * 0.08, sqrt( Hb / 0.5 ) * ( r2 * r2 * 0.06 + 0.03 ), isRol ), sqrt( Hc ) * ( r2 * r2 * 0.14 + 0.05 ), isSpit ), sqrt( Hc ) * ( r2 * r2 * r2 * 0.18 + 0.05 ), isImp );
			// splash-up: sheets thrown up and forward out of the plunge line, rising up to ~1.3 H and
			// falling back as curtains
			let pl = breakersPlunge( C, r1, r3 );
			let pImp = pl.p + pl.n * ( sz * 0.4 ) + up * ( r2 * Hc * 0.1 );
			let vImp = up * sqrt( Hc * ( r3 * r3 * 0.95 + 0.35 ) * 19.62 ) + pl.n * 0.8 + d3 * ( c * ( r2 * 0.5 + 0.2 ) ) + tg * ( ( r1 - 0.5 ) * 1.2 );
			// the puff blown out of the collapsing barrel: out of the middle of the front, along the crest
			let fs = breakersFacePoint( C, r1 * 0.3 + 0.35 );
			let pSpit = fs.p + fs.n * ( sz * 0.5 );
			let vSpit = d3 * ( c * 0.7 ) + fs.n * 1.2 + tg * ( ( r1 - 0.5 ) * 4.0 );
			// the tumbling top of the roller
			let fr = breakersFacePoint( C, r1 * 0.4 );
			let pRol = fr.p + fr.n * ( sz * 0.4 );
			let vRol = d3 * ( c * 0.85 ) + up * ( r3 * 0.5 + 0.3 );
			let fc = breakersFacePoint( C, r1 * 0.5 + 0.3 );
			let pCl = fc.p + fc.n * ( sz * 0.5 );
			let vCl = up * ( ( r3 * 1.2 + 0.8 ) * sqrt( Hb / 0.4 ) ) + d3 * ( r2 - 0.3 );
			let p = select( select( select( pCl, pRol, isRol ), pSpit, isSpit ), pImp, isImp ) + breakersAlong( C, r0 );
			let v = select( select( select( vCl, vRol, isRol ), vSpit, isSpit ), vImp, isImp );
			let life = select( select( r3 * 0.3 + 0.5, 0.9, isSpit ), r3 * 0.5 + 1.0, isImp );
			sprayWrite( ring, p, v, sz, SPRAY_SPRAY, life, tag + r0 * 0.999 );
		}
	}

	// ---- mist: the fine spray that drifts off with the wind (and the air pushed by the wave)
	// (few, large, faint sprites: mist is the biggest overdraw of the spray)
	let m0 = breakersCount( haze * 5.0 * E + spit * 3.0 * E, gain, seed, 31u );
	// the lip feathers as it throws: a thin wisp torn off the crest and carried by the air (with the
	// wind; the drag toward the air velocity turns it back over the crest in an offshore wind)
	let mL = breakersCount( lipShed * 1.6 * E, gain, seed, 33u );
	// the churning top of the bore smokes: a thin haze drifting off it softens its silhouette (a bore
	// front isn't a hard white edge), some of it lingering as it drifts up the beach
	let m1 = m0 + breakersCount( roller * 4.5 * Eb, gain, seed, 32u );
	if ( mL > 0u ) {
		let base = sprayReserve( mL );
		for ( var j = 0u; j < mL; j++ ) {
			let ring = spraySlot( base, j );
			let h = seed + j * 23u;
			let r0 = sprayRand( h, 61u );
			let r1 = sprayRand( h, 62u );
			let r2 = sprayRand( h, 63u );
			let p = breakersTipAt( C, r1 * 0.4 ) + up * ( r2 * 0.1 ) + breakersAlong( C, r0 );
			let v = d3 * ( c * ( r1 * 0.2 + 0.85 ) ) + up * ( r2 * 0.8 + 0.3 );
			sprayWrite( ring, p, v, ( r2 * r2 * 0.2 + 0.1 ) * sqrt( Hc ), SPRAY_MIST, r1 * 0.8 + 0.7, tag + r0 * 0.999 );
		}
	}
	if ( m1 > 0u ) {
		let base = sprayReserve( m1 );
		for ( var j = 0u; j < m1; j++ ) {
			let ring = spraySlot( base, j );
			let h = seed + j * 17u;
			let r0 = sprayRand( h, 51u );
			let r1 = sprayRand( h, 52u );
			let r2 = sprayRand( h, 53u );
			let r3 = sprayRand( h, 54u );
			let isImp = j < m0;
			let pl = breakersPlunge( C, r1, r3 );
			let ft = breakersFacePoint( C, r1 * 0.3 );
			let p = select( ft.p + ft.n * 0.15 + up * ( r2 * r2 * Hb * 0.35 ), pl.p + pl.n * 0.2 + up * ( r2 * Hc * 0.4 ), isImp ) + breakersAlong( C, r0 );
			let v = select( d3 * ( c * ( r1 * 0.3 + 0.65 ) ) + up * ( r2 * 0.4 + 0.15 ), d3 * ( c * ( r1 * 0.3 + 0.4 ) ) + up * ( r3 * 0.9 + 0.4 ), isImp );
			let size = select( ( r3 * r3 * 0.35 + 0.18 ) * sqrt( max( Hb, 0.2 ) / 0.6 ), sqrt( Hc ) * ( r3 * 0.3 + 0.45 ), isImp );
			let life = select( r3 * r3 * 3.0 + 1.2, r3 * 1.5 + 2.5, isImp );
			sprayWrite( ring, p, v, size, SPRAY_MIST, life, tag + r0 * 0.999 );
		}
	}
}


// One crest of wave m at Lagrangian point pc (station i): store the lip frame and emit spray.
fn breakersProcessCrest( i: u32, pc: vec2f, m: f32 ) {
	let ph = shorePhaseAt( pc );
	let dir = ph.dir;
	let along = ph.along;
	let ground = terrainHeightAt( pc );
	let depth = frame.seaLevel - ground;
	let A = shoreWaveAmp( m, along );
	let cr = shoreCrest( A, depth ); // ( b, H, trough, lipThrow )
	let b = cr.x;
	let env = smoothstep( 26.0, 13.0, depth ) * sat( ph.exposure * 1.4 ) * shoreP.enabled;

	// followed from before it breaks until the bore reaches the shore (the lip sheet only uses b < 1.25)
	if ( b > -0.6 && env > 0.3 && depth > 0.12 ) {
		let c = sqrt( clamp( depth, 0.3, 25.0 ) * BRK_GRAVITY );
		let lam = c * shoreP.period;
		let s0 = shoreShape( 0.0, A, depth, lam );
		let ub = 0.4 / lam;
		let s1 = shoreShape( ub, A, depth, lam );
		let fd = breakersFftDisp( pc, depth );
		let d3 = vec3f( dir.x, 0.0, dir.y );
		let root = vec3f( pc.x, frame.seaLevel, pc.y ) + d3 * ( s0.x * env ) + vec3f( 0.0, s0.y * env, 0.0 ) + fd;
		let back = vec3f( pc.x, frame.seaLevel, pc.y ) + d3 * ( s1.x * env - 0.4 ) + vec3f( 0.0, s1.y * env, 0.0 ) + fd;
		let H = cr.y * env;
		let trough = frame.seaLevel + cr.z * env + fd.y;
		// slot by wave parity (neighbouring stations agree on it), id = wave index mod 1024, + 1 (0 = none)
		let slot = u32( m - floor( m * 0.5 ) * 2.0 );
		let id = m - floor( m / 1024.0 ) * 1024.0 + 1.0;
		let k = i * 6u + slot * 3u;
		breakersCrestW[ k ] = vec4f( root, b );
		breakersCrestW[ k + 1u ] = vec4f( back, H );
		breakersCrestW[ k + 2u ] = vec4f( dir, trough, id );
		breakersEmit( i, slot, root, dir, b, H, trough, c, m, depth, shoreBore( A, depth ) );
	}
}

@compute @workgroup_size( 64, 1, 1 )
fn main( @builtin( global_invocation_id ) gid: vec3u ) {
	let i = gid.x;
	if ( i >= 577u ) { return; }
	let st = breakersStations[ i ];
	let o = st.xy;
	let n = st.zw; // toward the sea
	let base = i * 6u;
	// clear both slots
	breakersCrestW[ base + 2u ] = vec4f( 0.0 );
	breakersCrestW[ base + 5u ] = vec4f( 0.0 );

	var sPrev = 0.0;
	for ( var k = 0; k < 64; k++ ) {
		let dist = f32( k ) * 2.0;
		let s = shorePhaseAt( o + n * dist ).s;
		// s grows seaward: a crest (integer phase) lies between this sample and the previous one
		if ( k > 0 && floor( s ) > floor( sPrev ) ) {
			let m = floor( s );
			// secant refinement on the exact phase
			var lo = dist - 2.0;
			var hi = dist;
			var sLo = sPrev;
			var sHi = s;
			var x = lo + ( m - sLo ) / max( sHi - sLo, 1e-5 ) * 2.0;
			for ( var it = 0; it < 2; it++ ) {
				let sx = shorePhaseAt( o + n * x ).s;
				if ( sx < m ) {
					lo = x;
					sLo = sx;
				} else {
					hi = x;
					sHi = sx;
				}
				x = lo + ( m - sLo ) / max( sHi - sLo, 1e-5 ) * ( hi - lo );
			}
			let pc = o + n * x;
			breakersProcessCrest( i, pc, m );
		}
		sPrev = s;
	}
}
