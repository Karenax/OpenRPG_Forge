// Shared source scene table; exact 64x64 linear RGBA32Float, no sampler.
// Admission must reject a missing image. This helper has no native BRDF fallback.
@group(#{MATERIAL_BIND_GROUP}) @binding(122) var source_preintegrated_brdf_image:texture_2d<f32>;

fn source_preintegrated_brdf_sample(ndotv:f32, perceptual_roughness:f32)->vec4<f32> {
    let xy=vec2<f32>(sqrt(clamp(ndotv,0.0001,1.0)),perceptual_roughness)*63.0;
    let base=vec2<i32>(floor(xy));
    let fraction=fract(xy);
    let lower=vec2<i32>(0);
    let upper=vec2<i32>(63);
    let a=textureLoad(source_preintegrated_brdf_image,clamp(base,lower,upper),0);
    let b=textureLoad(source_preintegrated_brdf_image,clamp(base+vec2<i32>(1,0),lower,upper),0);
    let c=textureLoad(source_preintegrated_brdf_image,clamp(base+vec2<i32>(0,1),lower,upper),0);
    let d=textureLoad(source_preintegrated_brdf_image,clamp(base+vec2<i32>(1,1),lower,upper),0);
    return mix(mix(a,b,fraction.x),mix(c,d,fraction.x),fraction.y);
}

fn source_preintegrated_brdf_diffuse(sampled:vec4<f32>)->f32 {
    return sampled.z+0.5;
}

fn source_preintegrated_brdf_specular(sampled:vec4<f32>, f0:vec3<f32>, f90:f32)->vec3<f32> {
    return (vec3<f32>(f90)-f0)*sampled.x+f0*sampled.y;
}
