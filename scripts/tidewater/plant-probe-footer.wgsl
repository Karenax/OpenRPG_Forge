@vertex
fn vertex(v:ProbeVertex)->ProbeOut {
    setupSource();
    let model=mesh_functions::get_world_from_local(v.instance);
    var source:SourceVertex;
    source.position=v.position; source.normal=v.normal; source.uv=v.uv;
    source.model=model; source.iPos=probe.iPos; source.iDat=probe.iDat;
    source.aVeg=v.aVeg; source.aMat=v.aMat; source.aLobe=v.aLobe;
    var o:ProbeOut;
    sourceVertex(&source,&o);
    o.clip=position_world_to_clip(source.worldPos);
    o.P=source.worldPos; o.normal=source.worldNormal; o.uv=v.uv;
    return o;
}
@fragment
fn fragment(vs:ProbeOut,@builtin(front_facing) front:bool)->@location(0) vec4f {
    setupSource();
    var input:FragInput;
    input.vs=vs; input.P=vs.P; input.N=normalize(select(-vs.normal,vs.normal,front));
    input.V=normalize(view.world_position-vs.P); input.uv=vs.uv;
    input.front=front; input.pixel=vs.clip.xy;
    var surface:SourceSurface;
    surface.ao=1.0;
    sourceSurface(input,&surface);
    // Diagnostic coverage pass: preserves the source's alpha mask and vertex
    // deformation, removing lighting from a paired silhouette comparison.
    if probe.lodEndFrame.w > 0.5 { return vec4f(1.0); }
    var pbr=pbr_types::pbr_input_new();
    pbr.frag_coord=vs.clip;
    pbr.world_position=vec4f(vs.P,1.0); pbr.world_normal=input.N;
    pbr.N=surface.normal; pbr.V=input.V;
    pbr.material.base_color=vec4f(surface.albedo,1.0);
    pbr.material.perceptual_roughness=surface.roughness;
    pbr.material.metallic=surface.metalness;
    pbr.diffuse_occlusion=vec3f(surface.ao);
    // Bevy lighting is a temporary integration boundary. Upstream's shadowed
    // translucency/specular and sky illumination are NOT yet reproduced.
    var color=apply_pbr_lighting(pbr);
    return main_pass_post_lighting_processing(pbr,color);
}
