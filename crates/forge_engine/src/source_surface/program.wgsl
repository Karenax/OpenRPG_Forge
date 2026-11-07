// SPDX-License-Identifier: MIT
// Source surface program; original bytes follow this notice.
// Tidewater 1438b1abfcaee3267092b75573014f4d9b4a983c; copyright 2026 DRG Software Solutions LLC.
// See third_party/tidewater/LICENSE.
// Development adapter; source vertex/surface expressions are inserted unchanged.
#import bevy_pbr::{mesh_functions, mesh_view_bindings::view,
    view_transformations::position_world_to_clip, pbr_types,
    pbr_functions::{apply_pbr_lighting,main_pass_post_lighting_processing}}
struct Settings { wind:vec4f,color:vec4f,emissive:vec4f,surface:vec4f,flags:vec4f };
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> settings:Settings;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var smpAnisoRepeat:sampler;
struct SourceFrame { time:f32,windDir:vec2f,windSpeed:f32,night:f32 };
var<private> frame:SourceFrame;
var<private> mat:SourceMaterialParams;
struct Input {
    @builtin(instance_index) instance:u32,
    @location(0) position:vec3f, @location(1) normal:vec3f,
    @location(2) uv:vec2f, @location(3) tint:vec3f, @location(4) vdata:vec4f,
};
struct Output {
    @builtin(position) position:vec4f,
    @location(0) P:vec3f, @location(1) normal:vec3f, @location(2) uv:vec2f,
    @location(3) vTint:vec3f, @location(4) vData:vec4f,
};
struct SourceVertex { position:vec3f,normal:vec3f,uv:vec2f,tint:vec3f,vdata:vec4f };
struct FragInput { vs:Output,P:vec3f,N:vec3f,V:vec3f,uv:vec2f,pixel:vec2f,front:bool };
struct Surface { albedo:vec3f,alpha:f32,normal:vec3f,roughness:f32,metalness:f32,
    emissive:vec3f,ao:f32,envIntensity:f32 };
fn setupSource() {
    frame.time=settings.wind.w; frame.windDir=settings.wind.xy; frame.windSpeed=settings.wind.z;
    frame.night=settings.flags.y;
    mat.color=settings.color.xyz; mat.opacity=settings.color.w;
    mat.emissive=settings.emissive.xyz; mat.emissiveIntensity=settings.emissive.w;
    mat.roughness=settings.surface.x; mat.metalness=settings.surface.y;
    mat.alphaTest=settings.surface.z; mat.envMapIntensity=settings.surface.w;
}
struct SourceMaterialParams {
	color: vec3f,
	opacity: f32,
	emissive: vec3f,
	roughness: f32,
	metalness: f32,
	alphaTest: f32,
	envMapIntensity: f32,
	emissiveIntensity: f32,
};

#ifdef VILLAGE_KIND_0
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var vlgWoodA:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var vlgWoodN:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var vlgPaintN:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var vlgGrime:texture_2d<f32>;

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

fn perturbNormalByMap( P: vec3f, N: vec3f, uv: vec2f, mapN: vec3f ) -> vec3f {
	let dp1 = dpdx( P ); let dp2 = dpdy( P );
	let duv1 = dpdx( uv ); let duv2 = dpdy( uv );
	let dp2perp = cross( dp2, N ); let dp1perp = cross( N, dp1 );
	let T = dp2perp * duv1.x + dp1perp * duv2.x;
	let B = dp2perp * duv1.y + dp1perp * duv2.y;
	let invmax = inverseSqrt( max( max( dot( T, T ), dot( B, B ) ), 1e-20 ) );
	return normalize( mat3x3f( T * invmax, B * invmax, N ) * mapN );
}

const VLM_TAU: f32 = 6.283185307179586;

fn vlmHash21( x: f32, y: f32 ) -> f32 { return fract( sin( x * 12.9898 + y * 78.233 ) * 43758.5453 ); }
fn vlmN01( n: f32 ) -> f32 { return n * 0.5 + 0.5; }

// baked normal (RG = xy of a unit normal) -> surface slope ( -dh/du, -dh/dv )
fn vlmSlopeOf( t: vec4f ) -> vec2f {
	let xy = t.xy * 2.0 - 1.0;
	return xy / sqrt( max( 1.0 - dot( xy, xy ), 0.04 ) );
}

fn vlmBand01( p: f32, k: f32 ) -> f32 { return step( k - 0.5, p ) * step( p, k + 0.5 ); }

// three normalMap( packN( vec3( s, 1 ) ) ): tangent frame from the mesh uv derivatives
fn vlmNormalFromSlope( P: vec3f, N: vec3f, uv: vec2f, s: vec2f ) -> vec3f {
	return perturbNormalByMap( P, N, uv, normalize( vec3f( s, 1.0 ) ) );
}



struct VlmTidal { col: vec3f, rough: f32, wet: f32 };

// Tidal zone: algae / barnacles / wet darkening driven by world height above sea level.
fn vlmTidal( pw: vec3f, col: vec3f, rough: f32 ) -> VlmTidal {
	let q = vec2f( pw.x + pw.z * 0.7, pw.y );
	let Gt = textureSample( vlgGrime, smpAnisoRepeat, q * vec2f( 0.6, 1.2 ) );
	let y = pw.y + ( Gt.a - 0.5 ) * 0.3;
	let wet = 1.0 - smoothstep( 0.3, 0.75, y );
	let damp = ( 1.0 - smoothstep( 0.7, 1.5, y ) ) * 0.4;
	let algae = mix( vec3f( 0.028, 0.042, 0.02 ), vec3f( 0.11, 0.105, 0.05 ), Gt.a * 0.6 + Gt.r * 0.4 );
	let Gb = textureSample( vlgGrime, smpAnisoRepeat, q * 3.1 );
	let barn = Gb.b * smoothstep( - 1.6, - 0.35, y ) * ( 1.0 - smoothstep( 0.25, 0.6, y ) );
	var c = col * ( 1.0 - damp );
	c = mix( c, algae, wet );
	c = mix( c, vec3f( 0.52, 0.5, 0.44 ), barn * 0.9 );
	var r = mix( rough, 0.3, wet * 0.9 );
	r = mix( r, 0.55, damp );
	r = mix( r, 0.9, barn );
	return VlmTidal( c, r, wet );
}


struct VlmGlass { color: vec3f, rough: f32, emissive: vec3f };

fn vlmGlass( uvm: vec2f, aTint: vec3f, seed: f32, kind: f32, lit: f32 ) -> VlmGlass {
	// grime map channels: R streaks, G salt, B spots, A macro
	let Gl = textureSample( vlgGrime, smpAnisoRepeat, uvm * 0.45 + vec2f( seed * 3.7, seed * 1.3 ) );
	let edgeD = min( min( uvm.x, 1.0 - uvm.x ), min( uvm.y, 1.0 - uvm.y ) );
	let frameDirt = 1.0 - smoothstep( 0.0, 0.07, edgeD );
	let curtain = ( 1.0 - smoothstep( 0.18, 0.3, uvm.x ) + smoothstep( 0.7, 0.82, uvm.x ) ) * step( 0.35, seed );
	let folds = vlmN01( sin( uvm.x * 70.0 + seed * 20.0 ) );
	let interior = vec3f( 0.012, 0.014, 0.017 ) * ( Gl.a * 0.8 + 0.6 );
	var winCol = mix( interior, aTint * 0.16 * ( folds * 0.4 + 0.6 ), curtain );
	winCol = winCol + vec3f( 0.05, 0.05, 0.045 ) * Gl.g + vec3f( 0.03, 0.026, 0.02 ) * ( Gl.b + frameDirt );
	let lanternCol = aTint * 0.55 * ( 1.0 - Gl.b * 0.25 );
	let isLantern = step( 0.5, kind );
	let color = mix( winCol, lanternCol, isLantern );
	let rough = mix( 0.035 + Gl.r * 0.22 + Gl.g * 0.18 + Gl.b * 0.1 + frameDirt * 0.25, 0.32 + Gl.r * 0.15, isLantern );

	let nightOn = smoothstep( 0.15, 0.75, frame.night );
	let warm = vec3f( 1.0, 0.56, 0.24 );
	let center = 1.0 - smoothstep( 0.1, 0.75, abs( uvm.x - 0.5 ) * 1.4 + abs( uvm.y - 0.62 ) );
	let winGlow = mix( warm * ( center * 0.6 + 0.5 ), ( aTint * 0.5 + warm * 0.5 ) * ( folds * 0.3 + 0.45 ), curtain )
		* ( vlmHash21( seed, 3.1 ) * 0.5 + 0.75 ) * 3.2 * ( 1.0 - Gl.r * 0.25 );
	let flicker = sin( frame.time * 9.0 + seed * 40.0 ) * sin( frame.time * 5.3 + seed * 13.0 ) * 0.12 + 0.9;
	let lanternGlow = vec3f( 1.0, 0.64, 0.3 ) * 6.0 * flicker;
	let emissive = mix( winGlow, lanternGlow, isLantern ) * nightOn * max( lit, isLantern );
	return VlmGlass( color, rough, emissive );
}

fn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>){
	o.vTint = v.tint;
	o.vData = v.vdata;

}
fn sourceSurface(in:FragInput,s:ptr<function,Surface>){
	s.emissive = s.emissive * mat.emissiveIntensity;
	s.envIntensity = mat.envMapIntensity;

	let aTint = in.vs.vTint;
	let aData = in.vs.vData;
	let seed = aData.x;
	let paint = aData.y;
	let pattern = aData.z;
	let weather = aData.w;
	let uv0 = in.uv;
	let isCap = step( 1500.0, uv0.x );
	let isEnd = step( 500.0, uv0.x ) * ( 1.0 - isCap );
	let endAny = max( isCap, isEnd );
	let uvS = uv0 - vec2f( isCap * 2000.0 + isEnd * 1000.0, 0.0 );
	let fw = fwidth( uvS );
	let px = max( fw.x, fw.y );
	let near = 1.0 - smoothstep( 0.0012, 0.005, px );

	let mLap = vlmBand01( pattern, 1.0 );
	let mBB = vlmBand01( pattern, 2.0 );
	let mTG = vlmBand01( pattern, 3.0 );
	let mLv = vlmBand01( pattern, 4.0 );
	let mPk = vlmBand01( pattern, 5.0 );
	let mHz = vlmBand01( pattern, 6.0 );
	let mNail = step( 6.5, pattern );
	let isVert = mBB + mTG;
	let g = mix( uvS, uvS.yx, isVert ); // g.x along the grain

	// every board of siding / planking gets its own piece of the wood texture
	let boardIdx = floor( uvS.y / 0.2 ) * mLap + floor( uvS.y / 0.16 ) * mHz
		+ floor( uvS.x / 0.42 ) * mBB + floor( uvS.x / 0.14 ) * mTG + floor( uvS.y / 0.105 ) * mPk;
	let pieceSeed = seed * 131.0 + boardIdx * 7.13;
	let off = vec2f( vlmHash21( pieceSeed, 1.7 ), vlmHash21( pieceSeed, 9.2 ) );
	let tuv = g * vec2f( 0.5, 1.0 ) + off;
	let A = textureSample( vlgWoodA, smpAnisoRepeat, tuv );
	let N = textureSample( vlgWoodN, smpAnisoRepeat, tuv );
	let D = textureSample( vlgWoodN, smpAnisoRepeat, tuv * vec2f( 3.0, 4.0 ) + vec2f( 0.37, 0.61 ) );

	// grime / salt / macro variation in mesh space
	let Gm = textureSample( vlgGrime, smpAnisoRepeat, uvS * 0.5 + off.yx * 0.5 );
	let macroV = textureSample( vlgGrime, smpAnisoRepeat, uvS * 0.07 + vec2f( seed * 0.37, seed * 0.71 ) ).a;
	let hasPaint = step( 0.001, paint );
	// raw timber weathers along the grain: the same grime map stretched ~15x along the board, so
	// dirt, salt and tone vary in long streaks and change gradually from one end to the other
	let Gs = textureSample( vlgGrime, smpAnisoRepeat, g * vec2f( 0.035, 0.55 ) + off * 0.5 );
	let rawK = 1.0 - hasPaint;

	// end grain: polar remap of the side-grain texture -> growth rings become arcs (plank ends) or circles (post tops)
	let pith = mix(
		vec2f( vlmHash21( seed, 5.3 ) * 0.4 - 0.6, vlmHash21( seed, 6.1 ) * 0.3 + 0.25 ),
		vec2f( ( vlmHash21( seed, 3.1 ) - 0.5 ) * 0.02, ( vlmHash21( seed, 4.7 ) - 0.5 ) * 0.02 ),
		isCap
	);
	let ev = uvS - pith;
	let er = length( ev );
	let eUV = vec2f( atan2( ev.y, ev.x ) / VLM_TAU * 3.0 + off.x, er + off.y );
	let eLod = log2( max( fwidth( er ) * 1024.0, 1.0 ) );
	let E = textureSampleLevel( vlgWoodA, smpAnisoRepeat, eUV, eLod );

	// raw timber colour: the baked map is fully weathered silver; less weathered pieces shift to warm brown
	let wW = clamp( weather + ( macroV - 0.5 ) * 0.35, 0.0, 1.0 );
	// wood exposed under chipped paint was protected until recently: lighter and warmer
	let wWx = wW * mix( 1.0, 0.6, hasPaint );
	let sideCol = mix( A.rgb * vec3f( 1.26, 1.02, 0.78 ) * 1.04, A.rgb, smoothstep( 0.12, 0.6, wWx ) );
	let endCol = mix( E.rgb * vec3f( 1.3, 1.0, 0.7 ), E.rgb, wW ) * 0.66;
	let woodTone = mix( aTint, vec3f( 1.2 ), hasPaint );
	let boardTone = ( vlmHash21( pieceSeed, 3.3 ) - 0.5 ) * 0.16 + ( Gs.a - 0.5 ) * 0.14;
	let wood = mix( sideCol, endCol, endAny ) * woodTone * ( macroV * 0.2 + 0.9 ) * ( 1.0 + boardTone * rawK );

	// paint film: chips follow the baked chip field; the film has thickness at the chip edges
	// chip field quantiles: 5% 0.26, 10% 0.31, 20% 0.35, 30% 0.39 -> paint 0.7 leaves ~8% bare wood,
	// 0.6 ~14%, 0.5 ~22%; lap drip edges and the splash zone near wall bottoms wear faster
	let chip = A.a;
	let lapEdge = ( 1.0 - smoothstep( 0.0, 0.18, fract( uvS.y / 0.2 ) ) ) * mLap;
	let lowWall = ( 1.0 - smoothstep( 0.0, 0.7, uvS.y ) ) * ( mLap + mBB + mTG );
	let thr = clamp( 0.395 - ( paint - 0.4 ) * 0.32 + lapEdge * 0.05 + lowWall * 0.05, 0.15, 0.55 );
	let painted = smoothstep( thr - 0.012, thr + 0.012, chip ) * hasPaint * ( 1.0 - endAny );
	let e = 1.0 / 1024.0;
	let cX = textureSample( vlgWoodA, smpAnisoRepeat, tuv + vec2f( e, 0.0 ) ).a - textureSample( vlgWoodA, smpAnisoRepeat, tuv - vec2f( e, 0.0 ) ).a;
	let cY = textureSample( vlgWoodA, smpAnisoRepeat, tuv + vec2f( 0.0, e ) ).a - textureSample( vlgWoodA, smpAnisoRepeat, tuv - vec2f( 0.0, e ) ).a;
	let edge = ( 1.0 - smoothstep( 0.0, 0.025, abs( chip - thr ) ) ) * hasPaint * near;
	let edgeSlope = vec2f( cX, cY ) * edge * - 9.0;
	let P = textureSample( vlgPaintN, smpAnisoRepeat, g * vec2f( 1.0, 2.0 ) + off * 3.7 );

	let fade = clamp( Gm.a * 0.55 + wW * 0.25 + macroV * 0.2, 0.0, 1.0 );
	let chalk = aTint * 0.64 + vec3f( 0.27, 0.265, 0.25 );
	// bright paints (white trim, cream siding) never stay white out here: dusty, yellowed, grey
	let aged = mix( aTint, aTint * vec3f( 0.86, 0.84, 0.77 ), smoothstep( 0.45, 0.8, luminance( aTint ) ) * ( 0.6 + Gm.a * 0.4 ) );
	let paintCol = mix( aged, chalk, fade * 0.5 ) * ( ( P.b - 0.5 ) * 0.16 + 1.0 );

	// siding / plank patterns (shading and slope in mesh space)
	let fvLap = fract( uvS.y / 0.2 );
	let lapFade = 1.0 - smoothstep( 0.02, 0.07, fw.y );
	let lapShade = mix( 0.92, mix( 0.45, 1.03, smoothstep( 0.0, 0.09, fvLap ) ) - fvLap * 0.07, lapFade );
	let lapSy = mix( 0.0, select( - 0.14, - 2.4, fvLap < 0.07 ), lapFade );

	let fuBB = fract( uvS.x / 0.42 );
	let bbFade = 1.0 - smoothstep( 0.03, 0.09, fw.x );
	let inBatten = step( fuBB, 0.12 );
	let bbShade = mix( 0.95, mix( 1.0, 0.7, ( 1.0 - smoothstep( 0.12, 0.2, fuBB ) ) * ( 1.0 - inBatten ) ) * ( inBatten * 0.06 + 1.0 ), bbFade );
	let bbSx = select( select( 0.0, 2.0, abs( fuBB - 0.112 ) < 0.012 ), - 2.0, fuBB < 0.015 ) * bbFade;

	let fuTG = fract( uvS.x / 0.14 );
	let tgFade = 1.0 - smoothstep( 0.015, 0.05, fw.x );
	let tgShade = mix( 0.96, mix( 0.6, 1.0, smoothstep( 0.0, 0.06, fuTG ) ), tgFade );
	let tgSx = select( select( 0.0, 1.0, fuTG < 0.06 ), - 1.0, fuTG < 0.03 ) * tgFade;

	let fvLv = fract( uvS.y / 0.07 );
	let lvFade = 1.0 - smoothstep( 0.008, 0.03, fw.y );
	let lvShade = mix( 0.8, mix( 0.42, 1.05, smoothstep( 0.0, 0.8, fvLv ) ), lvFade );
	let lvSy = mix( 0.0, select( - 1.0, 1.2, fvLv > 0.85 ), lvFade );

	let fvPk = fract( uvS.y / 0.105 );
	let pkFade = 1.0 - smoothstep( 0.012, 0.04, fw.y );
	let pkShade = mix( 0.95, mix( 0.58, 1.0, smoothstep( 0.0, 0.07, fvPk ) ), pkFade );
	let pkSy = select( select( 0.0, 1.0, fvPk < 0.07 ), - 1.0, fvPk < 0.035 ) * pkFade;

	let fvHz = fract( uvS.y / 0.16 );
	let hzFade = 1.0 - smoothstep( 0.015, 0.05, fw.y );
	let hzShade = mix( 0.95, mix( 0.5, 1.0, smoothstep( 0.0, 0.05, fvHz ) ), hzFade );

	let shade = 1.0
		+ mLap * ( lapShade - 1.0 ) + mBB * ( bbShade - 1.0 ) + mTG * ( tgShade - 1.0 )
		+ mLv * ( lvShade - 1.0 ) + mPk * ( pkShade - 1.0 ) + mHz * ( hzShade - 1.0 );

	// nail heads with rust bleed on deck planks
	let nu = ( fract( ( uvS.x - 0.1 ) / 0.8 + 0.5 ) - 0.5 ) * 0.8;
	let nv = min( abs( uvS.y - 0.045 ), abs( uvS.y - 0.155 ) );
	let nearN = 1.0 - smoothstep( 0.0015, 0.004, px );
	let nail = ( 1.0 - smoothstep( 0.0045, 0.0075, length( vec2f( nu, nv ) ) ) ) * mNail * nearN;
	let stain = ( 1.0 - smoothstep( 0.0, 0.05, length( vec2f( nu * 0.3, nv * 1.4 ) ) ) ) * mNail;

	// ---- colour
	var col = mix( wood * ( stain * - 0.4 + 1.0 ), paintCol, painted );
	col = mix( col, vec3f( 0.04, 0.032, 0.028 ), nail );
	let dirt = mix( 0.22, 0.5, wW );
	let Gd = mix( Gm, Gs, rawK );
	col = col * ( 1.0 - Gd.r * dirt * mix( 0.75, 0.35, rawK ) );
	col = mix( col, vec3f( 0.62, 0.61, 0.57 ), Gd.g * mix( 0.28, 0.16, rawK ) * ( painted * 0.5 + 0.5 ) );
	col = col * ( 1.0 - Gm.b * 0.3 * hasPaint );
	let isWall = mLap + mBB + mTG;
	// splash-back along the bottom of walls: rain throws sand and soil up the boards, and green
	// mildew grows where they stay damp; a ragged upper edge
	let splashH = 0.55 + ( Gm.a - 0.5 ) * 0.5 + ( macroV - 0.5 ) * 0.3;
	let splash = ( 1.0 - smoothstep( 0.0, splashH, uvS.y ) ) * isWall;
	col = col * ( 1.0 - splash * 0.5 );
	col = mix( col, col * vec3f( 0.78, 0.9, 0.62 ), splash * smoothstep( 0.4, 0.7, Gm.a + Gm.b * 0.5 ) * 0.7 );
	let ao = mix( N.a, mix( 1.0, N.a, 0.35 ) * P.a, painted );
	col = col * shade * mix( 1.0, ao, 0.5 );

	// ---- world-space weathering
	let Ng = in.N;
	let upF = sat( Ng.y );
	let sideF = 1.0 - abs( Ng.y );
	// worn walking path down the middle of deck boards: fibres worn off (paler, warmer, smoother),
	// grey dirt ground into the grain and dark scuffs
	let walkC = - paint - 1.0;
	let wd = ( uvS.x - walkC + ( macroV - 0.5 ) * 0.5 ) / 0.6;
	let walkK = step( paint, - 0.5 ) * smoothstep( 0.6, 0.9, upF ) * exp( - wd * wd ) * ( 1.0 - endAny );
	// the path is worn brown-grey: the silver skin scuffed off, grime trodden into the grain
	let trodden = col * vec3f( 0.8, 0.76, 0.72 );
	col = mix( col, trodden, walkK * 0.55 );
	// grit and dirt packed along the edges of deck boards (the gaps collect it)
	let bev = min( uvS.y, 0.18 - uvS.y );
	let edgeDirt = mNail * smoothstep( 0.6, 0.9, upF ) * ( 1.0 - smoothstep( 0.004, 0.03, bev ) ) * ( 1.0 - endAny );
	col = col * ( 1.0 - edgeDirt * ( 0.18 + Gs.r * 0.15 ) );
	// water that pools along the board edges keeps them damp and darker in broad, gradual bands
	// (no blotches: it follows the boards); sun and salt bleach the open middle of the deck
	let damp = smoothstep( 0.6, 0.9, upF ) * rawK * ( 1.0 - endAny ) * smoothstep( 0.45, 0.75, macroV );
	col = col * ( 1.0 - damp * ( 1.0 - smoothstep( 0.0, 0.06, bev ) ) * 0.18 );
	// rain and rust streaks running down vertical faces (from nails, bolts, sills and roof edges)
	let sqA = textureSample( vlgGrime, smpAnisoRepeat, vec2f( ( in.P.x + in.P.z ) * 2.3 + seed * 5.0, in.P.y * 0.12 ) );
	let sqB = textureSample( vlgGrime, smpAnisoRepeat, vec2f( ( in.P.x - in.P.z ) * 5.1, in.P.y * 0.3 + 0.37 ) );
	let streak = sideF * sideF * clamp( smoothstep( 0.45, 0.85, sqB.r ) + smoothstep( 0.6, 0.7, sqA.a ) * 0.35, 0.0, 1.0 ) * ( 0.45 + wW * 0.55 ) * ( 1.0 - endAny );
	let rustS = streak * smoothstep( 0.5, 0.6, sqA.a );
	col = col * ( 1.0 - streak * 0.24 );
	col = mix( col, col * vec3f( 1.04, 0.78, 0.6 ), rustS * 0.6 );
	// bird droppings: sparse splats on upward faces above head height (rails, posts, cap beams, sills)
	let bq = in.P.xz / 0.3;
	let bc = floor( bq );
	let bh = vlmHash21( bc.x + seed, bc.y );
	let bo = fract( bq ) - 0.5 - ( vec2f( vlmHash21( bc.x + 3.1, bc.y ), vlmHash21( bc.x, bc.y + 7.7 ) ) - 0.5 ) * 0.45;
	let bd = length( bo * vec2f( 1.0, 1.35 ) ) + ( Gm.a - 0.5 ) * 0.08;
	let dropK = step( 0.93, bh ) * smoothstep( 0.75, 0.95, upF ) * step( 2.6, in.P.y ) * ( 1.0 - smoothstep( 0.1, 0.15, bd ) );
	let dropCore = dropK * ( 1.0 - smoothstep( 0.02, 0.07, bd ) );
	col = mix( col, mix( vec3f( 0.62, 0.61, 0.56 ), vec3f( 0.8, 0.79, 0.74 ), dropCore ), dropK * 0.6 );

	// ---- normal (grain space -> mesh space) + analytic pattern relief
	let sWood = vlmSlopeOf( N ) + vlmSlopeOf( D ) * near * 0.5;
	let sPaint = vlmSlopeOf( N ) * 0.3 + vlmSlopeOf( P );
	let sG = ( mix( sWood, sPaint, painted ) + edgeSlope ) * ( 1.0 - endAny * 0.6 ) * ( 1.0 - walkK * 0.45 ) * ( 1.0 - dropK * 0.7 );
	let sM = mix( sG, sG.yx, isVert );
	let sPat = vec2f(
		mBB * bbSx + mTG * tgSx,
		mLap * lapSy + mLv * lvSy + mPk * pkSy
	);
	// ---- roughness: satin-ish paint vs dry raw timber; grime and salt dull it
	let roughRaw = N.b + wW * 0.03;
	let roughPaint = mix( 0.4, 0.7, P.b ) + fade * 0.12;
	let rough = mix( roughRaw, roughPaint, painted ) + Gm.r * 0.06 + Gm.g * 0.08 - nail * 0.3 - walkK * 0.1 + streak * 0.05 - dropCore * 0.2;
	let res = vlmTidal( in.P, col, rough );

	// ---- glass mode (windows and lanterns share this material to save a draw call)
	let isGlass = step( 8.5, pattern );
	let glass = vlmGlass( in.uv, aTint, seed, paint, weather );
	s.albedo = mix( res.col, glass.color, isGlass );
	s.roughness = clamp( mix( res.rough, glass.rough, isGlass ), 0.03, 1.0 );
	s.ao = mix( ao, 1.0, isGlass );
	s.emissive = glass.emissive * isGlass;
	s.normal = vlmNormalFromSlope( in.P, in.N, in.uv, ( sM + sPat ) * ( 1.0 - isGlass ) );

}
#endif

#ifdef VILLAGE_KIND_1
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var vlgHardA:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var vlgHardN:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var vlgRope:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var vlgGrime:texture_2d<f32>;

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

fn perturbNormalByMap( P: vec3f, N: vec3f, uv: vec2f, mapN: vec3f ) -> vec3f {
	let dp1 = dpdx( P ); let dp2 = dpdy( P );
	let duv1 = dpdx( uv ); let duv2 = dpdy( uv );
	let dp2perp = cross( dp2, N ); let dp1perp = cross( N, dp1 );
	let T = dp2perp * duv1.x + dp1perp * duv2.x;
	let B = dp2perp * duv1.y + dp1perp * duv2.y;
	let invmax = inverseSqrt( max( max( dot( T, T ), dot( B, B ) ), 1e-20 ) );
	return normalize( mat3x3f( T * invmax, B * invmax, N ) * mapN );
}

const VLM_TAU: f32 = 6.283185307179586;

fn vlmHash21( x: f32, y: f32 ) -> f32 { return fract( sin( x * 12.9898 + y * 78.233 ) * 43758.5453 ); }
fn vlmN01( n: f32 ) -> f32 { return n * 0.5 + 0.5; }

// baked normal (RG = xy of a unit normal) -> surface slope ( -dh/du, -dh/dv )
fn vlmSlopeOf( t: vec4f ) -> vec2f {
	let xy = t.xy * 2.0 - 1.0;
	return xy / sqrt( max( 1.0 - dot( xy, xy ), 0.04 ) );
}

fn vlmBand01( p: f32, k: f32 ) -> f32 { return step( k - 0.5, p ) * step( p, k + 0.5 ); }

// three normalMap( packN( vec3( s, 1 ) ) ): tangent frame from the mesh uv derivatives
fn vlmNormalFromSlope( P: vec3f, N: vec3f, uv: vec2f, s: vec2f ) -> vec3f {
	return perturbNormalByMap( P, N, uv, normalize( vec3f( s, 1.0 ) ) );
}



struct VlmTidal { col: vec3f, rough: f32, wet: f32 };

// Tidal zone: algae / barnacles / wet darkening driven by world height above sea level.
fn vlmTidal( pw: vec3f, col: vec3f, rough: f32 ) -> VlmTidal {
	let q = vec2f( pw.x + pw.z * 0.7, pw.y );
	let Gt = textureSample( vlgGrime, smpAnisoRepeat, q * vec2f( 0.6, 1.2 ) );
	let y = pw.y + ( Gt.a - 0.5 ) * 0.3;
	let wet = 1.0 - smoothstep( 0.3, 0.75, y );
	let damp = ( 1.0 - smoothstep( 0.7, 1.5, y ) ) * 0.4;
	let algae = mix( vec3f( 0.028, 0.042, 0.02 ), vec3f( 0.11, 0.105, 0.05 ), Gt.a * 0.6 + Gt.r * 0.4 );
	let Gb = textureSample( vlgGrime, smpAnisoRepeat, q * 3.1 );
	let barn = Gb.b * smoothstep( - 1.6, - 0.35, y ) * ( 1.0 - smoothstep( 0.25, 0.6, y ) );
	var c = col * ( 1.0 - damp );
	c = mix( c, algae, wet );
	c = mix( c, vec3f( 0.52, 0.5, 0.44 ), barn * 0.9 );
	var r = mix( rough, 0.3, wet * 0.9 );
	r = mix( r, 0.55, damp );
	r = mix( r, 0.9, barn );
	return VlmTidal( c, r, wet );
}

fn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>){
	o.vTint = v.tint;
	o.vData = v.vdata;

}
fn sourceSurface(in:FragInput,s:ptr<function,Surface>){
	s.emissive = s.emissive * mat.emissiveIntensity;
	s.envIntensity = mat.envMapIntensity;

	let aTint = in.vs.vTint;
	let aData = in.vs.vData;
	let seed = aData.x;
	let rustAmt = aData.y;
	let isRope = step( 1.5, aData.w );
	let uv0 = in.uv;
	let off = vec2f( vlmHash21( seed, 2.3 ), vlmHash21( seed, 8.9 ) );
	let tuv = uv0 + off;
	let HA = textureSample( vlgHardA, smpAnisoRepeat, tuv );
	let HN = textureSample( vlgHardN, smpAnisoRepeat, tuv );
	let hasRust = step( 0.001, rustAmt );
	let thr = mix( 0.95, 0.3, rustAmt );
	let rust = smoothstep( thr - 0.06, thr + 0.06, HA.r ) * hasRust;
	var base = aTint * ( 1.0 - HA.b * 0.2 ) * ( ( HA.r - 0.5 ) * 0.1 + 1.0 );
	base = mix( base, vec3f( 0.3, 0.3, 0.29 ), HA.g * 0.55 * hasRust * ( 1.0 - rust ) );
	let rustCol = mix( vec3f( 0.12, 0.045, 0.02 ), vec3f( 0.4, 0.17, 0.06 ), smoothstep( 0.3, 0.9, HA.r ) );
	let hardCol = mix( base, rustCol, rust ) * mix( 1.0, HN.a, 0.5 );
	let hardRough = mix( clamp( aData.w + ( HN.b - 0.5 ) * 0.35 - HA.g * 0.15 * hasRust, 0.04, 1.0 ), 0.88, rust );
	let hardS = vlmSlopeOf( HN ) * ( rust * 0.8 + 0.25 );

	// rope: uvs normalised by the circumference so the three strands wrap seamlessly
	let ropeR = max( aData.w - 2.0, 0.004 );
	let R = textureSample( vlgRope, smpAnisoRepeat, uv0 / ( ropeR * VLM_TAU ) + vec2f( aData.x * 3.1, 0.0 ) );
	let ropeCol = aTint * mix( 0.45, 1.25, R.r ) * mix( 1.0, R.g, 0.6 );
	let rn = R.ba * 2.0 - 1.0;
	let ropeS = rn / sqrt( max( 1.0 - dot( rn, rn ), 0.04 ) );

	let res = vlmTidal( in.P, mix( hardCol, ropeCol, isRope ), mix( hardRough, 0.93, isRope ) );
	s.normal = vlmNormalFromSlope( in.P, in.N, in.uv, mix( hardS, ropeS, isRope ) );
	s.albedo = res.col;
	s.roughness = res.rough;
	s.metalness = aData.z * ( 1.0 - max( rust, res.wet ) ) * ( 1.0 - isRope );
	s.ao = mix( HN.a, R.g, isRope );

}
#endif

#ifdef VILLAGE_KIND_2
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var vlgRoofA:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var vlgRoofN:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var vlgHardN:texture_2d<f32>;

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

fn perturbNormalByMap( P: vec3f, N: vec3f, uv: vec2f, mapN: vec3f ) -> vec3f {
	let dp1 = dpdx( P ); let dp2 = dpdy( P );
	let duv1 = dpdx( uv ); let duv2 = dpdy( uv );
	let dp2perp = cross( dp2, N ); let dp1perp = cross( N, dp1 );
	let T = dp2perp * duv1.x + dp1perp * duv2.x;
	let B = dp2perp * duv1.y + dp1perp * duv2.y;
	let invmax = inverseSqrt( max( max( dot( T, T ), dot( B, B ) ), 1e-20 ) );
	return normalize( mat3x3f( T * invmax, B * invmax, N ) * mapN );
}

const VLM_TAU: f32 = 6.283185307179586;

fn vlmHash21( x: f32, y: f32 ) -> f32 { return fract( sin( x * 12.9898 + y * 78.233 ) * 43758.5453 ); }
fn vlmN01( n: f32 ) -> f32 { return n * 0.5 + 0.5; }

// baked normal (RG = xy of a unit normal) -> surface slope ( -dh/du, -dh/dv )
fn vlmSlopeOf( t: vec4f ) -> vec2f {
	let xy = t.xy * 2.0 - 1.0;
	return xy / sqrt( max( 1.0 - dot( xy, xy ), 0.04 ) );
}

fn vlmBand01( p: f32, k: f32 ) -> f32 { return step( k - 0.5, p ) * step( p, k + 0.5 ); }

// three normalMap( packN( vec3( s, 1 ) ) ): tangent frame from the mesh uv derivatives
fn vlmNormalFromSlope( P: vec3f, N: vec3f, uv: vec2f, s: vec2f ) -> vec3f {
	return perturbNormalByMap( P, N, uv, normalize( vec3f( s, 1.0 ) ) );
}


fn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>){
	o.vTint = v.tint;
	o.vData = v.vdata;

}
fn sourceSurface(in:FragInput,s:ptr<function,Surface>){
	s.emissive = s.emissive * mat.emissiveIntensity;
	s.envIntensity = mat.envMapIntensity;

	let aTint = in.vs.vTint;
	let aData = in.vs.vData;
	let seed = aData.x;
	let rustAmt = aData.y;
	let galv = aData.z;
	let uvm = in.uv;
	let fw = fwidth( uvm );
	let near = 1.0 - smoothstep( 0.0015, 0.006, max( fw.x, fw.y ) );
	let sheetF = uvm.y / 0.84 + floor( seed * 13.0 );
	let sheetId = floor( sheetF );
	let sheetR = vlmHash21( sheetId, seed * 91.0 );
	let tuv = vec2f( sheetF, uvm.x / 1.68 + sheetR * 5.37 );
	let M = textureSample( vlgRoofA, smpAnisoRepeat, tuv );
	let N = textureSample( vlgRoofN, smpAnisoRepeat, tuv );
	let macroV = textureSample( vlgRoofA, smpAnisoRepeat, tuv * vec2f( 0.13, 0.09 ) + vec2f( seed, seed * 1.7 ) );

	let eave = 1.0 - smoothstep( 0.0, 0.9, uvm.x );
	let replaced = step( 0.86, sheetR );
	let rustIn = clamp( rustAmt + 0.15 + replaced * 0.2 + ( macroV.r - 0.4 ) * 0.5, 0.0, 1.0 );
	// rust channel quantiles: 50% 0.25, 70% 0.31, 90% 0.42 -> rust 0.4 ~10%, 0.6 ~25%, 0.8 ~45%
	let thr = mix( 0.62, 0.23, rustIn ) - eave * 0.2;
	let rust = smoothstep( thr - 0.04, thr + 0.04, M.r );
	let fade = clamp( M.b * 0.7 + macroV.b * 0.3, 0.0, 1.0 );

	let paintCol = mix( aTint, aTint * 0.7 + vec3f( 0.12, 0.1, 0.085 ), fade * 0.55 ) * ( sheetR * 0.16 + 0.9 );
	let patchCol = paintCol * vec3f( 0.72, 0.68, 0.66 ) + vec3f( 0.03, 0.025, 0.02 );
	let galvCol = vec3f( 0.34, 0.35, 0.35 ) * ( fade * 0.35 + 0.78 );
	let base = mix( mix( paintCol, patchCol, replaced ), galvCol, galv );
	let rustCol = mix( vec3f( 0.12, 0.045, 0.02 ), vec3f( 0.4, 0.17, 0.065 ), M.g );
	var col = mix( base, rustCol, rust );
	col = col * ( 1.0 - M.a * 0.45 );
	col = col * mix( 1.0, N.a, 0.55 );

	// texture X runs along the eave (mesh v), texture Y up the slope (mesh u)
	let sT = vlmSlopeOf( N );
	let Hd = textureSample( vlgHardN, smpAnisoRepeat, uvm * 1.7 );
	let sl = vec2f( sT.y, sT.x ) + vlmSlopeOf( Hd ) * ( rust * 0.8 + 0.2 ) * near;
	s.normal = vlmNormalFromSlope( in.P, in.N, in.uv, sl );

	s.albedo = col;
	s.metalness = mix( mix( 0.04, 0.5, galv ), 0.0, rust );
	s.roughness = clamp( mix( mix( 0.55, 0.42, galv ) + ( N.b - 0.5 ) * 0.4 + fade * 0.15, 0.9, rust ) + M.a * 0.08, 0.05, 1.0 );
	s.ao = N.a;

}
#endif

#ifdef VILLAGE_KIND_3
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var vlgThatchA:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var vlgThatchN:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var vlgGrime:texture_2d<f32>;

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

fn perturbNormalByMap( P: vec3f, N: vec3f, uv: vec2f, mapN: vec3f ) -> vec3f {
	let dp1 = dpdx( P ); let dp2 = dpdy( P );
	let duv1 = dpdx( uv ); let duv2 = dpdy( uv );
	let dp2perp = cross( dp2, N ); let dp1perp = cross( N, dp1 );
	let T = dp2perp * duv1.x + dp1perp * duv2.x;
	let B = dp2perp * duv1.y + dp1perp * duv2.y;
	let invmax = inverseSqrt( max( max( dot( T, T ), dot( B, B ) ), 1e-20 ) );
	return normalize( mat3x3f( T * invmax, B * invmax, N ) * mapN );
}

const VLM_TAU: f32 = 6.283185307179586;

fn vlmHash21( x: f32, y: f32 ) -> f32 { return fract( sin( x * 12.9898 + y * 78.233 ) * 43758.5453 ); }
fn vlmN01( n: f32 ) -> f32 { return n * 0.5 + 0.5; }

// baked normal (RG = xy of a unit normal) -> surface slope ( -dh/du, -dh/dv )
fn vlmSlopeOf( t: vec4f ) -> vec2f {
	let xy = t.xy * 2.0 - 1.0;
	return xy / sqrt( max( 1.0 - dot( xy, xy ), 0.04 ) );
}

fn vlmBand01( p: f32, k: f32 ) -> f32 { return step( k - 0.5, p ) * step( p, k + 0.5 ); }

// three normalMap( packN( vec3( s, 1 ) ) ): tangent frame from the mesh uv derivatives
fn vlmNormalFromSlope( P: vec3f, N: vec3f, uv: vec2f, s: vec2f ) -> vec3f {
	return perturbNormalByMap( P, N, uv, normalize( vec3f( s, 1.0 ) ) );
}


fn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>){
	o.vTint = v.tint;
	o.vData = v.vdata;

}
fn sourceSurface(in:FragInput,s:ptr<function,Surface>){
	s.emissive = s.emissive * mat.emissiveIntensity;
	s.envIntensity = mat.envMapIntensity;

	let aData = in.vs.vData;
	let seed = aData.x;
	let age = aData.y;
	let uvm = in.uv;
	let tuv = vec2f( uvm.y + vlmHash21( seed, 1.3 ) * 7.0, uvm.x );
	let A = textureSample( vlgThatchA, smpAnisoRepeat, tuv );
	let N = textureSample( vlgThatchN, smpAnisoRepeat, tuv );
	// macro variation: patches of newer / older thatch and rain streaks down the slope
	let Gm = textureSample( vlgGrime, smpAnisoRepeat, vec2f( tuv.x * 0.23, tuv.y * 0.19 ) + seed );
	let Gs = textureSample( vlgGrime, smpAnisoRepeat, vec2f( tuv.x * 0.5, tuv.y * 0.25 ) + seed * 1.7 );

	let lum = dot( A.rgb, vec3f( 0.3, 0.59, 0.11 ) );
	let greyed = vec3f( lum ) * vec3f( 0.95, 0.9, 0.8 );
	let ageL = clamp( age + ( Gm.a - 0.5 ) * 0.9, 0.0, 1.0 );
	var col = mix( A.rgb, greyed, ageL * 0.8 ) * ( Gm.a * 0.35 + 0.82 ) * ( 1.0 - ageL * 0.2 );
	col = col * ( 1.0 - Gs.r * 0.25 ) * ( 1.0 - Gs.b * 0.35 );
	col = col * mix( 1.0, N.a, 0.55 );
	let sT = vlmSlopeOf( N );
	s.normal = vlmNormalFromSlope( in.P, in.N, in.uv, vec2f( sT.y, sT.x ) );
	s.albedo = col;
	s.roughness = N.b;
	s.ao = N.a;

}
#endif

#ifdef VILLAGE_KIND_4
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var vlgStoneA:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var vlgStoneN:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var vlgPaintN:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var vlgGrime:texture_2d<f32>;

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

fn perturbNormalByMap( P: vec3f, N: vec3f, uv: vec2f, mapN: vec3f ) -> vec3f {
	let dp1 = dpdx( P ); let dp2 = dpdy( P );
	let duv1 = dpdx( uv ); let duv2 = dpdy( uv );
	let dp2perp = cross( dp2, N ); let dp1perp = cross( N, dp1 );
	let T = dp2perp * duv1.x + dp1perp * duv2.x;
	let B = dp2perp * duv1.y + dp1perp * duv2.y;
	let invmax = inverseSqrt( max( max( dot( T, T ), dot( B, B ) ), 1e-20 ) );
	return normalize( mat3x3f( T * invmax, B * invmax, N ) * mapN );
}

const VLM_TAU: f32 = 6.283185307179586;

fn vlmHash21( x: f32, y: f32 ) -> f32 { return fract( sin( x * 12.9898 + y * 78.233 ) * 43758.5453 ); }
fn vlmN01( n: f32 ) -> f32 { return n * 0.5 + 0.5; }

// baked normal (RG = xy of a unit normal) -> surface slope ( -dh/du, -dh/dv )
fn vlmSlopeOf( t: vec4f ) -> vec2f {
	let xy = t.xy * 2.0 - 1.0;
	return xy / sqrt( max( 1.0 - dot( xy, xy ), 0.04 ) );
}

fn vlmBand01( p: f32, k: f32 ) -> f32 { return step( k - 0.5, p ) * step( p, k + 0.5 ); }

// three normalMap( packN( vec3( s, 1 ) ) ): tangent frame from the mesh uv derivatives
fn vlmNormalFromSlope( P: vec3f, N: vec3f, uv: vec2f, s: vec2f ) -> vec3f {
	return perturbNormalByMap( P, N, uv, normalize( vec3f( s, 1.0 ) ) );
}


fn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>){
	o.vTint = v.tint;
	o.vData = v.vdata;

}
fn sourceSurface(in:FragInput,s:ptr<function,Surface>){
	s.emissive = s.emissive * mat.emissiveIntensity;
	s.envIntensity = mat.envMapIntensity;

	let aTint = in.vs.vTint;
	let aData = in.vs.vData;
	let seed = aData.x;
	let style = aData.y;
	let uv0 = in.uv;
	let isCap = step( 1500.0, uv0.x );
	let isEnd = step( 500.0, uv0.x ) * ( 1.0 - isCap );
	let uvS = uv0 - vec2f( isCap * 2000.0 + isEnd * 1000.0, 0.0 );
	let isRubble = step( 1.5, style );
	let isPlaster = vlmBand01( style, 1.0 );
	let off = vec2f( vlmHash21( seed, 4.1 ), vlmHash21( seed, 7.7 ) );
	let tuv = uvS * mix( 0.5, 0.8, isRubble ) + off;
	let A = textureSample( vlgStoneA, smpAnisoRepeat, tuv );
	let N = textureSample( vlgStoneN, smpAnisoRepeat, tuv );
	// lime plaster: trowel marks from the paint film set, stains / dust from the grime map
	let puv = uvS + off * 3.0;
	let PN = textureSample( vlgPaintN, smpAnisoRepeat, puv * vec2f( 0.7, 1.4 ) );
	let PA = textureSample( vlgGrime, smpAnisoRepeat, puv * 0.5 );

	// plaster survives where the baked plaster field is high; the plaster edge has thickness
	let pf = A.a + ( PA.a - 0.5 ) * 0.12;
	let pm = smoothstep( 0.3, 0.33, pf ) * isPlaster;
	let e = 1.0 / 1024.0;
	let gX = textureSample( vlgStoneA, smpAnisoRepeat, tuv + vec2f( e, 0.0 ) ).a - textureSample( vlgStoneA, smpAnisoRepeat, tuv - vec2f( e, 0.0 ) ).a;
	let gY = textureSample( vlgStoneA, smpAnisoRepeat, tuv + vec2f( 0.0, e ) ).a - textureSample( vlgStoneA, smpAnisoRepeat, tuv - vec2f( 0.0, e ) ).a;
	let pEdge = ( 1.0 - smoothstep( 0.0, 0.03, abs( pf - 0.315 ) ) ) * isPlaster;

	let stoneCol = A.rgb * mix( vec3f( 1.0 ), aTint * 1.1, 0.15 );
	var plasterCol = aTint * ( 1.0 - PA.r * 0.3 ) * ( 1.0 - PA.b * 0.25 ) * ( ( PA.a - 0.5 ) * 0.16 + 1.0 );
	plasterCol = plasterCol * mix( 1.0, PN.a, 0.6 ) * ( ( PN.b - 0.5 ) * 0.12 + 1.0 );
	var col = mix( stoneCol * mix( 1.0, N.a, 0.5 ), plasterCol, pm );
	// sand dust and splash dirt near the ground
	let low = 1.0 - smoothstep( 0.05, 0.75, uvS.y );
	let dust = low * smoothstep( 0.25, 0.7, PA.a ) * 0.8;
	col = mix( col, vec3f( 0.46, 0.4, 0.3 ), dust );
	col = col * ( 1.0 - low * 0.18 );

	let sl = mix( vlmSlopeOf( N ), vlmSlopeOf( PN ) * 2.5, pm ) + vec2f( gX, gY ) * pEdge * - 6.0;
	s.normal = vlmNormalFromSlope( in.P, in.N, in.uv, sl );
	s.albedo = col;
	s.roughness = clamp( mix( N.b, PN.b * 0.3 + 0.66, pm ) + dust * 0.05, 0.05, 1.0 );
	s.ao = mix( N.a, PN.a, pm );

}
#endif

#ifdef VILLAGE_KIND_5
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var vlgNet:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var vlgGrime:texture_2d<f32>;

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

fn perturbNormalByMap( P: vec3f, N: vec3f, uv: vec2f, mapN: vec3f ) -> vec3f {
	let dp1 = dpdx( P ); let dp2 = dpdy( P );
	let duv1 = dpdx( uv ); let duv2 = dpdy( uv );
	let dp2perp = cross( dp2, N ); let dp1perp = cross( N, dp1 );
	let T = dp2perp * duv1.x + dp1perp * duv2.x;
	let B = dp2perp * duv1.y + dp1perp * duv2.y;
	let invmax = inverseSqrt( max( max( dot( T, T ), dot( B, B ) ), 1e-20 ) );
	return normalize( mat3x3f( T * invmax, B * invmax, N ) * mapN );
}

const VLM_TAU: f32 = 6.283185307179586;

fn vlmHash21( x: f32, y: f32 ) -> f32 { return fract( sin( x * 12.9898 + y * 78.233 ) * 43758.5453 ); }
fn vlmN01( n: f32 ) -> f32 { return n * 0.5 + 0.5; }

// baked normal (RG = xy of a unit normal) -> surface slope ( -dh/du, -dh/dv )
fn vlmSlopeOf( t: vec4f ) -> vec2f {
	let xy = t.xy * 2.0 - 1.0;
	return xy / sqrt( max( 1.0 - dot( xy, xy ), 0.04 ) );
}

fn vlmBand01( p: f32, k: f32 ) -> f32 { return step( k - 0.5, p ) * step( p, k + 0.5 ); }

// three normalMap( packN( vec3( s, 1 ) ) ): tangent frame from the mesh uv derivatives
fn vlmNormalFromSlope( P: vec3f, N: vec3f, uv: vec2f, s: vec2f ) -> vec3f {
	return perturbNormalByMap( P, N, uv, normalize( vec3f( s, 1.0 ) ) );
}


fn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>){
	o.vTint = v.tint;
	o.vData = v.vdata;

	let w = v.vdata.y;
	let ph = frame.time * 1.7 + v.position.x * 0.6 + v.position.z * 0.45 + v.vdata.x * 20.0;
	let gust = sin( ph ) * 0.6 + sin( ph * 2.3 + 1.7 ) * 0.3 + 0.55;
	let flutter = sin( frame.time * 7.0 + v.position.y * 9.0 + v.vdata.x * 50.0 ) * 0.25;
	let swayPos = v.position + vec3f( frame.windDir.x, flutter * 0.3, frame.windDir.y ) * ( ( gust + flutter ) * ( frame.windSpeed * 0.011 * w ) );

	let aData = v.vdata;
	let isFlag = step( 5000.0, aData.z );
	// flag: oriented downwind around its pole
	let a = aData.y;
	let dir = normalize( vec3f( frame.windDir.x, 0.0, frame.windDir.y ) );
	let perp = vec3f( - dir.z, 0.0, dir.x );
	let strength = smoothstep( 0.5, 9.0, frame.windSpeed );
	let phase = frame.time * 7.5 - a * 5.0 + aData.x * 30.0;
	let wave = sin( phase ) * ( a * 0.09 ) * ( strength * 0.7 + 0.3 );
	let droop = ( 1.0 - strength ) * a * 0.55;
	let flagPos = vec3f( aData.z - 10000.0, v.position.y - droop, aData.w ) + dir * ( a * ( 1.0 - droop * 0.4 ) ) + perp * wave;
	v.position = mix( swayPos, flagPos, isFlag );

}
fn sourceSurface(in:FragInput,s:ptr<function,Surface>){
	s.emissive = s.emissive * mat.emissiveIntensity;
	s.envIntensity = mat.envMapIntensity;

	let aTint = in.vs.vTint;
	let aData = in.vs.vData;
	let isFlag = step( 5000.0, aData.z );
	let isNet = step( 0.001, aData.z ) * ( 1.0 - isFlag );
	let uvm = in.uv;

	// net
	let tile = max( aData.z, 0.02 ) * 4.0;
	let nuv = uvm / tile;
	let Nt = textureSample( vlgNet, smpAnisoRepeat, nuv );
	let fwn = fwidth( nuv );
	let far = smoothstep( 0.05, 0.1, max( fwn.x, fwn.y ) );
	let sc = in.pixel;
	let ign = fract( fract( sc.x * 0.06711056 + sc.y * 0.00583715 ) * 52.9829189 );
	let netAlpha = mix( Nt.r, step( ign, 0.55 ), far );
	let netCol = aTint * mix( 0.5, 1.05, Nt.g ) * mix( 1.0, 0.8, far );
	let nn = Nt.ba * 2.0 - 1.0;
	let netS = nn / sqrt( max( 1.0 - dot( nn, nn ), 0.04 ) ) * ( 1.0 - far );

	// cloth: woven canvas, sun faded
	let weave = vlmN01( sin( uvm.x * 900.0 ) ) * vlmN01( sin( uvm.y * 900.0 ) );
	let wf = 1.0 - smoothstep( 0.0005, 0.002, fwidth( uvm.x ) );
	let fadeN = textureSample( vlgGrime, smpAnisoRepeat, uvm * 0.8 + aData.x ).a;
	let clothCol = mix( aTint, aTint * 0.6 + vec3f( 0.3 ), fadeN * 0.5 ) * ( weave * 0.12 * wf + 0.94 );

	// flag normal (the cloth swings downwind around its pole, see the vertex snippet)
	let a = aData.y;
	let dir = normalize( vec3f( frame.windDir.x, 0.0, frame.windDir.y ) );
	let perp = vec3f( - dir.z, 0.0, dir.x );
	let strength = smoothstep( 0.5, 9.0, frame.windSpeed );
	let phase = frame.time * 7.5 - a * 5.0 + aData.x * 30.0;
	let slopeW = cos( phase ) * 0.45 * ( a + 0.2 ) * ( strength * 0.7 + 0.3 );
	let flagN = normalize( perp - dir * slopeW ) * select( - 1.0, 1.0, in.front );

	s.normal = normalize( mix( vlmNormalFromSlope( in.P, in.N, in.uv, netS * isNet ), flagN, isFlag ) );
	s.alpha = mix( 1.0, netAlpha, isNet );
	s.albedo = mix( mix( clothCol, netCol, isNet ), aTint * ( fadeN * 0.15 + 0.88 ), isFlag );

}
#endif

#ifdef VILLAGE_KIND_6
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var vlgNet:texture_2d<f32>;

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

fn perturbNormalByMap( P: vec3f, N: vec3f, uv: vec2f, mapN: vec3f ) -> vec3f {
	let dp1 = dpdx( P ); let dp2 = dpdy( P );
	let duv1 = dpdx( uv ); let duv2 = dpdy( uv );
	let dp2perp = cross( dp2, N ); let dp1perp = cross( N, dp1 );
	let T = dp2perp * duv1.x + dp1perp * duv2.x;
	let B = dp2perp * duv1.y + dp1perp * duv2.y;
	let invmax = inverseSqrt( max( max( dot( T, T ), dot( B, B ) ), 1e-20 ) );
	return normalize( mat3x3f( T * invmax, B * invmax, N ) * mapN );
}

const VLM_TAU: f32 = 6.283185307179586;

fn vlmHash21( x: f32, y: f32 ) -> f32 { return fract( sin( x * 12.9898 + y * 78.233 ) * 43758.5453 ); }
fn vlmN01( n: f32 ) -> f32 { return n * 0.5 + 0.5; }

// baked normal (RG = xy of a unit normal) -> surface slope ( -dh/du, -dh/dv )
fn vlmSlopeOf( t: vec4f ) -> vec2f {
	let xy = t.xy * 2.0 - 1.0;
	return xy / sqrt( max( 1.0 - dot( xy, xy ), 0.04 ) );
}

fn vlmBand01( p: f32, k: f32 ) -> f32 { return step( k - 0.5, p ) * step( p, k + 0.5 ); }

// three normalMap( packN( vec3( s, 1 ) ) ): tangent frame from the mesh uv derivatives
fn vlmNormalFromSlope( P: vec3f, N: vec3f, uv: vec2f, s: vec2f ) -> vec3f {
	return perturbNormalByMap( P, N, uv, normalize( vec3f( s, 1.0 ) ) );
}


fn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>){
	o.vTint = v.tint;
	o.vData = v.vdata;

	let w = v.vdata.y;
	let ph = frame.time * 1.7 + v.position.x * 0.6 + v.position.z * 0.45 + v.vdata.x * 20.0;
	let gust = sin( ph ) * 0.6 + sin( ph * 2.3 + 1.7 ) * 0.3 + 0.55;
	let flutter = sin( frame.time * 7.0 + v.position.y * 9.0 + v.vdata.x * 50.0 ) * 0.25;
	let swayPos = v.position + vec3f( frame.windDir.x, flutter * 0.3, frame.windDir.y ) * ( ( gust + flutter ) * ( frame.windSpeed * 0.011 * w ) );
	v.position = swayPos;

}
fn sourceSurface(in:FragInput,s:ptr<function,Surface>){
	s.emissive = s.emissive * mat.emissiveIntensity;
	s.envIntensity = mat.envMapIntensity;

	let aTint = in.vs.vTint;
	let aData = in.vs.vData;
	let uvm = in.uv;
	let tile = max( aData.z, 0.02 ) * 4.0;
	let nuv = uvm / tile;
	let Nt = textureSample( vlgNet, smpAnisoRepeat, nuv );
	let fwn = fwidth( nuv );
	let far = smoothstep( 0.05, 0.1, max( fwn.x, fwn.y ) );
	let nn = Nt.ba * 2.0 - 1.0;
	let netS = nn / sqrt( max( 1.0 - dot( nn, nn ), 0.04 ) ) * ( 1.0 - far );

	s.normal = normalize( vlmNormalFromSlope( in.P, in.N, in.uv, netS ) );
	// strands up close; average coverage of the knotted mesh (~0.4) once they are sub-pixel
	s.alpha = mix( Nt.r, 0.4, far ) * 0.95;
	s.albedo = aTint * mix( 0.5, 1.05, Nt.g ) * mix( 1.0, 0.85, far );

}
#endif
@vertex
fn vertex(input:Input)->Output {
    setupSource();
    var v:SourceVertex;
    v.position=input.position;v.normal=input.normal;v.uv=input.uv;
    v.tint=input.tint;v.vdata=input.vdata;
    var o:Output;
    sourceVertex(&v,&o);
    o.P=(mesh_functions::get_world_from_local(input.instance)*vec4f(v.position,1.0)).xyz;
    o.position=position_world_to_clip(o.P);
    o.normal=mesh_functions::mesh_normal_local_to_world(v.normal,input.instance);
    o.uv=v.uv;return o;
}
@fragment
fn fragment(vs:Output,@builtin(front_facing) front:bool)->@location(0) vec4f {
    setupSource();
    var input:FragInput;input.vs=vs;input.P=vs.P;input.N=normalize(vs.normal);
#ifdef VILLAGE_DOUBLE_SIDED
    input.N=select(-input.N,input.N,front);
#endif
    input.V=normalize(view.world_position-vs.P);input.uv=vs.uv;
    input.pixel=vs.position.xy;input.front=front;
    var s:Surface;s.albedo=mat.color;s.alpha=mat.opacity;s.normal=input.N;
    s.roughness=mat.roughness;s.metalness=mat.metalness;s.emissive=mat.emissive;s.ao=1.0;s.envIntensity=1.0;
    sourceSurface(input,&s);
    if s.alpha < mat.alphaTest { discard; }
    if settings.flags.z > 0.5 { return vec4f(s.albedo,s.alpha); }
    return vec4f(sourceSceneLight(LightingSurface(s.albedo,s.normal,s.roughness,
        s.metalness,1.0,vec3f(0.0),s.ao,s.envIntensity,s.emissive),input.V,input.P,input.pixel),s.alpha);
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


fn hookEnvDiffuse( N: vec3f ) -> vec3f {
	let d = N;
	var e = sceneLighting.environmentSH[ 0 ].rgb * 0.282095
		+ sceneLighting.environmentSH[ 1 ].rgb * 0.488603 * d.y + sceneLighting.environmentSH[ 2 ].rgb * 0.488603 * d.z + sceneLighting.environmentSH[ 3 ].rgb * 0.488603 * d.x
		+ sceneLighting.environmentSH[ 4 ].rgb * 1.092548 * d.x * d.y + sceneLighting.environmentSH[ 5 ].rgb * 1.092548 * d.y * d.z
		+ sceneLighting.environmentSH[ 6 ].rgb * 0.315392 * ( 3.0 * d.z * d.z - 1.0 ) + sceneLighting.environmentSH[ 7 ].rgb * 1.092548 * d.x * d.z
		+ sceneLighting.environmentSH[ 8 ].rgb * 0.546274 * ( d.x * d.x - d.y * d.y );
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
     s.translucency,N,V,sceneLighting.sunDirection.xyz,sceneLighting.sunColor.xyz*terrainSunShadowAt(P)*cloudsShadow(P.xz)*sunShadow(P,N,pixel))
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

struct VillageSceneLighting {
    sunDirection: vec4f,
    sunColor: vec4f,
    environmentSH: array<vec4f,9>,
};
@group(#{MATERIAL_BIND_GROUP}) @binding(18) var<uniform> sceneLighting: VillageSceneLighting;
