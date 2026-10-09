// Standard isotropic source Deferred projection and sky specular. Authored
// values remain untouched; only the source G-buffer lighting values are encoded.
// Caller supplies SOURCE-world normal/view and actual scene occlusion/fresnel.
// Dependencies: source_sky_cube, source_preintegrated_brdf, source_deferred_diffuse.
fn source_deferred_round_even(x:f32)->f32 {
    let lower=floor(x);
    let fraction=x-lower;
    let up=fraction>0.5 || (fraction==0.5 && (u32(lower)&1u)!=0u);
    return select(lower,lower+1.0,up);
}
fn source_deferred_unorm8(x:f32)->f32 {
    return source_deferred_round_even(clamp(x,0.0,1.0)*255.0)/255.0;
}
fn source_deferred_normal_roughness(original_source_normal:vec3<f32>,authored_roughness:f32)->vec4<f32> {
    var n=normalize(original_source_normal);
    // NormalBuffer: preserve the source Z sign at octahedron seams.
    let negative_z=(bitcast<u32>(n.z)&0x80000000u)!=0u;
    n.z=select(max(1.0/1024.0,abs(n.z)),-max(1.0/1024.0,abs(n.z)),negative_z);
    n=n/max(dot(abs(n),vec3<f32>(1.0)),0.000001);
    let t=clamp(-n.z,0.0,1.0);
    let oct=n.xy+select(vec2<f32>(-t),vec2<f32>(t),n.xy>=vec2<f32>(0.0));
    // SDK Packing: truncation of 4095.5, not a replacement round operation.
    let packed=vec2<u32>(clamp(oct*0.5+vec2<f32>(0.5),vec2<f32>(0.0),vec2<f32>(1.0))*4095.5);
    let f=vec2<f32>(packed)/4095.0*2.0-vec2<f32>(1.0);
    var decoded=vec3<f32>(f,1.0-select(f.x,-f.x,f.x<0.0)-select(f.y,-f.y,f.y<0.0));
    let fold=max(-decoded.z,0.0);
    decoded=vec3<f32>(decoded.xy+select(vec2<f32>(fold),vec2<f32>(-fold),decoded.xy>=vec2<f32>(0.0)),decoded.z);
    return vec4<f32>(normalize(decoded),source_deferred_unorm8(authored_roughness));
}
fn source_deferred_fresnel0(authored_f0:vec3<f32>)->vec3<f32> {
    let srgb=clamp(1.055*pow(abs(authored_f0),vec3<f32>(0.416666667))-vec3<f32>(0.055),vec3<f32>(0.0),vec3<f32>(1.0));
    let c=vec3<f32>(source_deferred_unorm8(srgb.x),source_deferred_unorm8(srgb.y),source_deferred_unorm8(srgb.z));
    return c*(c*(c*0.305306011+vec3<f32>(0.682171111))+vec3<f32>(0.012522878));
}
fn source_deferred_specular_ao(ndotv:f32,visibility:f32,roughness:f32)->f32 {
    return clamp(pow(abs(ndotv+visibility),exp2(-16.0*roughness-1.0))-1.0+visibility,0.0,1.0);
}
fn source_deferred_material_specular_ao(authored_specular_ao:f32)->f32 {
    // Standard, non-lightmapped PackFloatInt8bit(value,0,2), hardware RT alpha,
    // then UnpackFloatInt8bit. Seven bits carry the original specular visibility.
    let encoded=source_deferred_unorm8((127.0/255.0)*authored_specular_ao);
    let flag=u32(encoded/(128.0/255.0)+1.0/255.0);
    return clamp((encoded-(128.0/255.0)*f32(flag))/(127.0/255.0),0.0,1.0);
}
// The 756 captured source GPU factors use 8-bit fractional interpolation.
// Reproduce that sampler projection; retain the shared table and its texels.
fn source_deferred_preintegrated_specular_sample(ndotv:f32,r:f32)->vec4<f32> {
    let xy=vec2<f32>(sqrt(clamp(ndotv,0.0001,1.0)),r)*63.0;
    let base=vec2<i32>(floor(xy));
    let f=fract(xy)*256.0;
    let weight=vec2<f32>(source_deferred_round_even(f.x),source_deferred_round_even(f.y))/256.0;
    let lower=vec2<i32>(0);let upper=vec2<i32>(63);
    let a=textureLoad(source_preintegrated_brdf_image,clamp(base,lower,upper),0);
    let b=textureLoad(source_preintegrated_brdf_image,clamp(base+vec2<i32>(1,0),lower,upper),0);
    let c=textureLoad(source_preintegrated_brdf_image,clamp(base+vec2<i32>(0,1),lower,upper),0);
    let d=textureLoad(source_preintegrated_brdf_image,clamp(base+vec2<i32>(1,1),lower,upper),0);
    return mix(mix(a,b,weight.x),mix(c,d,weight.x),weight.y);
}
fn source_deferred_reflection_direction(n:vec3<f32>,v:vec3<f32>,r:f32,ndotv:f32)->vec3<f32> {
    let reflection=reflect(-v,n);
    let a=1.0-r*r;
    let factor=(sqrt(a)+r*r)*clamp(a*a+a*ndotv*ndotv,0.0,1.0);
    let dominant=mix(n,reflection,factor);
    let roughness=r*r;
    return mix(dominant,reflection,clamp(smoothstep(0.0,1.0,roughness*roughness),0.0,1.0));
}
fn source_deferred_sky_specular(
    original_source_normal:vec3<f32>,original_source_view:vec3<f32>,authored_roughness:f32,
    authored_f0:vec3<f32>,authored_specular_ao:f32,scene_visibility:f32,
    source_specular_occlusion_blend:f32,f90:f32,
)->vec3<f32> {
    let normal_roughness=source_deferred_normal_roughness(original_source_normal,authored_roughness);
    let n=normal_roughness.xyz;
    let v=normalize(original_source_view);
    let r=normal_roughness.w;
    let ndotv=max(dot(n,v),0.0001);
    let f0=source_deferred_fresnel0(authored_f0);
    let direction=source_deferred_reflection_direction(n,v,r,ndotv);
    let lod=(r*(1.7-0.7*r))*6.0;
    let sampled=source_deferred_preintegrated_specular_sample(ndotv,r);
    let fgd=source_preintegrated_brdf_specular(sampled,f0,f90);
    let screen_ao=mix(1.0,source_deferred_specular_ao(ndotv,scene_visibility,r*r),source_specular_occlusion_blend);
    let ao=source_gtao_multibounce(min(source_deferred_material_specular_ao(authored_specular_ao),screen_ao),f0);
    let energy=vec3<f32>(1.0)+f0*(1.0/sampled.y-1.0);
    return fgd*source_prefiltered_sky_sample(direction,0,lod).xyz*ao*energy;
}
