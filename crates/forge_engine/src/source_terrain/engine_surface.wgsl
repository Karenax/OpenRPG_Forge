// Source surface adapter only: default Bevy vertex and provisional PBR lighting.
#import bevy_pbr::{forward_io::VertexOutput, mesh_view_bindings::view, pbr_types,
    pbr_functions::{apply_pbr_lighting,main_pass_post_lighting_processing}}
struct TerrainSettings { wind:vec4f,sun:vec4f,domain:vec4f,flags:vec4f };
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var terrainNormalTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var smpLinearClamp:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var terrainSplatTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var terrainDetailTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var smpAniso4Repeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var smpLinearRepeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(6) var<uniform> settings:TerrainSettings;
struct SourceFrame { cameraPos:vec3f,time:f32,windDir:vec2f,windSpeed:f32,sunDir:vec3f };
var<private> frame:SourceFrame;
var<private> terrainParams:TerrainParams;
var<private> mat:SourceMaterialParams;
struct FragInput { P:vec3f,N:vec3f,V:vec3f,pixel:vec2f };
struct SourceSurface { albedo:vec3f,normal:vec3f,emissive:vec3f,
    roughness:f32,metalness:f32,specularIntensity:f32,ao:f32,envIntensity:f32 };
struct TerrainParams {
	origin: f32,
	size: f32,
	res: f32,
	shoreRes: f32,
	sunBake: vec3f,
	sunBaked: f32,
};
struct SourceMaterialParams {
	color: vec3f,
	opacity: f32,
	emissive: vec3f,
	roughness: f32,
	metalness: f32,
	alphaTest: f32,
	viewPos: vec3f,
	wetDarken: f32,
	gustOffset: vec2f,
	envMapIntensity: f32,
	emissiveIntensity: f32,
};

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


fn terrainUvOf( xz: vec2f ) -> vec2f {
	return ( xz - terrainParams.origin ) / terrainParams.size;
}

// exact bilinear height at world xz (matches TerrainData.heightAt)


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


// 0 (in the terrain's shadow) .. 1 (lit) for a world position, soft penumbra that widens with the
// occluder distance


const PAL_rockDark: vec3f = vec3f( 0.0196066485, 0.0184784733, 0.0163381447 );
const PAL_rockMid: vec3f = vec3f( 0.0732389559, 0.0660303346, 0.0570892115 );
const PAL_rockLight: vec3f = vec3f( 0.195994179, 0.174728005, 0.147318999 );
const PAL_rockWarm: vec3f = vec3f( 0.162647194, 0.112804787, 0.0732389559 );
const PAL_lichenPale: vec3f = vec3f( 0.447988412, 0.447988412, 0.367246468 );
const PAL_lichenOrange: vec3f = vec3f( 0.570481595, 0.214041140, 0.0331047666 );
const PAL_blackZone: vec3f = vec3f( 0.00657140361, 0.00657140361, 0.00598105957 );
const PAL_barnacle: vec3f = vec3f( 0.570481595, 0.538235524, 0.447988412 );
const PAL_algae: vec3f = vec3f( 0.0331047666, 0.0592535211, 0.0100228256 );
const PAL_coralline: vec3f = vec3f( 0.342391640, 0.162647194, 0.178867504 );
const PAL_moss: vec3f = vec3f( 0.0300741041, 0.0683848542, 0.00719440929 );
const PAL_mossDry: vec3f = vec3f( 0.0732389559, 0.0946296266, 0.0173891024 );
const MEADOW_lush: vec3f = vec3f( 0.0153252033, 0.0331047666, 0.00393593950 );
const MEADOW_green: vec3f = vec3f( 0.0508760882, 0.0835351274, 0.0100228256 );
const MEADOW_olive: vec3f = vec3f( 0.106539224, 0.112804787, 0.0173891024 );
const MEADOW_yellow: vec3f = vec3f( 0.214041140, 0.178867504, 0.0331047666 );
const MEADOW_straw: vec3f = vec3f( 0.342391640, 0.252950076, 0.0889815256 );
const MEADOW_soil: vec3f = vec3f( 0.0245150137, 0.0153252033, 0.00719440929 );

struct RockGrad {
	dpdx: vec3f,
	dpdy: vec3f,
	fwY: f32,
	useGrad: bool,
};

// implicit-derivative RockGrad for a world position (call in uniform control flow)
fn terrainImplicitGrad( p: vec3f ) -> RockGrad {
	var g: RockGrad;
	g.dpdx = dpdx( p ); g.dpdy = dpdy( p ); g.fwY = 0.0; g.useGrad = false;
	return g;
}

// Mikkelsen surface-gradient bump: perturb world normal N by the scalar height field hd
// (screen-space derivatives, so any mix of projections / scales works). Robustness for terrain:
//  - the tilt is limited to ~55 degrees (at grazing angles |det| collapses and the unclamped
//    gradient would swing the normal into the tangent plane: 'chrome' patches on steep faces)
//  - the bump fades out where the screen-space frame is degenerate (the thin sliver triangles
//    of CDLOD geomorphing, which otherwise light up as bright lines along the grid) or where the
//    rendered facet disagrees with N (sub-texel crags)
fn terrainPerturbNormal( p: vec3f, N: vec3f, hd: f32, scale: f32 ) -> vec3f {
	let dpx = dpdx( p ); let dpy = dpdy( p );
	let dhdx = dpdx( hd ) * scale; let dhdy = dpdy( hd ) * scale;
	let r1 = cross( dpy, N );
	let r2 = cross( N, dpx );
	let det = dot( dpx, r1 );
	let ad = abs( det );
	let grad = ( r1 * dhdx + r2 * dhdy ) * sign( det );
	let area = length( cross( dpx, dpy ) );
	let fr = area / max( length( dpx ) * length( dpy ), 1e-20 ); // sin of the footprint angle
	let facet = ad / max( area, 1e-20 ); // cos between the facet and N
	let k = smoothstep( 0.12, 0.35, fr ) * smoothstep( 0.3, 0.6, facet );
	let g = grad * min( 1.0, ad * 1.4 / max( length( grad ), 1e-20 ) ) * k;
	return normalize( N * max( ad, 1e-20 ) - g );
}

// triplanar blend weights (sharp)
fn terrainTriWeights( N: vec3f ) -> vec3f {
	let a = abs( N );
	let w = a * a * ( a * a );
	return w / ( w.x + w.y + w.z );
}

fn terrainDetailGrad( uv: vec2f, gx: vec2f, gy: vec2f ) -> vec4f {
	return textureSampleGrad( terrainDetailTex, smpAniso4Repeat, uv, gx, gy );
}

// one channel-set of the detail texture, triplanar. The samples use explicit gradients (the
// derivatives of the world position in g), so they may run in non-uniform control flow.
fn terrainTriplanar( p: vec3f, w: vec3f, tile: f32, g: RockGrad ) -> vec4f {
	let s = 1.0 / tile;
	let x = terrainDetailGrad( p.zy * s, g.dpdx.zy * s, g.dpdy.zy * s );
	let y = terrainDetailGrad( p.xz * s + 0.37, g.dpdx.xz * s, g.dpdy.xz * s );
	let z = terrainDetailGrad( p.xy * s + 0.71, g.dpdx.xy * s, g.dpdy.xy * s );
	return x * w.x + y * w.y + z * w.z;
}

struct RockSurface {
	albedo: vec3f,
	rough: f32,
	hd: f32,     // bump height (m)
	moss: f32,   // 0..1
	wet: f32,
	height: f32,
};

// Weathered volcanic rock seen on the headlands, sea stacks and boulders.
//   p world position, N world normal (geometric / mcr), h height above sea level
//   mcr: 0..1 large scale variation, seed: per object variation (0..1)
// With g.useGrad every sample uses the gradients in g (branch safe).
fn terrainRockSurface( p: vec3f, N: vec3f, h: f32, mcr: f32, seed: f32, mossAmount: f32, g: RockGrad ) -> RockSurface {
	let w = terrainTriWeights( N );
	// big blocks (4 m cells), plates (0.9 m) and grain / chips
	let big = terrainTriplanar( p, w, 27.0, g );
	let mid = terrainTriplanar( p, w, 6.1, g );
	let fine = terrainTriplanar( p, w, 1.3, g );
	// pixel footprint (m): features smaller than a few pixels fade out instead of sparkling
	let px = select( length( abs( g.dpdx ) + abs( g.dpdy ) ), max( length( g.dpdx ), length( g.dpdy ) ), g.useGrad );
	let fineK = 1.0 - smoothstep( 0.006, 0.02, px );
	let midK = 1.0 - smoothstep( 0.03, 0.1, px );
	let hr = big.x * 0.45 + mid.x * 0.35 + ( ( fine.x - 0.5 ) * fineK + 0.5 ) * 0.2;

	// layered lava flows / bedding on steep faces: irregular bands (1D lookup of the fbm channel
	// along the height, warped), faded out once a band gets thinner than a few pixels
	let steep = 1.0 - smoothstep( 0.55, 0.85, N.y );
	let bandY = p.y + mid.w * 2.5 + mcr * 6.0;
	var fwY = g.fwY;
	if ( ! g.useGrad ) { fwY = fwidth( bandY ); }
	let bandUV = vec2f( bandY / 14.0, seed * 0.37 + 0.13 );
	var strata: f32;
	if ( g.useGrad ) {
		strata = terrainDetailGrad( bandUV, vec2f( fwY / 14.0, 0.0 ), vec2f( 0.0 ) ).w;
	} else {
		strata = textureSample( terrainDetailTex, smpAniso4Repeat, bandUV ).w;
	}
	let strataAA = 1.0 - smoothstep( 0.15, 0.6, fwY );
	let tone = hr * 0.9 + ( mcr - 0.5 ) * 0.7 + ( strata - 0.5 ) * 0.8 * steep * strataAA + ( seed - 0.5 ) * 0.3;
	var col = mix( PAL_rockDark, PAL_rockMid, smoothstep( 0.1, 0.5, tone ) );
	col = mix( col, PAL_rockLight, smoothstep( 0.5, 0.85, tone ) );
	// iron staining / warm weathering in patches
	col = mix( col, PAL_rockWarm, smoothstep( 0.62, 0.8, mid.w + mcr * 0.3 ) * 0.18 );
	// joints between the big blocks, fainter between plates
	col = col * ( smoothstep( 0.05, 0.25, big.x ) * 0.35 + 0.65 ) * ( smoothstep( 0.05, 0.25, mid.x ) * 0.15 + 0.85 );
	// rain streaks: dark stains running down steep faces, paler bands between (the fbm channel
	// stretched vertically on the two vertical projection planes)
	let sUVa = vec2f( p.z / 3.1, p.y / 41.0 );
	let sUVb = vec2f( p.x / 3.1 + 0.5, p.y / 41.0 + 0.3 );
	let sa = terrainDetailGrad( sUVa, vec2f( g.dpdx.z / 3.1, g.dpdx.y / 41.0 ), vec2f( g.dpdy.z / 3.1, g.dpdy.y / 41.0 ) );
	let sb = terrainDetailGrad( sUVb, vec2f( g.dpdx.x / 3.1, g.dpdx.y / 41.0 ), vec2f( g.dpdy.x / 3.1, g.dpdy.y / 41.0 ) );
	let sw4 = pow( abs( N.xz ), vec2f( 4.0 ) );
	let stainS = ( sa.w * sw4.x + sb.w * sw4.y ) / ( sw4.x + sw4.y + 1e-5 );
	let stain = smoothstep( 0.52, 0.72, stainS ) * steep;
	col = col * ( 1.0 - stain * 0.4 ) * ( smoothstep( 0.35, 0.2, stainS ) * steep * 0.12 + 1.0 );

	// lichens on the dry upper faces
	let dry = smoothstep( 2.6, 4.0, h );
	let lichen = smoothstep( 0.6, 0.78, mid.y ) * smoothstep( 0.2, 0.7, N.y ) * dry * smoothstep( 0.45, 0.65, mcr );
	col = mix( col, PAL_lichenPale, lichen * 0.45 );
	col = mix( col, PAL_lichenOrange, smoothstep( 0.8, 0.88, mid.y ) * dry * smoothstep( 0.5, 0.8, N.y ) * 0.3 );

	// moss / grass on ledges and tops, ferns hanging along the bedding planes of steep faces
	let ledge = smoothstep( 0.55, 0.7, strata ) * steep * strataAA * smoothstep( 0.4, 0.6, mid.w + ( mcr - 0.5 ) * 0.4 );
	let moss = max( smoothstep( 0.62, 0.9, N.y + ( big.x - 0.5 ) * 0.5 + ( mcr - 0.5 ) * 0.3 ), ledge * 0.8 )
		* smoothstep( 2.5, 5.0, h ) * mossAmount;
	col = mix( col, mix( PAL_moss, PAL_mossDry, mid.w ), moss * 0.9 );

	// shoreline zonation: black lichen band (splash zone), barnacles and algae in the intertidal
	let splash = smoothstep( 0.5, 1.0, h ) * smoothstep( 2.8, 1.8, h + mid.w * 1.2 ) * smoothstep( 0.35, 0.6, mcr + mid.w * 0.3 );
	col = mix( col, PAL_blackZone, splash * 0.55 );
	// sun-bleached, weathered upper faces
	col = mix( col, PAL_rockLight, smoothstep( 0.35, 0.95, N.y ) * smoothstep( 1.5, 3.0, h ) * 0.3 );
	let inter = smoothstep( -0.7, -0.2, h ) * smoothstep( 0.7, 0.2, h );
	let barn = smoothstep( 0.62, 0.72, fine.z ) * inter * fineK;
	col = mix( col, PAL_algae, inter * smoothstep( 0.4, 0.6, mid.y ) * 0.6 );
	col = mix( col, PAL_barnacle, barn * 0.8 );
	// below the water: algae films and pink coralline crusts
	let sub = smoothstep( -0.3, -1.2, h );
	col = mix( col, mix( PAL_algae, PAL_coralline, smoothstep( 0.45, 0.7, mid.w ) ), sub * 0.55 );

	// wet below the swash line (dark, glossy)
	let wet = smoothstep( 1.0, 0.25, h + mid.w * 0.3 );
	col = col * mix( 1.0, 0.55, wet );

	var r: RockSurface;
	r.albedo = col;
	r.rough = mix( mix( 0.88, 0.8, steep ), 0.45, wet ) + moss * 0.06;
	// relief (m): tilted blocks and plates with bevelled joints, then grain
	r.hd = big.x * 0.25 + mid.x * 0.07 * ( midK * 0.6 + 0.4 ) + fine.x * 0.012 * fineK * ( 1.0 - wet * 0.6 ) + barn * 0.005;
	r.moss = moss;
	r.wet = wet;
	r.height = hr;
	return r;
}

// saturation helper
fn terrainSaturation( c: vec3f, s: f32 ) -> vec3f {
	return mix( vec3f( luminance( c ) ), c, s );
}

struct MeadowTone {
	tone: vec3f,
	dry: f32,
	lush: f32,
};

//   mA, mB: detail fbm channel at the 173 m / 47 m scales (~0.5 +- 0.1): the samples at
//   rot2( xz, 0.7 ) / 173 and rot2( xz, 2.1 ) / 47 that the terrain takes anyway; slope: 1 - N.y;
//   south: N.z (the sun side); detail: optional finer fbm (~0.5 +- 0.1) that breaks up the patches
//   (hasDetail = false: none)
// Returns { tone, dry, lush }: mostly fresh green grass with olive, sun-bleached yellow and a few
// straw-dry patches (more on exposed slopes), darker lush grass in the damp patches (the hollows
// are darkened further by the AO).
fn terrainMeadowTone( mA: f32, mB: f32, slope: f32, south: f32, detail: f32, hasDetail: bool ) -> MeadowTone {
	var m = mA * 0.55 + mB * 0.45 + slope * 0.25 + south * 0.04;
	let dd = select( 0.0, detail - 0.5, hasDetail );
	m += dd * 0.28;
	let olive = smoothstep( 0.52, 0.6, m );
	let yellow = smoothstep( 0.6, 0.67, m );
	let straw = smoothstep( 0.66, 0.73, m + ( mB - 0.5 ) * 0.2 );
	let lush = smoothstep( 0.46, 0.37, mB * 0.7 + mA * 0.3 + slope * 0.2 + dd * 0.2 );
	var c = mix( MEADOW_green, MEADOW_olive, olive );
	c = mix( c, MEADOW_yellow, yellow * 0.8 );
	c = mix( c, MEADOW_straw, straw * 0.55 );
	c = mix( c, MEADOW_lush, lush * 0.75 );
	var o: MeadowTone;
	o.tone = c;
	o.dry = olive * 0.4 + yellow * 0.6;
	o.lush = lush;
	return o;
}

// meadow weight of the current fragment (written by the surface, read by the sun modulation)
var<private> terMeadowW: f32 = 0.0;

// tall grass shades itself when the sun is low (the meadow darkens and gains contrast)


// Travelling gust field in [0, 1] (VegNodes gustAt): noise of (worldXZ - windDir * t * speed),
// where the time-integrated offset comes from mat.gustOffset. Two fetches of the detail texture's
// fbm channel (~35 m and ~15 m gust cells).
fn terGustAt( xz: vec2f, offset: vec2f ) -> f32 {
	let p = xz - offset;
	let n = textureSampleLevel( terrainDetailTex, smpLinearRepeat, p / 140.0, 0.0 ).w * 0.62
		+ textureSampleLevel( terrainDetailTex, smpLinearRepeat, p / 61.0 + 0.37, 0.0 ).w * 0.38;
	return smoothstep( 0.46, 0.6, n );
}

// ---- wetness (swash zone) from the shore system, else a static damp band
fn terWetFoam( xz: vec2f, h: f32 ) -> vec2f { return terrainWetness(xz,h); }

fn terDetail( uv: vec2f ) -> vec4f {
	return textureSample( terrainDetailTex, smpAniso4Repeat, uv );
}

fn sourceSurface(in:FragInput,s:ptr<function,SourceSurface>){
	s.emissive = s.emissive * mat.emissiveIntensity;
	s.envIntensity = mat.envMapIntensity;

	let p = in.P;
	let xz = p.xz;
	let h = p.y;
	var outRough = 0.9;
	var outN = vec3f( 0.0, 1.0, 0.0 );
	var outAO = 1.0;
	var albedoOut = vec3f( 0.0 );

	// ---- data maps
	let nr = terrainNormalRock( xz );
	let N0 = normalize( vec3f( nr.x, sqrt( max( 1.0 - nr.x * nr.x - nr.y * nr.y, 0.0025 ) ), nr.y ) );
	let sp = terrainSplat( xz );
	let slope = 1.0 - N0.y;
	let camDist = length( frame.cameraPos - p );
	let dpx = dpdx( p ); let dpy = dpdy( p );
	let fwY = fwidth( h );

	// ---- deep seabed (seen through the water: no rock, land cover, swash or wind ripples),
	// the same colour / relief / AO as the full path below, from 6 detail samples instead of ~17
	// detail samples shared by both paths (sampled in uniform control flow: no derivative seams
	// where the paths meet)
	let macroA = terDetail( ( mat2x2f( 0.764842187, 0.644217687, -0.644217687, 0.764842187 ) * ( xz ) ) / 173.0 ).w;
	let macroB = terDetail( ( mat2x2f( -0.504846105, 0.863209367, -0.863209367, -0.504846105 ) * ( xz ) ) / 47.0 ).w;
	let mcr = macroA * 0.6 + macroB * 0.4;
	let dN = terDetail( xz / 1.9 );
	let dM = terDetail( ( mat2x2f( 0.267498829, 0.963558185, -0.963558185, 0.267498829 ) * ( xz ) ) / 6.7 + 0.21 );
	let dF = terDetail( ( mat2x2f( -0.737393716, 0.675463181, -0.675463181, -0.737393716 ) * ( xz ) ) / 0.63 + 0.53 );
	let grain = min( dN.z, 0.62 );
	let grainF = min( dF.z, 0.62 );
	let seabedPath = h < -0.8 && nr.z < 0.05 && slope < 0.18;
	let sw = vec2f( -0.11914522061843065, -0.9928768384869221 );
	let swDir = sw;
	let rc = vec2f( -78.000, 58.000 );
	// ripple phases of both paths (identical there), differentiated in uniform control flow
	let ph2 = dot( xz, swDir ) * ( 6.2832 / 0.75 ) + dM.w * 16.0 + macroB * 24.0;
	let fade2 = 1.0 - smoothstep( 0.5, 2.0, fwidth( ph2 ) );
	let rip2 = pow( sin( ph2 ) * 0.5 + 0.5, 1.4 );
	let ph3 = dot( xz, swDir ) * ( 6.2832 / 0.16 ) + dM.w * 26.0 + dN.w * 5.0;
	let fade3 = 1.0 - smoothstep( 0.6, 2.2, fwidth( ph3 ) );
	let rip3 = pow( sin( ph3 ) * 0.5 + 0.5, 1.5 );
	// bump height of either path: the surface-gradient bump runs after the branch (its screen-space
	// derivatives would be undefined in quads that straddle the two paths)
	var hdOut = 0.0;
	if ( seabedPath ) {

		let underW = 1.0;
		let sandW = smoothstep( 0.3, 0.72, sp.x + ( dM.z - 0.5 ) * 0.5 + ( mcr - 0.5 ) * 0.35 );
		// ---- seabed: sand with ripple fields, seagrass meadows, rubble heads
		let depth = -h;
		let reefD = length( xz - rc );
		let reefW = 1.0 - smoothstep( 29, 66.69999999999999, reefD + ( mcr - 0.5 ) * 30.0 );
		var under = mix( vec3f( 0.673859541, 0.570481595, 0.367246468 ), vec3f( 0.476999785, 0.447988412, 0.295699805 ), smoothstep( 1.0, 9.0, depth ) );
		under = under * ( ( dM.w - 0.5 ) * 0.14 + 1.0 ) * ( ( grain - 0.45 ) * 0.25 + 1.0 );
		// megaripple fields (~0.75 m) across the swell, troughs collect darker shell hash; not in
		// the swash zone or the first metre of depth
		let fieldW = smoothstep( 0.42, 0.62, macroB + ( dM.w - 0.5 ) * 0.35 ) * smoothstep( 0.9, 2.0, depth ) * ( 1.0 - reefW );
		under = under * ( ( rip2 - 0.55 ) * 0.22 * fieldW * fade2 + 1.0 );
		// small wave ripples (~0.16 m) everywhere below the swash
		// seagrass meadows: ragged edges, blade streaks leaning with the wave surge, epiphyte tips
		// (the fringe breaks up into clumps: noise at three scales thresholds the soft splat edge)
		let clumps = ( dM.w - 0.5 ) * 0.5 + ( dN.y - 0.45 ) * 0.4 + ( macroB - 0.5 ) * 0.3;
		let seagrassW = smoothstep( 0.3, 0.55, sp.z + clumps ) * underW * smoothstep( 0.3, 0.9, depth );
		let swPerp = vec2f( -swDir.y, swDir.x );
		let blades = terDetail( vec2f( dot( xz, swDir ) / 2.6, dot( xz, swPerp ) / 0.35 ) ).y;
		var meadow = mix( vec3f( 0.0134117489, 0.0219809471, 0.00598105957 ), vec3f( 0.0592535211, 0.0683848542, 0.0196066485 ), smoothstep( 0.35, 0.75, blades ) );
		meadow = mix( meadow, vec3f( 0.0469642007, 0.0331047666, 0.0116454309 ), smoothstep( 0.55, 0.8, dM.y + ( macroB - 0.5 ) * 0.4 ) * 0.5 );
		// sparse at the fringe: sand shows between the blades; thinner, paler patches inside
		meadow = mix( under, meadow, smoothstep( 0.3, 0.85, sp.z + clumps * 0.5 ) * 0.35 + 0.65 );
		meadow = mix( meadow, mix( meadow, under, 0.45 ), smoothstep( 0.58, 0.8, macroB + ( dM.w - 0.5 ) * 0.4 ) );
		under = mix( under, meadow, seagrassW );
		// rubble heads: coral rubble and rock turfed with algae, pink coralline crusts
		let rubbleW = smoothstep( 0.3, 0.6, sp.w + ( dN.x - 0.5 ) * 0.4 + ( dM.w - 0.5 ) * 0.3 ) * underW;
		var rubble = mix( vec3f( 0.0331047666, 0.0300741041, 0.0196066485 ), vec3f( 0.106539224, 0.0889815256, 0.0549717327 ), smoothstep( 0.3, 0.7, dN.x ) );
		rubble = mix( rubble, vec3f( 0.0331047666, 0.0469642007, 0.0100228256 ), smoothstep( 0.5, 0.7, dF.y ) * 0.6 );
		rubble = mix( rubble, vec3f( 0.295699805, 0.119280188, 0.162647194 ), smoothstep( 0.62, 0.74, dM.x ) * 0.6 );
		under = mix( under, rubble, rubbleW );
		// reef flat: coral rubble and pink crusts toward the reef
		under = mix( under, mix( vec3f( 0.273838416, 0.214041140, 0.162647194 ), vec3f( 0.318546778, 0.154872486, 0.178867504 ), smoothstep( 0.45, 0.7, dM.x ) ), reefW * 0.7 * smoothstep( 0.4, 0.6, dN.x ) );

		// damp sand below the berm (wet = 0 under water)
		let dampMottle = smoothstep( 0.3, 0.7, dM.w + ( dN.y - 0.45 ) * 0.6 + ( macroB - 0.5 ) * 0.4 );
		let wetK = smoothstep( 1.7, 0.5, h + dM.w * 0.3 ) * sandW * mix( 0.22, 0.5, dampMottle );
		let wetAlbedo = terrainSaturation( under * mat.wetDarken, 1.15 ) * vec3f( 0.97, 0.98, 1.0 );
		albedoOut = mix( under, wetAlbedo, wetK );
		outRough = 0.75;
		let waveR = rip3 * 0.012 * fade3 * smoothstep( -0.3, -0.9, h ) * ( 1.0 - seagrassW );
		let megaR = rip2 * 0.035 * fade2 * fieldW * ( 1.0 - seagrassW );
		let sandH = grain * 0.004 + grainF * 0.003 + waveR + megaR;
		let seabedH = seagrassW * ( blades * 0.05 + 0.08 ) + rubbleW * ( dN.x * 0.07 + dF.x * 0.015 );
		hdOut = sandH + seabedH;
		outAO = nr.w * ( 1.0 - seagrassW * 0.3 ) * ( 1.0 - rubbleW * smoothstep( 0.55, 0.2, dN.x ) * 0.35 );
		terMeadowW = 0.0;

	} else {

		// ---- detail samples (tileable heights: x rock, y soil, z sand, w fbm)
		// sand grain without the pebble peaks (pebbles are drawn separately where they belong)

		// ---- downslope streaks (rock flutes, hanging vegetation, landslide scars): the detail
		// texture stretched vertically on the two vertical projection planes
		let sw4 = pow( abs( N0.xz ), vec2f( 4.0 ) );
		let swN = sw4 / ( sw4.x + sw4.y + 1e-5 );
		let stA = terDetail( vec2f( p.z / 9.3, h / 37.0 ) );
		let stB = terDetail( vec2f( p.x / 9.3 + 0.5, h / 37.0 + 0.3 ) );
		let streak = stA.w * swN.x + stB.w * swN.y;
		let scar = stA.y * swN.x + stB.y * swN.y;

		// ---- rock: only evaluated where the rock mask or the slope allow it. Exposure follows the
		// form: steep faces, convex spurs and ridges (high AO) go bare, gully floors (low AO,
		// drainage lines) keep soil and plants; noise and fall-line streaks break the outline up.
		// Around the bare rock a band of scree / dark soil and moss; plants creep over it.
		let gully = sp.z * smoothstep( -0.5, 0.5, h );
		var rockAlbedo = vec3f( 0.2 ); var rockRough = 0.8; var rockHd = 0.0;
		var rockW = 0.0; var screeW = 0.0;
		if ( nr.z > 0.06 || slope > 0.3 ) {

			var g: RockGrad;
			g.dpdx = dpx; g.dpdy = dpy; g.fwY = fwY; g.useGrad = true;
			let R = terrainRockSurface( p, N0, h, mcr, 0.5, 1.0, g );
			let cliffK = smoothstep( 0.28, 0.55, slope );
			let convex = smoothstep( 0.5, 0.85, nr.w );
			let rv = nr.z * 0.7 + smoothstep( 0.3, 0.62, slope ) * 0.5 + convex * 0.14 - gully * 0.4
				+ ( R.height - 0.45 ) * 0.35 + ( dM.w - 0.5 ) * 0.34 + ( dN.w - 0.5 ) * 0.22
				+ ( streak - 0.5 ) * 1.0 * cliffK;
			// fades to 0 at the branch boundary: no step along the slope / mask iso-lines
			let branchK = max( smoothstep( 0.06, 0.18, nr.z ), smoothstep( 0.3, 0.42, slope ) );
			rockW = smoothstep( 0.5, 0.68, rv ) * branchK;
			screeW = smoothstep( 0.28, 0.52, rv ) * branchK * ( 1.0 - rockW );
			// weathered basalt: darker and browner than the sea-cliff palette, streaked; moss and
			// ferns on the ledges and on the less steep parts of the faces
			// dark wet stains down the fall line, paler dry ribs between them
			let stain = smoothstep( 0.5, 0.7, streak ) * cliffK;
			// dark, weathered basalt (the island's inland rock is darker and browner than the pale
			// sea-cliff palette), stained down the fall line
			var basalt = R.albedo * vec3f( 0.36, 0.34, 0.31 ) * ( 1.0 - stain * 0.45 ) * ( smoothstep( 0.42, 0.25, streak ) * cliffK * 0.15 + 1.0 );
			// soil and humus caught in the joints and hollows of the rock (low relief), so the face
			// reads as fractured stone instead of a smooth plate
			let joints = smoothstep( 0.42, 0.22, R.height + ( dN.w - 0.5 ) * 0.25 );
			basalt = mix( basalt, mix( vec3f( 0.0153252033, 0.0100228256, 0.00598105957 ), vec3f( 0.0331047666, 0.0331047666, 0.0100228256 ), dF.y ), joints * 0.7 );
			// moss / small plants on every ledge and on the less steep parts, more near the edges
			let ledgeMoss = smoothstep( 0.4, 0.75, N0.y + ( dN.y - 0.45 ) * 0.6 ) * smoothstep( 0.25, 0.55, macroB + dM.y * 0.4 );
			let fringe = smoothstep( 0.5, 0.58, rv ) * smoothstep( 0.8, 0.6, rv ) * smoothstep( 0.3, 0.6, dM.w + dN.y * 0.4 );
			let mossK = sat( max( ledgeMoss * 0.75, fringe * 0.8 ) + R.moss * 0.3 + joints * 0.25 );
			rockAlbedo = mix( basalt, mix( vec3f( 0.0134117489, 0.0245150137, 0.00393593950 ), vec3f( 0.0396819095, 0.0549717327, 0.00854038211 ), dF.y ), mossK );
			rockRough = R.rough;
			// craggier than the boulders: the big blocks and plates stand out from afar
			rockHd = R.hd * 2.2 + R.height * 0.6;

		}

		// ---- weights
		let notRock = 1.0 - rockW;
		let underW = smoothstep( 0.12, -0.6, h );
		let landW = 1.0 - underW;
		let sandW = smoothstep( 0.3, 0.72, sp.x + ( dM.z - 0.5 ) * 0.5 + ( mcr - 0.5 ) * 0.35 ) * notRock;
		let pathW = smoothstep( 0.28, 0.62, sp.y + ( dN.y - 0.45 ) * 0.4 + ( dM.w - 0.5 ) * 0.25 ) * notRock * landW
			* ( 1.0 - smoothstep( 50.0, 220.0, camDist ) * 0.85 );
		// forest on the higher / steeper ground and in the gullies, tall-grass meadow on the valley
		// floor and around the village (same classification as the vegetation's land cover)
		let jungleW = sat( smoothstep( 9.0, 24.0, h + ( mcr - 0.5 ) * 18.0 ) + smoothstep( 0.18, 0.36, slope ) + gully * 0.6 );
		// landslide scars: raw red-brown laterite in streaks down steep slopes, rare
		let lateriteW = smoothstep( 0.62, 0.74, scar + ( macroB - 0.5 ) * 0.3 ) * smoothstep( 0.3, 0.42, slope )
			* smoothstep( 0.52, 0.66, mcr ) * notRock * 0.85;

		// ---- beach sand: pale coral sand, drifts of warmer / coarser sand, grain
		let dryK = smoothstep( 0.8, 3.0, h );
		var sand = mix( vec3f( 0.655930683, 0.522521554, 0.318546778 ), vec3f( 0.787412289, 0.673859541, 0.476999785 ), smoothstep( 0.3, 0.72, mcr + dryK * 0.2 ) );
		sand = mix( sand, vec3f( 0.673859541, 0.476999785, 0.263273408 ), smoothstep( 0.55, 0.8, dM.w + ( macroB - 0.5 ) * 0.6 ) * 0.45 );
		sand = sand * ( ( dM.w - 0.5 ) * 0.16 + 1.0 ) * ( ( dN.w - 0.5 ) * 0.1 + 1.0 );
		sand = sand * ( ( grain - 0.45 ) * 0.3 + 0.97 ) * ( ( grainF - 0.45 ) * 0.2 + 1.0 );
		// disturbed / trodden patches: slightly darker, coarser sand (footfall, crabs, wind scour)
		let trod = smoothstep( 0.52, 0.7, dN.y + ( dM.y - 0.5 ) * 0.6 ) * dryK;
		sand = sand * ( 1.0 - trod * 0.07 );
		// the high-water band collects shell grit, coral bits and dried seaweed
		let hw = h + ( dM.w - 0.5 ) * 0.5;
		let wrackBand = smoothstep( 1.15, 1.4, hw ) * smoothstep( 2.1, 1.7, hw );
		let pebDensity = smoothstep( 0.62, 0.85, macroB + dM.w * 0.3 ) * 0.2 + wrackBand * smoothstep( 0.3, 0.6, dM.y );
		let pebW = smoothstep( 0.66, 0.8, dN.z ) * sat( pebDensity ) * smoothstep( 0.6, 0.9, sp.x ) * landW
			* ( 1.0 - smoothstep( 12.0, 35.0, camDist ) );
		let pebCol = mix( mix( vec3f( 0.710566490, 0.638283215, 0.507078510 ), vec3f( 0.570481595, 0.367246468, 0.318546778 ), smoothstep( 0.45, 0.75, dM.x ) ), vec3f( 0.106539224, 0.0889815256, 0.0732389559 ), smoothstep( 0.74, 0.82, dM.y ) );
		sand = mix( sand, pebCol, pebW * 0.75 );
		let wrack = wrackBand * smoothstep( 0.58, 0.72, dN.y ) * smoothstep( 0.4, 0.6, macroB );
		sand = mix( sand, vec3f( 0.0469642007, 0.0272117809, 0.0116454309 ), wrack * 0.8 );
		// trampled sand along the paths
		sand = sand * ( 1.0 - pathW * 0.07 );

		// wind ripples on the dry sand: crests across the wind (bent by the fbm), wavelength
		// ~10.5 cm, fading where trodden; visible in the albedo too (finer sand on the crests).
		// (A wavelength varying in space must not divide the absolute coordinate: the phase then
		// swings by x * dÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â½ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â» / ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â½ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â², which at 100 m from the origin turned the ripples into patches
		// of random direction and spacing with seams and moirÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©, and made fwidth switch them off
		// in blotches. The spacing varies through the smooth warp instead.)
		let wd = frame.windDir;
		let ph1 = dot( xz, wd ) * ( 6.2832 / 0.105 ) + dM.w * 24.0 + dN.w * 7.0 + macroB * 30.0;
		let fade1 = 1.0 - smoothstep( 0.6, 2.2, fwidth( ph1 ) );
		let rip1 = pow( sin( ph1 ) * 0.5 + 0.5, 1.6 );
		let windK = fade1 * smoothstep( 1.5, 2.2, h ) * ( 1.0 - pathW ) * ( 1.0 - trod * 0.7 )
			* smoothstep( 0.2, 0.5, macroB + dM.w * 0.3 );
		sand = sand * ( ( rip1 - 0.5 ) * 0.12 * windK + 1.0 );

		// ---- seabed: sand with ripple fields, seagrass meadows, rubble heads
		let depth = -h;
		let reefD = length( xz - rc );
		let reefW = 1.0 - smoothstep( 29, 66.69999999999999, reefD + ( mcr - 0.5 ) * 30.0 );
		var under = mix( vec3f( 0.673859541, 0.570481595, 0.367246468 ), vec3f( 0.476999785, 0.447988412, 0.295699805 ), smoothstep( 1.0, 9.0, depth ) );
		under = under * ( ( dM.w - 0.5 ) * 0.14 + 1.0 ) * ( ( grain - 0.45 ) * 0.25 + 1.0 );
		// megaripple fields (~0.75 m) across the swell, troughs collect darker shell hash; not in
		// the swash zone or the first metre of depth
		let fieldW = smoothstep( 0.42, 0.62, macroB + ( dM.w - 0.5 ) * 0.35 ) * smoothstep( 0.9, 2.0, depth ) * ( 1.0 - reefW );
		under = under * ( ( rip2 - 0.55 ) * 0.22 * fieldW * fade2 + 1.0 );
		// small wave ripples (~0.16 m) everywhere below the swash
		// seagrass meadows: ragged edges, blade streaks leaning with the wave surge, epiphyte tips
		// (the fringe breaks up into clumps: noise at three scales thresholds the soft splat edge)
		let clumps = ( dM.w - 0.5 ) * 0.5 + ( dN.y - 0.45 ) * 0.4 + ( macroB - 0.5 ) * 0.3;
		let seagrassW = smoothstep( 0.3, 0.55, sp.z + clumps ) * underW * smoothstep( 0.3, 0.9, depth );
		let swPerp = vec2f( -swDir.y, swDir.x );
		let blades = terDetail( vec2f( dot( xz, swDir ) / 2.6, dot( xz, swPerp ) / 0.35 ) ).y;
		var meadow = mix( vec3f( 0.0134117489, 0.0219809471, 0.00598105957 ), vec3f( 0.0592535211, 0.0683848542, 0.0196066485 ), smoothstep( 0.35, 0.75, blades ) );
		meadow = mix( meadow, vec3f( 0.0469642007, 0.0331047666, 0.0116454309 ), smoothstep( 0.55, 0.8, dM.y + ( macroB - 0.5 ) * 0.4 ) * 0.5 );
		// sparse at the fringe: sand shows between the blades; thinner, paler patches inside
		meadow = mix( under, meadow, smoothstep( 0.3, 0.85, sp.z + clumps * 0.5 ) * 0.35 + 0.65 );
		meadow = mix( meadow, mix( meadow, under, 0.45 ), smoothstep( 0.58, 0.8, macroB + ( dM.w - 0.5 ) * 0.4 ) );
		under = mix( under, meadow, seagrassW );
		// rubble heads: coral rubble and rock turfed with algae, pink coralline crusts
		let rubbleW = smoothstep( 0.3, 0.6, sp.w + ( dN.x - 0.5 ) * 0.4 + ( dM.w - 0.5 ) * 0.3 ) * underW;
		var rubble = mix( vec3f( 0.0331047666, 0.0300741041, 0.0196066485 ), vec3f( 0.106539224, 0.0889815256, 0.0549717327 ), smoothstep( 0.3, 0.7, dN.x ) );
		rubble = mix( rubble, vec3f( 0.0331047666, 0.0469642007, 0.0100228256 ), smoothstep( 0.5, 0.7, dF.y ) * 0.6 );
		rubble = mix( rubble, vec3f( 0.295699805, 0.119280188, 0.162647194 ), smoothstep( 0.62, 0.74, dM.x ) * 0.6 );
		under = mix( under, rubble, rubbleW );
		// reef flat: coral rubble and pink crusts toward the reef
		under = mix( under, mix( vec3f( 0.273838416, 0.214041140, 0.162647194 ), vec3f( 0.318546778, 0.154872486, 0.178867504 ), smoothstep( 0.45, 0.7, dM.x ) ), reefW * 0.7 * smoothstep( 0.4, 0.6, dN.x ) );
		sand = mix( sand, under, underW );

		// ---- ground: tall-grass meadow (tone shared with the grass field), forest floor and, from
		// afar, the forest canopy; laterite scars
		let V = normalize( frame.cameraPos - p );
		let NdV = sat( dot( N0, V ) );
		let mt = terrainMeadowTone( macroA, macroB, slope, N0.z, dM.w * 0.65 + dN.w * 0.35, true );
		// clumps (1-3 m) and tussocks, blade-scale grain
		let clump = dM.w * 0.6 + dN.y * 0.4;
		// seen from afar the tussocks and their shadowed gaps are what makes tall grass read as
		// grass (not lawn): the clump contrast grows with distance as the blades fade out
		let clumpK = mix( 0.34, 0.95, smoothstep( 40.0, 140.0, camDist ) );
		var lawn = mt.tone * ( ( clump - 0.5 ) * clumpK + 1.0 ) * ( ( dF.y - 0.4 ) * 0.22 + 1.0 );
		lawn = lawn * mix( 1.0, smoothstep( 0.25, 0.55, dN.y * 0.5 + dM.y * 0.5 ) * 0.35 + 0.72, smoothstep( 50.0, 160.0, camDist ) );
		// grass combed along the wind: long streaks (anisotropic sample of the fbm channel)
		// (comb and gust only where the lawn shows: not under full forest, sand or water)
		let lawnShows = jungleW < 1.0 && landW > 0.0 && sandW < 1.0;
		var comb = 0.5;
		if ( lawnShows ) {
			let combUV = vec2f( dot( xz, frame.windDir ) / 7.5, dot( xz, vec2f( -frame.windDir.y, frame.windDir.x ) ) / 0.9 );
			comb = terDetail( combUV + vec2f( 0.31, 0.77 ) ).w;
			lawn = lawn * ( ( comb - 0.5 ) * 0.3 + 1.0 );
		}
		// seen from above the dark soil shows between the clumps; at grazing angles blade sides
		// cover everything (lighter, more saturated)
		let gapK = smoothstep( 0.3, 0.95, NdV ) * smoothstep( 0.62, 0.3, clump );
		lawn = mix( lawn, MEADOW_soil, gapK * 0.45 );
		lawn = mix( lawn, terrainSaturation( lawn * 1.12, 1.15 ), smoothstep( 0.45, 0.1, NdV ) * 0.6 );
		// travelling gusts flatten the grass: the paler blade backs show as waves (same gust
		// field as the grass blades)
		let windStrength = max( frame.windSpeed * 0.1, 0.03 );
		if ( lawnShows ) {
			let gust = terGustAt( xz, mat.gustOffset ) * sat( windStrength * 0.5 );
			lawn = mix( lawn, lawn * vec3f( 1.25, 1.22, 1.06 ) + 0.01, gust * 0.6 );
		}
		// bare trodden soil in places, sandy soil toward the beach
		lawn = mix( lawn, vec3f( 0.132868322, 0.0889815256, 0.0432336359 ), smoothstep( 0.72, 0.84, dN.y + ( dM.y - 0.5 ) * 0.5 ) * 0.25 );
		lawn = mix( lawn, vec3f( 0.318546778, 0.233021999, 0.119280188 ), sat( sp.x * 1.6 ) * smoothstep( 0.45, 0.62, dN.z + dM.y * 0.3 ) * 0.7 );
		// inside the geometric grass field (GrassField) the ground is only seen between the blades:
		// the shaded base of the sward, dark and brownish with dead leaves; it hands over to the
		// sward's own look (above) where the blades thin out
		let grassHere = smoothstep( 2.5, 4.5, h ) * ( 1.0 - smoothstep( 0.45, 0.85, jungleW ) ) * ( 1.0 - sat( sp.x * 1.6 ) );
		let fieldK = ( 1.0 - smoothstep( 62.0, 87.0, length( p.xz - frame.cameraPos.xz ) ) ) * grassHere;
		let swardBase = mix( mt.tone * 0.4, MEADOW_soil, 0.4 ) * ( ( dN.y - 0.45 ) * 0.6 + 1.0 ) * ( ( dF.y - 0.4 ) * 0.3 + 1.0 );
		lawn = mix( lawn, swardBase, fieldK * 0.85 );
		let litter = mix( vec3f( 0.0331047666, 0.0196066485, 0.00854038211 ), vec3f( 0.0946296266, 0.0508760882, 0.0153252033 ), dF.y );
		var jungle = mix( vec3f( 0.0100228256, 0.0219809471, 0.00393593950 ), litter, smoothstep( 0.52, 0.7, dN.y ) );
		jungle = mix( jungle, vec3f( 0.0134117489, 0.0100228256, 0.00489631008 ), gully * 0.3 );
		let far = smoothstep( 40.0, 160.0, camDist );
		var cover = mix( vec3f( 0.00719440929, 0.0153252033, 0.00309597523 ), vec3f( 0.0245150137, 0.0469642007, 0.00598105957 ), smoothstep( 0.3, 0.7, mcr ) );
		cover = mix( cover, vec3f( 0.0592535211, 0.0683848542, 0.0134117489 ), smoothstep( 0.66, 0.84, macroB + dM.w * 0.2 ) * 0.45 );
		jungle = mix( jungle, cover, far * 0.8 );
		jungle = jungle * ( ( mcr - 0.5 ) * 0.3 + 1.0 );
		// canopy: seen from a distance (or on slopes too steep for the trees) the forest reads as
		// a carpet of lumpy crowns with dark gaps
		let canopyW = jungleW * max( smoothstep( 0.2, 0.4, slope ), smoothstep( 90.0, 260.0, camDist ) ) * smoothstep( 25.0, 70.0, camDist ) * notRock * ( 1.0 - screeW * 0.7 );
		var canopyH = 0.0;
		if ( canopyW > 0.0 ) {
			let crowns = terDetail( ( mat2x2f( 0.621609968, 0.783326910, -0.783326910, 0.621609968 ) * ( xz ) ) / 61.0 ).w;
			let crownsB = terDetail( ( mat2x2f( -0.666276021, 0.745705212, -0.745705212, -0.666276021 ) * ( xz ) ) / 13.0 + 0.37 ).w;
			canopyH = smoothstep( 0.32, 0.7, crowns * 0.45 + crownsB * 0.4 + dM.w * 0.15 );
			var canopy = mix( vec3f( 0.00393593950, 0.00719440929, 0.00193498452 ), mix( vec3f( 0.0173891024, 0.0363064841, 0.00489631008 ), vec3f( 0.0396819095, 0.0592535211, 0.00854038211 ), macroB ), canopyH );
			// steep faces: the canopy hangs in streaks down the fall line
			canopy = canopy * ( ( streak - 0.5 ) * 0.5 * smoothstep( 0.3, 0.5, slope ) + 1.0 );
			jungle = mix( jungle, canopy, canopyW );
		}
		var ground = mix( lawn, jungle, jungleW );
		// around the bare rock: dark humus, stones and moss, with the surrounding plants creeping
		// in (a soft, noisy band; no speckle)
		let creepIn = smoothstep( 0.4, 0.75, dM.w + ( dN.y - 0.45 ) * 0.5 + ( macroB - 0.5 ) * 0.3 );
		var scree = mix( vec3f( 0.0219809471, 0.0153252033, 0.0100228256 ), vec3f( 0.0592535211, 0.0469642007, 0.0331047666 ), smoothstep( 0.45, 0.75, dM.x + ( dN.x - 0.5 ) * 0.3 ) );
		scree = mix( scree, mix( vec3f( 0.0153252033, 0.0272117809, 0.00489631008 ), vec3f( 0.0396819095, 0.0549717327, 0.00854038211 ), dF.y ), smoothstep( 0.45, 0.7, dM.y + ( dN.y - 0.45 ) * 0.5 ) * 0.7 );
		ground = mix( ground, scree, screeW * ( 1.0 - creepIn * 0.7 ) );
		let laterite = mix( vec3f( 0.147318999, 0.0508760882, 0.0219809471 ), vec3f( 0.233021999, 0.106539224, 0.0469642007 ), dM.w ) * ( ( dN.y - 0.4 ) * 0.3 + 1.0 );
		ground = mix( ground, laterite, lateriteW );

		// ---- worn dirt paths / trampled ground (darker, redder soil in the forest)
		var dirt = mix( vec3f( 0.119280188, 0.0782883196, 0.0432336359 ), vec3f( 0.214041140, 0.154872486, 0.0835351274 ), dM.w ) * ( ( dN.y - 0.4 ) * 0.35 + 0.95 );
		dirt = mix( dirt, vec3f( 0.0732389559, 0.0396819095, 0.0196066485 ), jungleW * 0.7 );
		dirt = mix( dirt, vec3f( 0.273838416, 0.242866814, 0.195994179 ), smoothstep( 0.72, 0.82, dN.z ) * 0.5 );
		// grass creeping onto the trail, a grassy strip between the two worn ruts
		let creep = smoothstep( 0.45, 0.7, dN.y + ( dF.y - 0.45 ) * 0.5 ) * smoothstep( 0.9, 0.5, sp.y );
		dirt = mix( dirt, lawn, creep * 0.8 );

		// ---- combine
		let meadowW = ( 1.0 - jungleW ) * ( 1.0 - pathW ) * notRock * ( 1.0 - sandW ) * landW * ( 1.0 - screeW );
		terMeadowW = meadowW;
		var albedo = mix( ground, dirt, pathW );
		albedo = mix( albedo, sand, max( sandW, underW * notRock ) );
		albedo = mix( albedo, rockAlbedo, rockW );

		// ---- eroded beach scarp (splat alpha on land): storm-cut face of the foredune. Layered
		// sandy soil (laminae of paler and darker sand, darker humus-stained patches), live roots
		// and pale dead-root tangles hanging out of the face, damp darker sand at the toe.
		let scarpW = sat( sp.w * 1.6 ) * landW * notRock;
		var scarpH = 0.0;
		if ( scarpW > 0.003 ) {

			// laminae: thin storm layers, warped and wedging out, with a set of cross-beds at a low angle
			let tanF = normalize( vec2f( -N0.z, N0.x ) + vec2f( 1e-4, 0.0 ) );
			let alongF = dot( xz, tanF );
			let hw = h + ( dM.w - 0.5 ) * 0.5 + ( dN.w - 0.5 ) * 0.12 + ( macroB - 0.5 ) * 0.35;
			let lam = sin( hw * ( 6.2832 / 0.11 ) + dN.x * 2.0 ) * 0.5 + 0.5;
			let cross = sin( ( hw + alongF * 0.09 ) * ( 6.2832 / 0.075 ) ) * 0.5 + 0.5;
			let lamB = smoothstep( 0.3, 0.7, sin( ( hw + dM.y * 0.4 ) * ( 6.2832 / 0.43 ) ) * 0.5 + 0.5 );
			let layerMix = mix( lam, cross, smoothstep( 0.4, 0.6, dM.x ) );
			var soil = mix( vec3f( 0.214041140, 0.147318999, 0.0782883196 ), vec3f( 0.420033289, 0.318546778, 0.178867504 ), smoothstep( 0.3, 0.8, layerMix ) * 0.45 + lamB * 0.2 + ( grain - 0.45 ) * 0.5 + ( dM.w - 0.5 ) * 0.3 );
			// humus-stained, rootier soil in patches (the old dune surface caught in the cut)
			soil = mix( soil, vec3f( 0.0549717327, 0.0331047666, 0.0173891024 ), smoothstep( 0.55, 0.8, dM.y + ( macroB - 0.5 ) * 0.5 ) * 0.55 );
			// roots: horizontal coordinate along the face, stretched down the fall line
			let ru = terDetail( vec2f( dot( xz, tanF ) / 0.8, h / 0.5 ) + vec2f( 0.37, 0.11 ) );
			let rv = terDetail( vec2f( dot( xz, tanF ) / 1.7 + h * 0.6, h / 0.9 ) + vec2f( 0.71, 0.29 ) );
			let rootLive = smoothstep( 0.035, 0.0, abs( ru.x - 0.5 ) ) * smoothstep( 0.45, 0.62, ru.w );
			let rootDead = smoothstep( 0.03, 0.0, abs( rv.z - 0.52 ) ) * smoothstep( 0.5, 0.66, rv.y );
			soil = mix( soil, vec3f( 0.0245150137, 0.0134117489, 0.00719440929 ), rootLive * 0.85 );
			soil = mix( soil, vec3f( 0.603827339, 0.522521554, 0.393123130 ), rootDead * 0.75 );
			// damp, darker sand toward the toe and in seepage streaks
			let seep = smoothstep( 0.6, 0.8, terDetail( vec2f( dot( xz, tanF ) / 2.3, h / 6.0 ) ).w ) * 0.35;
			soil = soil * ( 1.0 - seep - smoothstep( 0.5, 1.0, sp.w ) * 0.08 );
			// under the lip: the dark, rooty topsoil the grass grows in, overhanging the cut
			let lip = smoothstep( 0.86, 0.97, sp.w + ( dN.y - 0.45 ) * 0.1 );
			soil = mix( soil, vec3f( 0.0331047666, 0.0196066485, 0.0100228256 ) * ( dN.x * 0.4 + 0.8 ), lip * 0.85 );
			// foot of the face: undercut by the swash of storm waves, in its own shadow
			soil *= 1.0 - ( 1.0 - smoothstep( 0.6, 0.78, sp.w ) ) * smoothstep( 0.35, 0.55, sp.w ) * 0.25;
			// slumped sand and fallen chunks at the toe: damp beach sand rather than soil
			soil = mix( vec3f( 0.354692061, 0.273838416, 0.162647194 ) * ( grain * 0.3 + 0.85 ), soil, smoothstep( 0.35, 0.75, sp.w ) );
			albedo = mix( albedo, soil, scarpW );
			scarpH = ( lip * 0.03 + layerMix * 0.012 + lamB * 0.018 + rootLive * 0.012 + rootDead * 0.009 + dN.x * 0.025 );

		}

		// ---- wetness (swash zone) from the shore system, else a static damp band
		let wetFoam = terWetFoam( xz, h );
		let wet = sat( wetFoam.x ) * ( 1.0 - jungleW * 0.8 ) * landW;
		// damp sand below the berm: darker in mottled, drying patches even when the swash has not
		// reached it lately
		let dampMottle = smoothstep( 0.3, 0.7, dM.w + ( dN.y - 0.45 ) * 0.6 + ( macroB - 0.5 ) * 0.4 );
		let damp = smoothstep( 1.7, 0.5, h + dM.w * 0.3 ) * sandW * mix( 0.22, 0.5, dampMottle );
		// sand dries in mottled patches; backwash leaves faint rills down the slope
		let mottle = smoothstep( 0.25, 0.75, dM.w + ( dN.y - 0.45 ) * 0.5 );
		let dryEdge = smoothstep( 0.0, 0.6, wet ) * smoothstep( 1.0, 0.6, wet );
		let wetK = max( wet, damp ) * ( 1.0 - dryEdge * mottle * 0.6 ) * notRock;
		let slopeDir = normalize( N0.xz + vec2f( 1e-4, 0.0 ) );
		let rillK = smoothstep( 0.2, 0.9, wet ) * smoothstep( 0.05, 0.6, h ) * sandW;
		var rill = 0.5;
		if ( rillK > 0.0 ) { rill = terDetail( vec2f( dot( xz, slopeDir ) / 3.2, dot( xz, vec2f( -slopeDir.y, slopeDir.x ) ) / 0.3 ) ).w; }
		let wetAlbedo = terrainSaturation( albedo * mat.wetDarken, 1.15 ) * vec3f( 0.97, 0.98, 1.0 ) * ( ( rill - 0.5 ) * 0.25 * rillK + 1.0 );
		albedo = mix( albedo, wetAlbedo, wetK );
		// swash marks: thin wavy lines of grit left at the limits of earlier uprushes
		let sl = ( h + dM.w * 0.18 + dN.w * 0.05 ) / 0.13;
		let slD = abs( fract( sl ) - 0.5 );
		let slW = fwidth( sl ) + 1e-4;
		let swashLine = smoothstep( slW * 1.5 + 0.04, 0.0, slD ) * smoothstep( 0.2, 0.4, h ) * smoothstep( 1.6, 1.2, h )
			* smoothstep( 0.45, 0.65, dM.y + ( macroB - 0.5 ) * 0.4 ) * sandW * ( 1.0 - smoothstep( 0.8, 2.0, slW * 10.0 ) );
		albedo = mix( albedo * ( 1.0 - swashLine * 0.3 ), vec3f( 0.787412289, 0.748414103, 0.673859541 ), swashLine * smoothstep( 0.6, 0.75, dF.z ) * 0.5 );
		// foam residue: lacy patterns stranded on the sand
		var residue = sat( wetFoam.y ) * landW * notRock;
		if ( residue > 0.0 ) { residue *= smoothstep( 0.42, 0.18, terDetail( ( mat2x2f( 0.921060994, 0.389418342, -0.389418342, 0.921060994 ) * ( xz ) ) / 0.9 ).x ) * 0.7 + 0.3; }
		albedo = mix( albedo, vec3f( 0.748414103, 0.787412289, 0.787412289 ), residue );

		// ---- roughness
		var rough = mix( 0.88, 0.93, sandW );
		rough = mix( rough, 0.9, pathW );
		rough = mix( rough, rockRough, rockW );
		// wet sand has a film of water: glossy while fresh, satin as it drains
		rough = mix( rough, mix( 0.42, 0.16, wet ), wetK );
		rough = mix( rough, 0.7, residue );
		rough = mix( rough, 0.75, underW );
		rough = mix( rough, 0.94, scarpW );
		outRough = rough;

		// ---- micro relief for the normal
		let windR = rip1 * 0.005 * windK;
		let waveR = rip3 * 0.012 * fade3 * smoothstep( -0.3, -0.9, h ) * ( 1.0 - seagrassW );
		let megaR = rip2 * 0.035 * fade2 * fieldW * ( 1.0 - seagrassW );
		let sandH = ( grain * 0.004 + grainF * 0.003 + pebW * 0.004 + windR + waveR + megaR ) * ( 1.0 - wet * 0.6 )
			+ rill * 0.006 * rillK;
		// seagrass canopy stands proud of the sand with a ragged scarp; rubble is knobbly
		let seabedH = seagrassW * ( blades * 0.05 + 0.08 ) + rubbleW * ( dN.x * 0.07 + dF.x * 0.015 );
		let groundH = dN.y * mix( 0.05, 0.035, jungleW ) + dF.y * 0.012 + dM.y * 0.045 + canopyH * canopyW * 2.5
			+ clump * 0.12 * ( 1.0 - jungleW ) + comb * 0.03 * ( 1.0 - jungleW );
		let screeH = dN.z * 0.04 + dM.x * 0.06;
		let dirtH = dN.z * 0.012 + dN.y * 0.01;
		var hd = mix( mix( groundH, screeH, screeW ), dirtH, pathW );
		hd = mix( hd, sandH + seabedH, max( sandW, underW * notRock ) );
		hd = mix( hd, rockHd, rockW );
		hd = mix( hd, scarpH, scarpW );
		hdOut = hd;

		// ---- ambient occlusion: baked horizon + cavity, plus litter / crevices / seagrass canopy
		let aoDetail = mix( 1.0, dN.y * 0.5 + 0.7, jungleW * ( 1.0 - sandW ) * notRock * landW )
			* mix( 1.0, smoothstep( 0.2, 0.7, clump ) * 0.35 + 0.65, meadowW );
		outAO = nr.w * aoDetail * ( 1.0 - seagrassW * 0.3 ) * ( 1.0 - rubbleW * smoothstep( 0.55, 0.2, dN.x ) * 0.35 )
			* mix( 1.0, smoothstep( 0.15, 0.6, canopyH ) * 0.6 + 0.4, canopyW );

		albedoOut = albedo;

	}

	outN = terrainPerturbNormal( p, N0, hdOut, 1.0 );

	s.albedo = albedoOut;
	s.roughness = outRough;
	s.normal = outN;
	s.ao = sat( outAO );

}
@fragment
fn fragment(vs:VertexOutput)->@location(0) vec4f {
    if settings.flags.z > 0.5 { return vec4f(1.0); }
    frame.cameraPos=view.world_position;
    frame.time=settings.wind.w; frame.windDir=settings.wind.xy;
    frame.windSpeed=settings.wind.z; frame.sunDir=vec3f(-0.886125497221313,0.45498452073767237,0.08815151197450805);
    terrainParams.origin=settings.domain.x; terrainParams.size=settings.domain.y;
    terrainParams.res=settings.domain.z;
    mat.wetDarken=settings.domain.w; mat.gustOffset=vec2f(0.0);
    mat.emissiveIntensity=1.0; mat.envMapIntensity=1.0;
    var input:FragInput;
    input.P=vs.world_position.xyz; input.N=normalize(vs.world_normal);
    input.V=normalize(view.world_position-input.P); input.pixel=vs.position.xy;
    var surface:SourceSurface;
    surface.ao=1.0; surface.roughness=0.9;
    sourceSurface(input,&surface);
    if settings.flags.x > 0.5 { return vec4f(surface.albedo,1.0); }
    return vec4f(sourceSceneLight(LightingSurface(surface.albedo,surface.normal,
        surface.roughness,surface.metalness,1.0,vec3f(0.0),surface.ao,
        surface.envIntensity,surface.emissive),input.V,input.P,input.pixel),1.0);
}

struct LightingSurface {
 albedo:vec3f,normal:vec3f,roughness:f32,metalness:f32,specularIntensity:f32,
 translucency:vec3f,ao:f32,envIntensity:f32,emissive:vec3f,
};
fn F_Schlick( f0: vec3f, f90: f32, dotVH: f32 ) -> vec3f {
	let fresnel = exp2( ( -5.55473 * dotVH - 6.98316 ) * dotVH );
	return f0 * ( 1.0 - fresnel ) + f90 * fresnel;
}
fn V_GGX_SmithCorrelated( alpha: f32, dotNL: f32, dotNV: f32 ) -> f32 {
	let a2 = alpha * alpha;
	let gv = dotNL * sqrt( a2 + ( 1.0 - a2 ) * dotNV * dotNV );
	let gl = dotNV * sqrt( a2 + ( 1.0 - a2 ) * dotNL * dotNL );
	return 0.5 / max( gv + gl, EPS );
}
fn D_GGX( alpha: f32, dotNH: f32 ) -> f32 {
	let a2 = alpha * alpha;
	let d = dotNH * dotNH * ( a2 - 1.0 ) + 1.0;
	return INV_PI * a2 / ( d * d );
}
fn BRDF_GGX( L: vec3f, V: vec3f, N: vec3f, f0: vec3f, f90: f32, roughness: f32 ) -> vec3f {
	let alpha = roughness * roughness;
	let H = normalize( L + V );
	let dotNL = sat( dot( N, L ) ); let dotNV = sat( dot( N, V ) );
	let dotNH = sat( dot( N, H ) ); let dotVH = sat( dot( V, H ) );
	return F_Schlick( f0, f90, dotVH ) * V_GGX_SmithCorrelated( alpha, dotNL, dotNV ) * D_GGX( alpha, dotNH );
}

// Source direct light only. Neutral visibility; environment/occlusion excluded.
fn sourceDirectLight(albedo:vec3f,roughness:f32,metalness:f32,specular:f32,
    transmission:vec3f,N:vec3f,V:vec3f,L:vec3f,lightColor:vec3f)->vec3f {
    let rough=clamp(roughness,0.03,1.0);
    let diffuseColor=albedo*(1.0-metalness);
    let specF0=mix(vec3f(0.04)*specular,albedo,metalness);
    let specF90=mix(specular,1.0,metalness);
    let irradiance=sat(dot(N,L))*lightColor;
    return irradiance*diffuseColor*INV_PI
        +irradiance*BRDF_GGX(L,V,N,specF0,specF90,rough)
        +transmission*lightColor;
}

const envSH=array<vec3f,9>(vec3f(0.37257152795791626,0.5714865326881409,0.9003067016601562),vec3f(0.15536543726921082,0.2394588440656662,0.40820568799972534),vec3f(0.026822328567504883,0.027735169976949692,0.019546907395124435),vec3f(-0.07973331212997437,-0.08053719997406006,-0.07878603041172028),vec3f(-0.028101500123739243,-0.02952546998858452,-0.029926728457212448),vec3f(0.014922921545803547,0.015481891110539436,0.009953215718269348),vec3f(-0.005529914982616901,0.0020130593329668045,0.009609866887331009),vec3f(-0.007252268493175507,-0.007864859886467457,-0.00771138072013855),vec3f(0.04689784348011017,0.06606932729482651,0.08220835775136948));
fn hookEnvDiffuse( N: vec3f ) -> vec3f {
	let d = N;
	var e = envSH[ 0 ].rgb * 0.282095
		+ envSH[ 1 ].rgb * 0.488603 * d.y + envSH[ 2 ].rgb * 0.488603 * d.z + envSH[ 3 ].rgb * 0.488603 * d.x
		+ envSH[ 4 ].rgb * 1.092548 * d.x * d.y + envSH[ 5 ].rgb * 1.092548 * d.y * d.z
		+ envSH[ 6 ].rgb * 0.315392 * ( 3.0 * d.z * d.z - 1.0 ) + envSH[ 7 ].rgb * 1.092548 * d.x * d.z
		+ envSH[ 8 ].rgb * 0.546274 * ( d.x * d.x - d.y * d.y );
	return max( e, vec3f( 0.0 ) ) * 1.0;
}
fn DFGApprox( dotNV: f32, roughness: f32 ) -> vec2f {
	let c0 = vec4f( -1.0, -0.0275, -0.572, 0.022 );
	let c1 = vec4f( 1.0, 0.0425, 1.04, -0.04 );
	let r = roughness * c0 + c1;
	let a004 = min( r.x * r.x, exp2( -9.28 * dotNV ) ) * r.x + r.y;
	return vec2f( -1.04, 1.04 ) * a004 + r.zw;
}
// multi-scattering specular energy compensation (Fdez-Aguera), as three's computeMultiscattering
fn multiscatter( N: vec3f, V: vec3f, specColor: vec3f, specF90: f32, roughness: f32, single: ptr<function, vec3f>, multi: ptr<function, vec3f> ) {
	let fab = DFGApprox( sat( dot( N, V ) ), roughness );
	let Fr = specColor;
	let FssEss = Fr * fab.x + specF90 * fab.y;
	let Ess = fab.x + fab.y;
	let Ems = 1.0 - Ess;
	let Favg = Fr + ( 1.0 - Fr ) * 0.047619;
	let Fms = FssEss * Favg / ( 1.0 - Ems * Favg );
	*single += FssEss;
	*multi += Fms * Ems;
}
@group(#{MATERIAL_BIND_GROUP}) @binding(10) var envCube:texture_cube<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(11) var envSampler:sampler;
fn sourceEnvironmentLight(s:LightingSurface,N:vec3f,V:vec3f)->vec3f {
 let rough=clamp(s.roughness,0.03,1.0);
 let diffuseColor=s.albedo*(1.0-s.metalness);
 let specF0=mix(vec3f(0.04)*s.specularIntensity,s.albedo,s.metalness);
 let specF90=mix(s.specularIntensity,1.0,s.metalness);
 let R=reflect(-V,N);
 let Rr=normalize(mix(R,N,rough*rough));
 let envIrr=hookEnvDiffuse(N)*PI*s.envIntensity;
 let radiance=textureSampleLevel(envCube,envSampler,Rr,rough*5.0).rgb*s.envIntensity;
 var single=vec3f(0.0);var multi=vec3f(0.0);
 multiscatter(N,V,specF0,specF90,rough,&single,&multi);
 let totalScatter=single+multi;
 let diffuseMS=diffuseColor*(1.0-max(max(totalScatter.r,totalScatter.g),totalScatter.b));
 let dotNV=sat(dot(N,V));
 let specAO=sat(pow(dotNV+s.ao,exp2(-16.0*rough-1.0))-1.0+s.ao);
 return diffuseMS*envIrr*INV_PI*s.ao+(radiance*single+multi*envIrr*INV_PI)*specAO;
}

fn sourceSceneLight(s:LightingSurface,V:vec3f,P:vec3f,pixel:vec2f)->vec3f {
 let N=normalize(s.normal);
 return sourceDirectLight(s.albedo,s.roughness,s.metalness,s.specularIntensity,
     s.translucency,N,V,vec3f(-0.886125497221313,0.45498452073767237,0.08815151197450805),vec3f(9.635385513305664,8.087041854858398,6.099357545375824)*terrainSunShadowAt(P)*cloudsShadow(P.xz)*sunShadow(P,N,pixel))
     +sourceEnvironmentLight(s,N,V)+s.emissive;
}

@group(#{MATERIAL_BIND_GROUP}) @binding(12) var terrainSunShadowTex:texture_2d<f32>;
fn terrainSunShadowAt( P: vec3f ) -> f32 {
	let N = f32( textureDimensions( terrainSunShadowTex ).x );
	let st = clamp( ((P.xz+1024.0)/2048.0) * N - 0.5, vec2f( 0.0 ), vec2f( N - 1.001 ) );
	let i = vec2i( floor( st ) );
	let t = fract( st );
	let s = mix(
		mix( textureLoad( terrainSunShadowTex, i, 0 ), textureLoad( terrainSunShadowTex, i + vec2i( 1, 0 ), 0 ), t.x ),
		mix( textureLoad( terrainSunShadowTex, i + vec2i( 0, 1 ), 0 ), textureLoad( terrainSunShadowTex, i + vec2i( 1, 1 ), 0 ), t.x ),
		t.y
	);
	let w = s.y * 0.012 + 0.35;
	return mix( 1.0, smoothstep( -w, w, P.y - s.x ), 1.0 );
}


struct ObjectShadowSettings {
    matrices:array<mat4x4f,4>, cascades:array<vec4f,4>, blend:array<vec4f,4>,
    count:u32, mapSize:f32, bias:f32, fade:f32,
    enabled:f32, pcssCascades:u32, sunAngularDiameter:f32, frameIndex:u32,
    camera:vec4f, forward:vec4f,
};
@group(#{MATERIAL_BIND_GROUP}) @binding(15) var sunShadowMap:texture_depth_2d_array;
@group(#{MATERIAL_BIND_GROUP}) @binding(16) var smpShadow:sampler_comparison;
@group(#{MATERIAL_BIND_GROUP}) @binding(17) var<uniform> shadowParams:ObjectShadowSettings;
fn interleavedGradientNoise( px: vec2f ) -> f32 { return fract( 52.9829189 * fract( dot( px, vec2f( 0.06711056, 0.00583715 ) ) ) ); }

// i-th of n points of a Vogel disc (unit radius), rotated by phi
fn vogelDiskSample( i: i32, n: i32, phi: f32 ) -> vec2f {
	let r = sqrt( ( f32( i ) + 0.5 ) / f32( n ) );
	let theta = f32( i ) * 2.399963229728653 + phi;
	return vec2f( cos( theta ), sin( theta ) ) * r;
}


fn shadowCascadeOf( viewDist: f32 ) -> i32 {
	for ( var i = 0; i < i32( shadowParams.count ); i++ ) { if ( viewDist < shadowParams.cascades[ i ].x ) { return i; } }
	return -1;
}

fn _shadowTap( uv: vec2f, layer: i32, z: f32 ) -> f32 {
	return textureSampleCompareLevel( sunShadowMap, smpShadow, uv, layer, z );
}

// shadow map depth (0 near .. 1 far, standard Z) at uv
fn _shadowDepth( uv: vec2f, layer: i32 ) -> f32 {
	let dim = vec2f( textureDimensions( sunShadowMap ) );
	let px = vec2i( clamp( uv * dim, vec2f( 0.0 ), dim - 1.0 ) );
	return textureLoad( sunShadowMap, px, layer, 0 );
}

// Contact-hardening sun shadows (PCSS, the former SunShadowFilter) on the near cascades. The penumbra of a
// real sun shadow grows with the distance from the occluder to the receiver (the sun is a 0.53 deg disc):
// sharp where an object touches the ground, soft under a palm crown 10 m up. Per pixel:
//  1. blocker search: average depth of the occluders around the pixel (raw depth loads, no sampler)
//  2. penumbra width = occluder-receiver distance * sun diameter, converted to this cascade's texels
//  3. percentage-closer filtering over that width
// Both sample sets are Vogel disks rotated per pixel and per frame (interleaved gradient noise): the
// TAA resolves the noise into a smooth gradient. Farther cascades: three's PCFShadowFilter (5 Vogel taps
// of hardware comparisons over one texel, rotated per pixel).
const SHADOW_MAX_OCCLUDER_HEIGHT: f32 = 30.0; // m: search radius covers penumbrae of occluders up to this far above
const SHADOW_SEARCH_TAPS: i32 = 8;
const SHADOW_FILTER_TAPS: i32 = 12;

fn sunShadowCascade( P: vec3f, N: vec3f, c: i32, noise: f32, pcfNoise: f32 ) -> f32 {
	return _sunShadowCascade( P, N, c, noise, pcfNoise, true );
}

// pcss = false: the 5-tap PCF filter in every cascade (no blocker search)
fn _sunShadowCascade( P: vec3f, N: vec3f, c: i32, noise: f32, pcfNoise: f32, pcss: bool ) -> f32 {
	let info = shadowParams.cascades[ c ];
	let Pb = P + N * info.z;
	let sc = shadowParams.matrices[ c ] * vec4f( Pb, 1.0 );
	let uvz = vec3f( sc.x * 0.5 + 0.5, 0.5 - sc.y * 0.5, sc.z );
	if ( any( uvz.xy < vec2f( 0.0 ) ) || any( uvz.xy > vec2f( 1.0 ) ) || uvz.z > 1.0 ) { return 1.0; }
	let z = uvz.z - shadowParams.bias;
	let texel = 1.0 / shadowParams.mapSize;
	if ( pcss && u32( c ) < shadowParams.pcssCascades ) {
		// moved every frame (Jimenez 2014): a noise pattern fixed on screen would never average out
		let phi = noise * TWO_PI;
		let width = info.y * shadowParams.mapSize; // cascade width (m)
		let range = info.w; // depth range (m)
		let SD = shadowParams.sunAngularDiameter;
		// 1. blockers within the widest penumbra this cascade can show. The texel straight along the light
		// ray comes first: a thin occluder (a log, a rope, a rail) can fall between the disk taps, which
		// left lit dots inside its umbra.
		let searchUV = max( min( SHADOW_MAX_OCCLUDER_HEIGHT * SD / width, texel * 24.0 ), texel * 1.5 );
		let d0 = _shadowDepth( uvz.xy, c );
		var blockSum = select( 0.0, d0, d0 < z );
		var blockCount = select( 0.0, 1.0, d0 < z );
		for ( var i = 0; i < SHADOW_SEARCH_TAPS; i++ ) {
			let d = _shadowDepth( uvz.xy + vogelDiskSample( i, SHADOW_SEARCH_TAPS, phi ) * searchUV, c );
			// standard depth: an occluder is closer to the light = smaller depth
			if ( d < z ) { blockSum += d; blockCount += 1.0; }
		}
		if ( blockCount < 0.5 ) { return 1.0; }
		// 2. occluder-receiver distance (orthographic: depth is linear over the camera range)
		let dz = abs( blockSum / blockCount - z ) * range;
		let penumbraUV = clamp( dz * SD / width, texel * 1.2, texel * 32.0 );
		// 3. PCF over the penumbra
		var sum = 0.0;
		for ( var i = 0; i < SHADOW_FILTER_TAPS; i++ ) {
			let d = _shadowDepth( uvz.xy + vogelDiskSample( i, SHADOW_FILTER_TAPS, phi + 1.7 ) * penumbraUV, c );
			sum += select( 0.0, 1.0, z <= d );
		}
		return sum / f32( SHADOW_FILTER_TAPS );
	}
	// three's PCFShadowFilter: 5 samples on a Vogel disk of one texel, rotated per pixel
	let phiP = pcfNoise * TWO_PI;
	var sum = 0.0;
	for ( var i = 0; i < 5; i++ ) {
		sum += _shadowTap( uvz.xy + vogelDiskSample( i, 5, phiP ) * texel, c, z );
	}
	return sum / 5.0;
}

// visibility of the sun at P (1 = lit); pixel = fragment coordinate for the dither.
// Cascade seams blended over a band that grows with their distance (SoftCSMShadowNode: a quarter of it,
// 2.5 m at the 10 m seam, 15 m at 60 m, and the last cascade fades out over its final 100 m); each
// cascade's map is widened to cover its part of the overlap.
fn sunShadow( P: vec3f, N: vec3f, pixel: vec2f ) -> f32 {
	return _sunShadow( P, N, pixel, true );
}

// sunShadow with the plain 5-tap PCF filter everywhere (surfaces whose own detail hides penumbrae: water)
fn sunShadowPCF( P: vec3f, N: vec3f, pixel: vec2f ) -> f32 {
	return _sunShadow( P, N, pixel, false );
}

fn _sunShadow( P: vec3f, N: vec3f, pixel: vec2f, pcss: bool ) -> f32 {
	if ( shadowParams.enabled < 0.5 ) { return 1.0; }
	let dist = dot( P - shadowParams.camera.xyz, shadowParams.forward.xyz );
	let noise = interleavedGradientNoise( pixel + f32( shadowParams.frameIndex % 64u ) * 5.588238 );
	let pcfNoise = interleavedGradientNoise( pixel );
	if ( shadowParams.fade < 0.5 ) {
		let c = shadowCascadeOf( dist );
		if ( c < 0 ) { return 1.0; }
		return _sunShadowCascade( P, N, c, noise, pcfNoise, pcss );
	}
	var ret = 1.0;
	let last = i32( shadowParams.count ) - 1;
	for ( var i = 0; i <= last; i++ ) {
		let b = shadowParams.blend[ i ]; // x, y: cascade range, z / w: blend margin at its near / far seam
		let center = ( b.x + b.y ) * 0.5;
		let margin = max( select( b.w, b.z, dist < center ), 1e-5 );
		let csmX = b.x - margin * 0.5;
		let csmY = select( b.y + margin * 0.5, b.y, i == last );
		if ( dist >= csmX && dist <= csmY ) {
			var ratio = clamp( min( dist - csmX, csmY - dist ) / margin, 0.0, 1.0 );
			// no fade at the near edge of the first cascade
			if ( i == 0 && dist <= center ) { ratio = 1.0; }
			ret -= ( 1.0 - _sunShadowCascade( P, N, i, noise, pcfNoise, pcss ) ) * ratio;
		}
	}
	return max( ret, 0.0 );
}

// one depth comparison in cascade c (1 = lit; outside the map: lit). For volumetric marches (haze shafts,
// motes) where the jitter and the temporal resolve do the filtering.
fn sunShadowCascadeHard( P: vec3f, c: i32 ) -> f32 {
	let sc = shadowParams.matrices[ c ] * vec4f( P, 1.0 );
	let uv = vec2f( sc.x * 0.5 + 0.5, 0.5 - sc.y * 0.5 );
	if ( any( uv <= vec2f( 0.0 ) ) || any( uv >= vec2f( 1.0 ) ) || sc.z > 1.0 ) { return 1.0; }
	return select( 0.0, 1.0, sc.z - 2e-5 <= _shadowDepth( uv, c ) );
}
// same in the cascade covering P (by view distance), 1 beyond the last one or with shadows off
fn sunShadowHard( P: vec3f ) -> f32 {
	if ( shadowParams.enabled < 0.5 ) { return 1.0; }
	let dist = dot( P - shadowParams.camera.xyz, shadowParams.forward.xyz );
	let c = shadowCascadeOf( dist );
	if ( c < 0 ) { return 1.0; }
	return sunShadowCascadeHard( P, c );
}
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

@group(#{MATERIAL_BIND_GROUP}) @binding(18) var shoreSimStateTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(19) var shoreSimLaceNearest:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(20) var<uniform> shoreSimP:ShoreSimParams;

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
fn terrainWetness( xz: vec2f, h: f32 ) -> vec2f {
	let s = shoreSimSample( xz );
	let inside = shoreSimInside( shoreSimUvOf( xz ) );
	// outside the simulated region fall back to a static damp band
	let band = smoothstep( 0.45, 0.0, h );
	// foam left on the sand: the lace the water carried, stranded and popping (ShoreSim.sandFoam)
	return vec2f( max( s.y, band * ( 1.0 - inside ) ), shoreSimSandFoam( xz, s, h ) );
}
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
