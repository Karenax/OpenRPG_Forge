
#ifdef VILLAGE_KIND_7
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var assetColor: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var assetNormal: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var assetOrm: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var assetAo: texture_2d<f32>;
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


fn sourceVertex(v: ptr<function,SourceVertex>, o: ptr<function,Output>) {
    o.vTint = v.tint;
    o.vData = v.vdata;
}
fn sourceSurface(input: FragInput, s: ptr<function,Surface>) {
    let flags = u32(settings.flags.w);
    if ((flags & 1u) != 0u) {
        let rgba = textureSample(assetColor, smpAnisoRepeat, input.uv);
        s.albedo *= rgba.rgb;
        s.alpha *= rgba.a;
    }
    s.albedo *= input.vs.vTint;
    if ((flags & 2u) != 0u) {
        let n = textureSample(assetNormal, smpAnisoRepeat, input.uv).xyz * 2.0 - 1.0;
        s.normal = perturbNormalByMap(input.P, input.N, input.uv, normalize(n));
    }
    if ((flags & 4u) != 0u) {
        let orm = textureSample(assetOrm, smpAnisoRepeat, input.uv);
        s.roughness *= orm.g;
        s.metalness *= orm.b;
    }
    if ((flags & 8u) != 0u) {
        s.ao = textureSample(assetAo, smpAnisoRepeat, input.uv).r;
    }
    s.emissive *= mat.emissiveIntensity;
    s.envIntensity = mat.envMapIntensity;
}
#endif
