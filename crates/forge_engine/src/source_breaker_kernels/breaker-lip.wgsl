// SPDX-License-Identifier: MIT
// Adapted from Tidewater breaking lip, copyright 2026 DRG Software Solutions LLC.
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

struct SkyParams {
	moonDir: vec3f,
	sunDiskIntensity: f32,
	starIntensity: f32,
};

struct AtmosphereParams {
	rayleighScale: f32,
	mieScale: f32,
	mieG: f32,
	ozoneScale: f32,
	groundAlbedo: vec3f,
	viewHeight: f32,
	sunIlluminance: vec3f,
	pad0: f32,
	sunDir: vec3f,
	pad1: f32,
};

struct LipMaterialParams {
	color: vec3f,
	opacity: f32,
	emissive: vec3f,
	roughness: f32,
	metalness: f32,
	alphaTest: f32,
	sheet: f32,
};

var<private> frame:Frame;
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> lipFrame:Frame;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var<storage,read> breakersCrest:array<vec4f>;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var brkLace:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var smpAnisoRepeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var<uniform> atmosphereParams:AtmosphereParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var<uniform> skyParams:SkyParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(6) var atmosphereTransmittanceLUT:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(7) var atmosphereSkyViewLUT:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(8) var smpLinearClamp:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(9) var<uniform> mat:LipMaterialParams;

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


fn breakersFresnel( cosI: f32, eta: f32 ) -> f32 {
	let c = clamp( cosI, 0.0, 1.0 );
	let g2 = eta * eta - 1.0 + c * c;
	let g = sqrt( max( g2, 0.0 ) );
	let a = ( g - c ) / ( g + c );
	let b = ( c * ( g + c ) - 1.0 ) / ( c * ( g - c ) + 1.0 );
	return select( 0.5 * a * a * ( b * b + 1.0 ), 1.0, g2 < 0.0 );
}


const BRK_LACE_TILE: f32 = 3.5;
const BRK_NV: f32 = 20.0;


const ATMO_RG: f32 = 6360.0;
const ATMO_RT: f32 = 6460.0;

struct AtmosphereMedium {
	rayScat: vec3f,
	mieScat: f32,
	extinction: vec3f,
	scattering: vec3f,
};

fn atmosphereMedium( hKm: f32 ) -> AtmosphereMedium {
	let rayDensity = exp( - hKm / 8.0 );
	let mieDensity = exp( - hKm / 1.2 );
	let ozoneDensity = max( 0.0, 1.0 - abs( hKm - 25.0 ) / 15.0 );
	var m: AtmosphereMedium;
	m.rayScat = vec3f( 5.802e-3, 13.558e-3, 33.1e-3 ) * rayDensity * atmosphereParams.rayleighScale;
	m.mieScat = 3.996e-3 * mieDensity * atmosphereParams.mieScale;
	let mieExt = 4.440e-3 * mieDensity * atmosphereParams.mieScale;
	let ozoneAbs = vec3f( 0.650e-3, 1.881e-3, 0.085e-3 ) * ozoneDensity * atmosphereParams.ozoneScale;
	m.extinction = m.rayScat + mieExt + ozoneAbs;
	m.scattering = m.rayScat + m.mieScat;
	return m;
}

// (r, mu) -> transmittance LUT uv
fn atmosphereTransmittanceUV( r: f32, mu: f32 ) -> vec2f {
	let H = sqrt( ATMO_RT * ATMO_RT - ATMO_RG * ATMO_RG );
	let rho = sqrt( max( r * r - ATMO_RG * ATMO_RG, 0.0 ) );
	let disc = r * r * ( mu * mu - 1.0 ) + ATMO_RT * ATMO_RT;
	let d = max( 0.0, - r * mu + sqrt( max( disc, 0.0 ) ) );
	let dMin = ATMO_RT - r;
	let dMax = rho + H;
	let xMu = ( d - dMin ) / ( dMax - dMin );
	let xR = rho / H;
	// unit -> sub-uv
	return vec2f( ( xMu + 0.001953125 ) * 0.9961089494163424, ( xR + 0.0078125 ) * 0.9846153846153847 );
}

// nearest positive ray-sphere intersection from ro (km, planet centered), -1 if none
fn atmosphereRaySphereNearest( ro: vec3f, rd: vec3f, radius: f32 ) -> f32 {
	let b = dot( ro, rd );
	let c = dot( ro, ro ) - radius * radius;
	let disc = b * b - c;
	let sq = sqrt( max( disc, 0.0 ) );
	let t0 = - b - sq;
	let t1 = - b + sq;
	return select( select( select( -1.0, t1, t1 > 0.0 ), t0, t0 > 0.0 ), -1.0, disc < 0.0 );
}


fn atmosphereSampleTransmittance( r: f32, mu: f32 ) -> vec3f {
	return textureSampleLevel( atmosphereTransmittanceLUT, smpLinearClamp, atmosphereTransmittanceUV( r, mu ), 0.0 ).rgb;
}
// Transmittance from the viewer toward direction dir (for sun disk / sun light color).
fn atmosphereTransmittanceToSpace( dir: vec3f ) -> vec3f {
	return atmosphereSampleTransmittance( atmosphereParams.viewHeight, dir.y );
}


// Sky luminance (no sun disk) for world direction dir (normalized).
fn atmosphereSkyLuminance( dir: vec3f ) -> vec3f {
	let viewH = atmosphereParams.viewHeight;
	let vHorizon = sqrt( max( viewH * viewH - ATMO_RG * ATMO_RG, 0.0 ) );
	let beta = acos( vHorizon / viewH );
	let zenithHorizonAngle = PI - beta;
	let viewZenithAngle = acos( clamp( dir.y, -1.0, 1.0 ) );

	let vCoordA = ( 1.0 - sqrt( max( 1.0 - viewZenithAngle / zenithHorizonAngle, 0.0 ) ) ) * 0.5;
	let vCoordB = sqrt( max( ( viewZenithAngle - zenithHorizonAngle ) / beta, 0.0 ) ) * 0.5 + 0.5;
	let v = select( vCoordB, vCoordA, viewZenithAngle < zenithHorizonAngle );

	// azimuth relative to sun
	let sunH = normalize( vec2f( atmosphereParams.sunDir.x, atmosphereParams.sunDir.z ) + vec2f( 1e-5, 0.0 ) );
	let dirH = normalize( vec2f( dir.x, dir.z ) + vec2f( 1e-5, 0.0 ) );
	let lightViewCos = dot( sunH, dirH );
	let u = sqrt( clamp( lightViewCos * -0.5 + 0.5, 0.0, 1.0 ) );

	let suv = vec2f( u * 0.9947916666666666 + 0.0026041666666666665, v * 0.9907407407407407 + 0.004629629629629629 );
	return textureSampleLevel( atmosphereSkyViewLUT, smpLinearClamp, suv, 0.0 ).rgb;
}


// hash without sine (D. Hoskins): keeps full precision for large integer coordinates
fn skyHash13( p: vec3f ) -> f32 {
	var p3 = fract( p * vec3f( 0.1031, 0.1030, 0.0973 ) );
	p3 += dot( p3, p3.yzx + 33.33 );
	return fract( ( p3.x + p3.y ) * p3.z );
}

// Sun disk radiance along dir (already includes atmospheric transmittance).
fn skySunDisk( dir: vec3f ) -> vec3f {
	let cosA = dot( dir, atmosphereParams.sunDir ); // the real sun (frame.sunDir is the moon at night)
	let ang = acos( clamp( cosA, -1.0, 1.0 ) );
	let r = ang / 0.00537625;
	let mask = smoothstep( 1.0, 0.9, r );
	let mu = sqrt( max( 1.0 - r * r, 0.0 ) );
	let limb = 1.0 - 0.6 * ( 1.0 - mu );
	let T = atmosphereTransmittanceToSpace( dir );
	// physically the disk radiance is E / solid angle (~1.6e5); clamp for fp16 targets
	return T * mask * limb * 2500.0 * skyParams.sunDiskIntensity * smoothstep( -0.02, 0.0, dir.y );
}

// Stars: one candidate per cell of a cube-face grid, jittered inside it, with a power law
// magnitude distribution (few bright stars, many faint ones), denser along the Milky Way. They
// come out after civil twilight: the brightest first, the faintest once the sky is fully dark.
fn skyStars( dir: vec3f ) -> vec3f {
	// cube face coordinates
	let a = abs( dir );
	let onX = a.x > a.y && a.x > a.z;
	let onY = a.y > a.z;
	let face = select( select( sign( dir.z ) + 8.0, sign( dir.y ) + 5.0, onY ), sign( dir.x ) + 2.0, onX );
	let uv = select( select( dir.xy / a.z, dir.xz / a.y, onY ), dir.yz / a.x, onX ) * 160.0;
	let cell = vec3f( floor( uv ), face );
	let h = skyHash13( cell );
	let bx = dot( dir, vec3f( 0.2822162605150792, 0.18814417367671948, 0.9407208683835974 ) ) * 4.0;
	let band = exp( - bx * bx );
	// the cell holds a star with probability P (higher along the Milky Way); an independent
	// uniform u ranks its brightness: N( < m ) ~ 10^( m / 2 ), brightest about magnitude -1
	let has = h < band * 0.035 + 0.025;
	let uc = max( skyHash13( cell + 7.7 ), 2e-4 );
	let m = log2( uc ) * 0.602 + 6.5;
	// limiting magnitude: -1 when the sun is 6 deg below the horizon, 6.5 below 16 deg
	let dark = 1.0 - smoothstep( -0.28, -0.1, atmosphereParams.sunDir.y );
	let vis = smoothstep( m - 0.6, m + 0.6, dark * 7.5 - 1.0 ) * select( 0.0, 1.0, has );
	// angular distance to the star (isotropic whatever the cube face distortion), in cells
	let sp = ( floor( uv ) + vec2f( skyHash13( cell + 3.1 ), skyHash13( cell + 5.7 ) ) * 0.4 + 0.3 ) / 160.0;
	let sdir = normalize( select( select( vec3f( sp, sign( dir.z ) ), vec3f( sp.x, sign( dir.y ), sp.y ), onY ), vec3f( sign( dir.x ), sp ), onX ) );
	let d = length( dir - sdir ) * 160.0;
	// flux relative to a magnitude 6.5 star; bright stars look bigger (glare), not just clipped
	let flux = pow( uc, -0.8 );
	let size = log2( flux ) * 0.08 + 1.0;
	let psf = exp( d * d / ( size * size ) * -49.99999999999999 ) / ( size * size );
	// subtle scintillation, stronger low in the sky
	let tw = sin( frame.time * ( skyHash13( cell + 13.3 ) * 9.0 + 5.0 ) + h * 60.0 ) * mix( 0.18, 0.06, sat( dir.y * 2.0 ) ) + 1.0;
	let col = mix( vec3f( 1.0, 0.8, 0.6 ), vec3f( 0.75, 0.85, 1.0 ), skyHash13( cell + 17.0 ) ) * 0.5 + 0.5;
	let star = col * ( psf * flux * vis * tw * 0.0075 );
	// diffuse glow of the Milky Way
	let glow = vec3f( 0.55, 0.6, 0.75 ) * ( band * dark * 0.0035 );
	// atmospheric extinction toward the horizon
	return ( star + glow ) * skyParams.starIntensity * smoothstep( 0.0, 0.2, dir.y );
}

fn skyMoon( dir: vec3f ) -> vec3f {
	let cosA = dot( dir, skyParams.moonDir );
	let ang = acos( clamp( cosA, -1.0, 1.0 ) );
	let r = ang / 0.0048;
	let mask = smoothstep( 1.0, 0.92, r );
	return vec3f( 0.9, 0.92, 1.0 ) * mask * 3.0 * skyParams.starIntensity * smoothstep( -0.02, 0.02, dir.y );
}

// Faint blue-grey moonlit sky (a little brighter toward the horizon) and the moon's aureole.
// Physically dim: about the level of the app's night ambient light.
fn skyMoonSky( dir: vec3f ) -> vec3f {
	let cosA = dot( dir, skyParams.moonDir );
	let ang = acos( clamp( cosA, -1.0, 1.0 ) );
	let aureole = exp( ang * -14.0 ) * 2.4 + exp( ang * -2.5 ) * 0.9;
	let grad = mix( 1.7, 1.0, sat( dir.y * 3.0 ) );
	let up = smoothstep( -0.05, 0.15, skyParams.moonDir.y );
	return vec3f( 0.005, 0.0068, 0.0105 ) * ( grad + aureole ) * frame.night * up;
}

// Everything behind the clouds except the sun and moon disks. The night terms are only
// evaluated at night (uniform branch).
fn skyBackground( dir: vec3f, starK: f32 ) -> vec3f {
	var L = atmosphereSkyLuminance( dir );
	if ( skyParams.starIntensity > 0.001 ) {
		L += skyMoonSky( dir ) + skyStars( dir ) * starK;
	}
	return L;
}

// Full sky radiance for a direction (no clouds).
fn skyRadiance( dir: vec3f, withSun: bool ) -> vec3f {
	var L = skyBackground( dir, 1.0 ) + skyMoon( dir );
	if ( withSun ) { L += skySunDisk( dir ); }
	return L;
}

// Sky with clouds composited (low resolution cloud panorama). withSun = false (environment
// map): no sun or moon disk, the key light is lit directly.
fn skyRadianceWithClouds( dir: vec3f, withSun: bool ) -> vec3f {
	var base: vec3f;
	if ( withSun ) { base = skyBackground( dir, 1.0 ) + skyMoon( dir ) + skySunDisk( dir ); }
	else { base = skyBackground( dir, 0.08 ); }
	return base;
}

// Water reflections: no moon disk (the water's specular lobe reflects the key light) and only a
// trace of the stars, which rough water would spread into flickering sparkles.
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

@group(#{MATERIAL_BIND_GROUP}) @binding(10) var<uniform> cloudsParams:CloudsParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(11) var cloudsPanorama:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(12) var smpLinearRepeat:sampler;
fn cloudsSample( dir: vec3f ) -> vec4f {
	let az = atan2( dir.z, dir.x );
	let u = fract( az / 6.283185307179586 );
	let elev = - acos( clamp( dir.y, -1.0, 1.0 ) ) + 1.5707963267948966;
	let t = clamp( ( elev + 0.06981317007977318 ) / 1.6406094968746698, 0.0, 1.0 );
	let s = textureSampleLevel( cloudsPanorama, smpLinearRepeat, vec2f( u, sqrt( t ) ), 0.0 );
	let below = smoothstep( -0.07, -0.03, dir.y );
	return vec4f( s.rgb * below, mix( 1.0, s.a, below ) );
}
fn skyReflectionRadiance( dir: vec3f ) -> vec3f {
	let base = skyBackground( dir, 0.08 );
	let c=cloudsSample(dir); return base*c.a+c.rgb;
}

// Main view background: full resolution clouds for the camera's view.
fn skyViewRadiance( dir: vec3f ) -> vec3f {
	let base = skyBackground( dir, 1.0 ) + skyMoon( dir ) + skySunDisk( dir );
	return base;
}


fn setupFrame(){frame=lipFrame;frame.sourceView=view.view_from_world;frame.proj=view.clip_from_view;frame.invView=view.world_from_view;frame.invProj=view.view_from_clip;frame.invViewProj=view.world_from_clip;frame.cameraPos=view.world_position;}
struct LipVertex{@builtin(position) clip:vec4f,@location(0) P:vec3f,@location(1) vLipN:vec3f,@location(2) vLip:vec4f,@location(3) vLipFade:f32};
@vertex fn vertex(@location(0) position:vec3f,@location(1) sheetId:vec4f)->LipVertex{
 setupFrame();var o:LipVertex;

	let id = sheetId;
	let seg = u32( id.x );
	let slot = u32( id.y );
	let side = u32( id.z );
	let k = id.w;
	let st = seg + side;
	let ot = seg + ( 1u - side );
	let e = st * 6u + slot * 3u;
	let eo = ot * 6u + slot * 3u;
	let c0 = breakersCrest[ e ];
	let c1 = breakersCrest[ e + 1u ];
	let c2 = breakersCrest[ e + 2u ];
	let mOther = breakersCrest[ eo + 2u ].w;
	let bOther = breakersCrest[ eo ].w;
	// (the crests are followed until the bore reaches the shore; the sheet is gone after the plunge)
	// (port: both ends of a segment must pass the b test, else a vertex pair collapses to one side only
	// and the quad between them draws as a long sliver down to y = -1e5 where b jumps between stations)
	let valid = c2.w > 0.5 && abs( mOther - c2.w ) < 0.5 && c0.w < 1.25 && bOther < 1.25;

	let root = c0.xyz;
	let b = c0.w;
	let back = c1.xyz;
	let H = c1.w;
	let d3 = vec3f( c2.x, 0.0, c2.y );
	let trough = c2.z;
	let q = clamp( b / 0.9, 0.0, 1.35 );
	let Xi = max( H * 0.8, 0.05 );
	let Yi = max( root.y - trough, 0.05 );
	// profile parameter: k = 0, 1 on the back of the crest (-1, -0.45), then 0..1 along the curtain
	let pv = select( select( ( k - 2.0 ) / ( BRK_NV - 3.0 ), -0.45, k < 1.5 ), -1.0, k < 0.5 );
	let xl = Xi * q * max( pv, 0.0 );
	let fl = xl / Xi;
	let yl = - Yi * ( fl * fl ); // ballistic: the jet leaves the crest horizontally
	let onLip = root + d3 * xl + vec3f( 0.0, yl, 0.0 );
	let onCap = mix( root, back, max( - pv, 0.0 ) ) + vec3f( 0.0, 0.012, 0.0 );
	let P = select( onLip, onCap, pv < 0.0 );
	// outward normal of the curtain (upper surface of the lip)
	let slope = Yi * 2.0 * xl / ( Xi * Xi );
	o.vLipN = normalize( vec3f( 0.0, 1.0, 0.0 ) + d3 * slope );
	o.vLip = vec4f( pv, b, f32( st ) * 0.6, Xi * q + Yi * q * q ); // w: curtain length (m)
	// the cap fades in over the crest; the whole lip fades out once it has become whitewater
	let capA = smoothstep( -1.0, -0.1, pv );
	// once the jet has re-entered the water the curtain is gone (the splash and the roller take over)
	o.vLipFade = capA * smoothstep( 0.02, 0.12, q ) * ( 1.0 - smoothstep( 1.0, 1.2, b ) );
o.P=select(vec3f(0.0,-1e5,0.0),P,valid);o.clip=position_world_to_clip(o.P);return o;}
struct LipInput{P:vec3f,front:bool,vs:LipVertex};
struct LipResult{color:vec4f};
fn shade(in:LipInput)->LipResult{var r:LipResult;

	let v = in.vs.vLip.x;
	let b = in.vs.vLip.y;
	let a = in.vs.vLip.z;
	let len = in.vs.vLip.w;
	let pos = in.P;
	let V = normalize( frame.cameraPos - pos );
	let Nw = normalize( in.vs.vLipN );
	let N0 = select( - Nw, Nw, in.front );
	let t = frame.time;
	// The water of the jet is stretched along the flow: streaks and ripples running down the
	// curtain (the lace pattern stretched ~8x along the jet and moving with it) in its surface
	// (normal, from the screen-space gradient of a small relief) and in its thickness
	let flowS = v * len - t * 1.1;
	// along-crest coordinate warped by low-frequency noise (three incommensurate scales, slope kept
	// below 1 so it never folds): the streak spacing drifts along the crest, no fixed period, and the
	// tiles of every pattern below never line up into a comb
	let aw = a + sin( a * 0.23 + 1.7 ) * 1.6 + sin( a * 0.61 + 4.2 ) * 0.4 + sin( a * 1.37 + 0.4 ) * 0.12;
	let sv = textureSample( brkLace, smpAnisoRepeat, vec2f( aw / 0.83, flowS / 3.0 ) );
	// and broad bands (sections of the lip thicker or thinner than others), visible from afar
	let sbv = textureSample( brkLace, smpAnisoRepeat, vec2f( aw / 2.9, flowS / 9.0 ) + vec2f( 0.37, 0.61 ) );
	let sb = sbv.z;
	let hS = sv.x * 0.02 + sv.z * 0.012;
	let dpx = dpdx( pos );
	let dpy = dpdy( pos );
	let r1 = cross( dpy, N0 );
	let r2 = cross( N0, dpx );
	let det = dot( dpx, r1 );
	let grad = ( r1 * dpdx( hS ) + r2 * dpdy( hS ) ) * sign( det );
	let N = normalize( N0 * abs( det ) - grad + N0 * 1e-9 );
	let NdV = max( dot( N, V ), 1e-3 );
	let F = breakersFresnel( NdV, 1.333 );
	let L = frame.sunDir;
	let sun = frame.sunColor * cloudsShadow( pos.xz );

	// reflection: sky + sun glint
	let Rr = reflect( - V, N );
	let R = normalize( vec3f( Rr.x, max( Rr.y, 0.004 ), Rr.z ) );
	let refl = skyReflectionRadiance( R );
	let Hh = normalize( L + V );
	let spec = sun * ( pow( max( dot( N, Hh ), 0.0 ), 180.0 ) * 12.0 ) * F;

	// light through the thin sheet: turquoise when backlit
	let tint = vec3f( 0.16, 0.62, 0.56 );
	let back = pow( sat( dot( - V, L ) * 0.5 + 0.5 ), 4.0 );
	// thick and deep green at the root, thin, bright and clear toward the tip, uneven along the streaks
	let thin = mix( 1.5, 0.7, v ) * ( sv.z * 0.3 + 0.8 ) * ( sb * 0.8 + 0.6 );
	let glow = ( sun * tint * ( back * 0.9 + 0.08 ) + frame.skyIrradiance * tint * 1.4 ) * thin;
	let aW = F + min( ( 1.0 - F ) * mix( 0.42, 0.16, v ) * ( sv.x * 0.5 + 0.75 ) * ( sb * 0.7 + 0.65 ), 0.85 );
	let cW = refl * F + spec + glow * ( 1.0 - F ) * 0.42;

	// The jet stays clear, glassy water while it is in the air: only its leading edge tears into
	// aerated fingers (filaments of the lace pattern stretched along the flow, merging into a
	// ragged white rim). Sections of the lip differ a little.
	let flowM = v * len - t * 1.1; // metres down the curtain, moving with the jet
	let fuv = vec2f( aw / 1.9 + 0.53, flowM / 1.8 );
	let fl = textureSample( brkLace, smpAnisoRepeat, fuv );
	let sect = fl.z; // slowly varying along the crest
	let vary = sect - 0.5 + sin( aw * 0.29 + b * 2.1 ) * 0.15;
	let reach = v + vary * 0.3;
	// slightly irregular leading edge (sections of the lip reach a little further than others)
	let tipN = sin( aw * 1.3 + t * 0.3 ) * 0.6 + sin( aw * 4.7 + 1.3 ) * 0.4;
	let edge = v + tipN * 0.03 + vary * 0.04;
	let thrown = smoothstep( 0.35, 0.8, b );
	// a thin, translucent aerated rim along the leading edge (it tears into the drops and
	// ligaments the spray system throws off it)
	let rim = smoothstep( 0.9, 0.96, edge ) * ( fl.z * 0.2 + 0.2 ) * thrown;
	// once the lip has landed the curtain is a falling mass of whitewater that dissolves (blotchy)
	// into the splash-up and the roller within a fraction of a second
	// (in streaks: white fingers run down the curtain ahead of the rest, so along a peeling crest the
	// broken section feathers into the clear one instead of ending on a vertical line)
	// (each finger its own length and brightness: the fine strands' per-cell random, gated and grouped
	// by the broad pattern so fingers cluster, merge and leave gaps instead of a regular row)
	let fing = ( sv.w * 0.55 + sv.z * 0.2 ) * ( smoothstep( 0.25, 0.75, sbv.w * 0.6 + sb * 0.4 ) * 0.8 + 0.2 ) * 0.34;
	let wh = smoothstep( ( 1.0 - v ) * 0.18 + 0.9 - fing, ( 1.0 - v ) * 0.18 + 1.0 - fing, b ); // from the tip up
	let lc = textureSample( brkLace, smpAnisoRepeat, vec2f( aw * 0.83 + 1.9, v * len - t * 1.3 ) / ( BRK_LACE_TILE * 0.8 ) );
	let blot = lc.z * 0.7 + lc.y * 0.3;
	let gone = smoothstep( 1.0, 1.25, b );
	let clumpW = smoothstep( gone - 0.12, gone + 0.12, blot );
	let clumps = sat( ( blot - gone ) * 2.5 ); // thicker inside the blotches
	// thin aerated filaments stretched down the curtain (foam of the previous wave drawn up the
	// face and thrown out with the lip), more of them toward the tip
	let fil = ( 1.0 - smoothstep( 0.02, 0.12, sv.x ) ) * smoothstep( 0.15, 0.8, v ) * ( sv.w * 0.5 + 0.25 ) * ( sb * 0.9 + 0.3 ) * thrown;
	let aer = mix( max( rim, fil ), 0.95, wh );
	// aerated water is a dense scatterer: bright from every side, glowing when backlit
	let foamLit = ( sun * ( max( dot( N, L ), 0.0 ) * 0.5 + 0.5 + back * 1.2 ) / PI + frame.skyIrradiance ) * 0.9;

	let tip = 1.0 - smoothstep( 0.93, 1.0, edge );
	let alpha = in.vs.vLipFade * tip * mat.sheet * mix( 1.0, clumpW, wh );
	let aF = aer * 0.92;
	let col = foamLit * mix( 1.0, clumps * 0.4 + 0.75, wh ) * aF + cW * ( 1.0 - aF );
	let aOut = ( aF + aW * ( 1.0 - aF ) ) * alpha;
	r.color = vec4f( col * alpha, aOut );

return r;}
@fragment fn fragment(v:LipVertex,@builtin(front_facing) front:bool)->@location(0) vec4f{setupFrame();var i:LipInput;i.P=v.P;i.front=front;i.vs=v;return shade(i).color;}


@group(#{MATERIAL_BIND_GROUP}) @binding(13) var cloudsShadowMap:texture_2d<f32>;

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
