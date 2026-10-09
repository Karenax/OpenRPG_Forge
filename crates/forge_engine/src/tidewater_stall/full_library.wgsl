// SPDX-License-Identifier: MIT
// Source-derived Tidewater 1438b1abfcaee3267092b75573014f4d9b4a983c; world hooks must be supplied by the scene.
const PI:f32=3.141592653589793;
const INV_PI:f32=0.3183098861837907;
const EPS:f32=1e-5;
fn sat(x:f32)->f32{return clamp(x,0.0,1.0);}
// SPDX-License-Identifier: MIT
// Source-derived from dgreenheck/tidewater@1438b1ab, StallKit.js and lighting.js.
// Full source surface library; lighting and renderer integration remain separate.
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var surfA: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var repeatSampler: sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var surfN: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var surfR: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var propA: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var propN: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(6) var propR: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(7) var signs: texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(8) var clampSampler: sampler;

struct Surface {
	albedo: vec3f,
	alpha: f32,
	normal: vec3f,      // world space, shading normal
	roughness: f32,
	emissive: vec3f,
	metalness: f32,
	translucency: vec3f, // fraction of the direct light transmitted through thin foliage (x lightColor)
	ao: f32,
	sheenColor: vec3f,
	specularIntensity: f32,
	clearcoat: f32,
	clearcoatRoughness: f32,
	sheenRoughness: f32,
	ior: f32,
	clearcoatNormal: vec3f,
	envIntensity: f32,
};

fn defaultSurface( N: vec3f ) -> Surface {
	var s: Surface;
	s.albedo = vec3f( 1.0 ); s.alpha = 1.0; s.normal = N; s.roughness = 1.0; s.metalness = 0.0;
	s.emissive = vec3f( 0.0 ); s.translucency = vec3f( 0.0 ); s.ao = 1.0; s.sheenColor = vec3f( 0.0 );
	s.specularIntensity = 1.0; s.clearcoat = 0.0; s.clearcoatRoughness = 0.0; s.sheenRoughness = 1.0;
	s.ior = 1.5; s.clearcoatNormal = N; s.envIntensity = 1.0;
	return s;
}


struct StallVaryings { vLayer:f32, vUV2:vec2f };
struct StallInput { vs:StallVaryings, uv:vec2f, color:vec4f, P:vec3f, N:vec3f };
struct StallFrame { time:f32, night:f32 };
struct StallMaterialParameters { emissiveIntensity:f32, envMapIntensity:f32 };

fn skHash( p: vec3f ) -> f32 { return fract( sin( dot( p, vec3f( 127.1, 311.7, 74.7 ) ) ) * 43758.5453 ); }
fn skNoise( p: vec3f ) -> f32 {
	let i = floor( p ); let f = fract( p ); let u = f * f * ( 3.0 - 2.0 * f );
	let a = mix( mix( skHash( i ), skHash( i + vec3f( 1, 0, 0 ) ), u.x ), mix( skHash( i + vec3f( 0, 1, 0 ) ), skHash( i + vec3f( 1, 1, 0 ) ), u.x ), u.y );
	let b = mix( mix( skHash( i + vec3f( 0, 0, 1 ) ), skHash( i + vec3f( 1, 0, 1 ) ), u.x ), mix( skHash( i + vec3f( 0, 1, 1 ) ), skHash( i + vec3f( 1, 1, 1 ) ), u.x ), u.y );
	return mix( a, b, u.z );
}
fn skFbm( p: vec3f ) -> f32 { return skNoise( p ) * 0.55 + skNoise( p * 2.13 + 7.1 ) * 0.3 + skNoise( p * 4.7 + 3.3 ) * 0.15; }
// 2D cellular noise: ( distance to the nearest feature, to the second nearest )
fn skCells( p: vec2f ) -> vec2f {
	let i = floor( p ); let f = fract( p );
	var d1 = 8.0; var d2 = 8.0;
	for ( var y = -1; y <= 1; y++ ) {
		for ( var x = -1; x <= 1; x++ ) {
			let g = vec2f( f32( x ), f32( y ) );
			let h = vec2f( skHash( vec3f( i + g, 1.7 ) ), skHash( vec3f( i + g, 9.3 ) ) );
			let d = length( g + h - f );
			if ( d < d1 ) { d2 = d1; d1 = d; } else if ( d < d2 ) { d2 = d; }
		}
	}
	return vec2f( d1, d2 );
}
// perturbed normal from a scalar height (screen-space derivatives)
fn skBump( P: vec3f, N: vec3f, h: f32 ) -> vec3f {
	let dPdx = dpdx( P ); let dPdy = dpdy( P );
	let r1 = cross( dPdy, N ); let r2 = cross( N, dPdx );
	let det = dot( dPdx, r1 );
	let grad = sign( det ) * ( dpdx( h ) * r1 + dpdy( h ) * r2 );
	return normalize( abs( det ) * N - grad + N * 1e-12 );
}

fn stallSurface(in:StallInput, frame:StallFrame, mat:StallMaterialParameters)->Surface {
var s=defaultSurface(in.N);
s.albedo=in.color.rgb;
s.alpha=in.color.a;
	s.emissive = s.emissive * mat.emissiveIntensity;
	s.envIntensity = mat.envMapIntensity;

	let L = round( in.vs.vLayer );
	let uv = in.uv;
	let uv2 = in.vs.vUV2;
	let P = in.P;
	let N0 = s.normal;
	// tangent frame from derivatives (uniform control flow): OpenGL normal maps, image v runs down
	let dp1 = dpdx( P ); let dp2 = dpdy( P ); let duv1 = dpdx( uv ); let duv2 = dpdy( uv );
	let dp2perp = cross( dp2, N0 ); let dp1perp = cross( N0, dp1 );
	let Tt = dp2perp * duv1.x + dp1perp * duv2.x;
	let Bt = dp2perp * duv1.y + dp1perp * duv2.y;
	let tInv = inverseSqrt( max( max( dot( Tt, Tt ), dot( Bt, Bt ) ), 1e-24 ) );
	let TBN = mat3x3f( Tt * tInv, - Bt * tInv, N0 );
	let g2x = dpdx( uv2 ); let g2y = dpdy( uv2 );
	let tint = in.color.rgb;
	var alb = tint;
	var rough = 0.8;
	var metal = 0.0;
	var ao = 1.0;
	var nrm = N0;
	if ( L > -0.5 ) {
		// scanned texture set: a prop (512 px) or a tiling surface (1K)
		var a: vec4f; var n: vec4f; var r: vec4f;
		if ( L > 99.5 ) {
			let li = i32( L - 100.0 );
			a = textureSampleGrad( propA, repeatSampler, uv, li, duv1, duv2 );
			n = textureSampleGrad( propN, repeatSampler, uv, li, duv1, duv2 );
			r = textureSampleGrad( propR, repeatSampler, uv, li, duv1, duv2 );
		} else {
			let li = i32( L );
			a = textureSampleGrad( surfA, repeatSampler, uv, li, duv1, duv2 );
			n = textureSampleGrad( surfN, repeatSampler, uv, li, duv1, duv2 );
			r = textureSampleGrad( surfR, repeatSampler, uv, li, duv1, duv2 );
		}
		// the corrugated sheets are corrugated in the geometry too: keep only a little of the ribs
		let strength = select( 1.0, 0.35, abs( L - 2.0 ) < 0.5 );
		var tn = n.xyz * 2.0 - 1.0;
		tn = vec3f( tn.xy * strength, max( tn.z, 0.05 ) );
		nrm = normalize( TBN * tn );
		alb = a.rgb * tint;
		ao = r.r; rough = r.g; metal = r.b;
		// the lantern's glass (prop layer 7: wooden_lantern_01_1): sooty panes that glow warm from dusk,
		// as the village lanterns do (the stall's local light switches on over the same range, App)
		if ( abs( L - 107.0 ) < 0.5 ) {
			let lum = dot( a.rgb, vec3f( 0.3, 0.55, 0.15 ) );
			let pane = smoothstep( 0.32, 0.16, lum ); // the dark panes, not the frame rims
			let nightOn = smoothstep( 0.15, 0.75, frame.night );
			let flicker = sin( frame.time * 9.0 + P.x * 40.0 ) * sin( frame.time * 5.3 + P.z * 13.0 ) * 0.12 + 0.9;
			// lit from inside: the flame shows through the soot, brighter in the middle of each pane
			// (the soot and smudges on the glass hold part of it back: a warm, uneven glow, not a light box)
			let soot = mix( 1.0, 0.35, smoothstep( 0.05, 0.22, lum ) );
			// the flame sits ~19 cm up in the middle: a hot core behind the glass, dim toward the frame
			let q = ( uv2 - vec2f( 0.0, 0.19 ) ) / vec2f( 0.05, 0.075 );
			let core = exp( - dot( q, q ) );
			let glow = vec3f( 1.0, 0.5, 0.18 ) * 0.35 + vec3f( 1.0, 0.72, 0.4 ) * 3.0 * core;
			s.emissive = glow * flicker * nightOn * pane * soot;
			// clean glass over the soot: glossy, a little lighter than the scan by day
			alb = mix( alb, alb * 1.6 + 0.02, pane );
			rough = mix( rough, 0.12, pane );
			metal = 0.0;
		}
		// sun-bleached and salty on the upward faces of the stall timber
		if ( L < 1.5 ) {
			let up = smoothstep( 0.3, 0.95, N0.y );
			let lum = dot( alb, vec3f( 0.3, 0.55, 0.15 ) );
			alb = mix( alb, vec3f( lum * 1.25 ), 0.28 * up + 0.12 );
			rough = clamp( rough + 0.06, 0.0, 1.0 );
		}
	} else if ( L > -1.5 ) {
		// crushed ice: packed chunks with glassy facets, clear meltwater in the gaps
		let c = skCells( P.xz * 34.0 + vec2f( P.y * 11.0 ) );
		let chunk = smoothstep( 0.0, 0.55, c.y - c.x );
		let frost = skFbm( P * 90.0 );
		let hgt = chunk * 0.004 + frost * 0.0008;
		nrm = skBump( P, N0, hgt );
		alb = mix( vec3f( 0.52, 0.62, 0.66 ), vec3f( 0.86, 0.92, 0.95 ), chunk * 0.8 + frost * 0.2 );
		rough = mix( 0.03, 0.28, frost * chunk );
		s.translucency = vec3f( 0.25, 0.32, 0.35 ) * chunk;
		s.clearcoat = 1.0; s.clearcoatRoughness = 0.02;
	} else if ( L > -3.5 || ( L > -7.5 && L < -6.5 ) ) {
		// painted: a sign on peeling timber, a chalkboard, or the scale's enamel dial
		let paint = textureSampleGrad( signs, clampSampler, uv2, 0, g2x, g2y );
		if ( L > -2.5 ) {
			let a = textureSampleGrad( surfA, repeatSampler, uv, 3, duv1, duv2 );
			let n = textureSampleGrad( surfN, repeatSampler, uv, 3, duv1, duv2 );
			let r = textureSampleGrad( surfR, repeatSampler, uv, 3, duv1, duv2 );
			// the board was painted over: a dark ground coat, the lettering, both chipped and sun-faded
			// chipped through where the old timber shows (its own peeling paint pattern) and at random
			let wear = skFbm( vec3f( uv2 * vec2f( 90.0, 45.0 ), 3.1 ) );
			let chips = smoothstep( 0.64, 0.74, wear + ( 0.45 - dot( a.rgb, vec3f( 0.33 ) ) ) * 0.35 );
			let ground = mix( vec3f( 0.03, 0.062, 0.068 ) * mix( 0.8, 1.15, wear ), a.rgb * tint, chips );
			let letters = paint.a * ( 1.0 - chips * 0.8 ) * mix( 0.78, 1.0, skNoise( vec3f( uv2 * 400.0, 0.0 ) ) );
			alb = mix( ground, paint.rgb * 0.82, letters );
			rough = mix( r.g, 0.62, ( 1.0 - chips ) * 0.8 );
			ao = r.r;
			var tn = n.xyz * 2.0 - 1.0;
			nrm = normalize( TBN * vec3f( tn.xy * mix( 1.0, 0.45, 1.0 - chips ), tn.z ) );
		} else if ( L > -3.5 ) {
			// slate with smeared chalk dust, the prices in chalk
			let dust = skFbm( vec3f( uv2 * vec2f( 18.0, 9.0 ), 0.0 ) );
			let chalk = paint.a * mix( 0.55, 1.0, skNoise( vec3f( uv2 * 900.0, 1.0 ) ) );
			alb = mix( vec3f( 0.028, 0.034, 0.031 ) + dust * 0.035, vec3f( 0.72, 0.72, 0.68 ), chalk );
			rough = 0.9;
		} else {
			// enamel dial face, a little crazed and yellowed
			let crazing = skNoise( vec3f( uv2 * 900.0, 2.0 ) );
			alb = mix( vec3f( 0.72, 0.68, 0.58 ), paint.rgb, paint.a ) * mix( 0.92, 1.0, crazing );
			rough = 0.22;
			s.clearcoat = 1.0; s.clearcoatRoughness = 0.06;
		}
	} else if ( L > -4.5 ) {
		// plain: colour, roughness and metalness from the vertex data; a little grime and pitting
		let g = skFbm( P * 14.0 );
		alb = tint * mix( 0.78, 1.04, g );
		rough = clamp( uv2.x + ( g - 0.5 ) * 0.2, 0.05, 1.0 );
		metal = uv2.y;
		if ( metal > 0.5 ) {
			let rust = smoothstep( 0.6, 0.8, skFbm( P * 40.0 + 3.0 ) );
			alb = mix( alb, vec3f( 0.2, 0.09, 0.04 ), rust * 0.8 );
			metal = mix( metal, 0.0, rust );
			rough = mix( rough, 0.9, rust );
		}
	} else if ( L > -5.5 ) {
		// three-strand laid rope: twisted strands, fibres, grime
		let strand = sin( ( uv.y * 3.0 + uv.x * 22.0 ) * 6.2831853 ) * 0.5 + 0.5;
		let fibre = skNoise( vec3f( uv.x * 900.0, uv.y * 40.0, 0.0 ) );
		alb = tint * mix( 0.55, 1.05, strand ) * mix( 0.85, 1.05, fibre ) * mix( 0.8, 1.0, skFbm( P * 6.0 ) );
		rough = 0.95;
		nrm = skBump( P, N0, strand * 0.0025 + fibre * 0.0003 );
	} else {
		// sun-faded painted float or fender: chalky, chipped to the white foam, streaky grime
		let fade = skFbm( P * 9.0 );
		let chip = smoothstep( 0.66, 0.74, skFbm( P * 55.0 + 2.0 ) );
		let streak = skFbm( vec3f( P.x * 30.0, P.y * 3.0, P.z * 30.0 ) );
		let chalky = mix( tint, vec3f( dot( tint, vec3f( 0.33 ) ) * 1.1 + 0.08 ), 0.3 + fade * 0.2 );
		alb = mix( chalky, vec3f( 0.78, 0.76, 0.7 ), chip ) * mix( 0.62, 1.0, smoothstep( 0.35, 0.7, streak ) );
		rough = mix( 0.55, 0.85, fade );
		nrm = skBump( P, N0, - chip * 0.0015 + fade * 0.0006 );
	}
	s.albedo = alb;
	s.roughness = rough;
	s.metalness = metal;
	s.ao = ao;
	s.normal = nrm;

return s;
}

struct LightAccum {
	directDiffuse: vec3f,
	directSpecular: vec3f,
	indirectDiffuse: vec3f,
	indirectSpecular: vec3f,
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
// Charlie sheen (Estevez & Kulla)
fn D_Charlie( roughness: f32, dotNH: f32 ) -> f32 {
	let a = roughness * roughness;
	let invA = 1.0 / a;
	let cos2h = dotNH * dotNH;
	let sin2h = max( 1.0 - cos2h, 0.0078125 );
	return ( 2.0 + invA ) * pow( sin2h, invA * 0.5 ) / ( 2.0 * PI );
}
fn V_Neubelt( dotNV: f32, dotNL: f32 ) -> f32 { return sat( 1.0 / ( 4.0 * ( dotNL + dotNV - dotNL * dotNV ) ) ); }
fn BRDF_Sheen( L: vec3f, V: vec3f, N: vec3f, color: vec3f, roughness: f32 ) -> vec3f {
	let H = normalize( L + V );
	return color * D_Charlie( roughness, sat( dot( N, H ) ) ) * V_Neubelt( sat( dot( N, V ) ), sat( dot( N, L ) ) );
}
// analytical approximation of the split-sum DFG term (Karis)
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

struct StallLightingFrame { sunDir:vec3f, sunColor:vec3f };
fn shadeSurface( s: Surface, P: vec3f, V: vec3f, pixel: vec2f, frame:StallLightingFrame ) -> vec3f {
	let N = s.normal;
	let rough = clamp( s.roughness, 0.03, 1.0 );
	let diffuseColor = s.albedo * ( 1.0 - s.metalness );
	let specF0 = mix( vec3f( 0.04 ) * s.specularIntensity, s.albedo, s.metalness );
	let specF90 = mix( s.specularIntensity, 1.0, s.metalness );
	var acc: LightAccum;
	acc.directDiffuse = vec3f( 0.0 ); acc.directSpecular = vec3f( 0.0 );
	acc.indirectDiffuse = vec3f( 0.0 ); acc.indirectSpecular = vec3f( 0.0 );

	// ---- sun / moon
	let L = frame.sunDir;
	let dotNL = sat( dot( N, L ) );
	var lightColor = frame.sunColor * hookDirectModulation( P, N );
	let geomN = N;
	// faces turned away from the sun with no transmission get nothing from it: skip the filter
	var shadow = 0.0;
	if ( dotNL > 0.0 || any( s.translucency > vec3f( 0.0 ) ) ) {
		shadow = sunShadow( hookShadowPosition( P, geomN, pixel ), geomN, pixel ) * hookContactShadow( P, N );
	}
	lightColor *= shadow;
	let irradiance = dotNL * lightColor;
	acc.directDiffuse += irradiance * diffuseColor * INV_PI;
	acc.directSpecular += irradiance * BRDF_GGX( L, V, N, specF0, specF90, rough );
	// thin-surface transmission (foliage): lit from behind as well
	acc.directDiffuse += s.translucency * lightColor;
	let ccN = s.clearcoatNormal;
	let ccNL = sat( dot( ccN, L ) );
	let ccSpec = ccNL * lightColor * BRDF_GGX( L, V, ccN, vec3f( 0.04 ), 1.0, clamp( s.clearcoatRoughness, 0.03, 1.0 ) );

	// ---- local lights (lanterns, windows, boat lights, flashlight)
	hookLocalLights( s, P, N, V, &acc );

	// ---- indirect: environment + ground bounce
	// (three's PhysicalLightingModel: env irradiance goes through the multiscatter-compensated
	// diffuse; the ground bounce is plain Lambert)
	let R = reflect( -V, N );
	let Rr = normalize( mix( R, N, rough * rough ) );
	let envIrr = hookEnvDiffuse( N ) * PI * s.envIntensity;
	let radiance = hookEnvSpecular( Rr, rough ) * s.envIntensity;
	var single = vec3f( 0.0 ); var multi = vec3f( 0.0 );
	multiscatter( N, V, specF0, specF90, rough, &single, &multi );
	let totalScatter = single + multi;
	let diffuseMS = diffuseColor * ( 1.0 - max( max( totalScatter.r, totalScatter.g ), totalScatter.b ) );
	acc.indirectSpecular += radiance * single + multi * envIrr * INV_PI;
	acc.indirectDiffuse += diffuseMS * envIrr * INV_PI + hookBounce( P, N ) * diffuseColor;

	// ambient occlusion (specular occlusion after Lagarde)
	let dotNV = sat( dot( N, V ) );
	let specAO = sat( pow( dotNV + s.ao, exp2( -16.0 * rough - 1.0 ) ) - 1.0 + s.ao );
	acc.indirectDiffuse *= s.ao;
	acc.indirectSpecular *= specAO;
	let amb = hookAmbientModulation( P, N );
	acc.indirectDiffuse *= amb;
	acc.indirectSpecular *= amb;

	var color = acc.directDiffuse + acc.directSpecular + acc.indirectDiffuse + acc.indirectSpecular;
	let ccNV = sat( dot( ccN, V ) );
	let Fcc = F_Schlick( vec3f( 0.04 ), 1.0, ccNV ) * s.clearcoat;
	let ccRad = hookEnvSpecular( reflect( -V, ccN ), clamp( s.clearcoatRoughness, 0.03, 1.0 ) ) * amb * specAO;
	color = color * ( 1.0 - Fcc ) + ( ccSpec * s.clearcoat + ccRad * Fcc );
	return color + s.emissive;
}
