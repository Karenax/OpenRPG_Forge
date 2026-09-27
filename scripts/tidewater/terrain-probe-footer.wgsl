@fragment
fn fragment(vs:VertexOutput)->@location(0) vec4f {
    frame.cameraPos=view.world_position;
    frame.time=settings.wind.w; frame.windDir=settings.wind.xy;
    frame.windSpeed=settings.wind.z; frame.sunDir=settings.sun.xyz;
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
    var pbr=pbr_types::pbr_input_new();
    pbr.frag_coord=vs.position; pbr.world_position=vs.world_position;
    pbr.world_normal=input.N; pbr.N=surface.normal; pbr.V=input.V;
    pbr.material.base_color=vec4f(surface.albedo,1.0);
    pbr.material.perceptual_roughness=surface.roughness;
    pbr.material.metallic=surface.metalness;
    pbr.diffuse_occlusion=vec3f(surface.ao);
    return main_pass_post_lighting_processing(pbr,apply_pbr_lighting(pbr));
}
