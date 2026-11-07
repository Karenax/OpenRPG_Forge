// Standard/PBR surface decoding remains with Bevy. Lighting uses the common
// source functions above; emissive stays separate from scene illumination.
#import bevy_pbr::{pbr_types, mesh_view_bindings::view}
fn sourceSurfaceLighting(input:pbr_types::PbrInput)->vec4f {
    if ((input.material.flags & pbr_types::STANDARD_MATERIAL_FLAGS_UNLIT_BIT)!=0u) {
        return input.material.base_color;
    }
    var s:SourceSurface;
    s.albedo=input.material.base_color.rgb;
    s.normal=input.N;
    s.roughness=input.material.perceptual_roughness;
    s.metalness=input.material.metallic;
    s.specularIntensity=4.0*input.material.reflectance.r*input.material.reflectance.r;
    s.translucency=vec3f(0.0);
    s.ao=input.diffuse_occlusion.r;
    let P=input.world_position.xyz;
    let N=normalize(input.N);
    let V=normalize(input.V);
    let visibility=terrainSunShadowAt(P)*cloudsShadow(P.xz)*sunShadow(P,input.world_normal,input.frag_coord.xy);
    let direct=sourceDirectLight(s.albedo,s.roughness,s.metalness,s.specularIntensity,s.translucency,N,V,
        sceneLighting.sunDirection.xyz,sceneLighting.sunColor.xyz*visibility);
    let ambient=sourceEnvironmentLight(s,N,V);
    let emissive=input.material.emissive.rgb*mix(1.0,view.exposure,input.material.emissive.a);
    // This host admits dry source scenes. Keep the acquired medium equation,
    // with neutral optical coefficients, independently of solar/cloud shadows.
    let local=sourceLocalLights(s,P,N,V,0.0,vec3f(0.0),vec3f(0.0));
    return vec4f(direct+ambient+nativeLocalLighting(input)+local+emissive,input.material.base_color.a);
}
