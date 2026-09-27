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
    var pbr=pbr_types::pbr_input_new();
    pbr.frag_coord=vs.position;pbr.world_position=vec4f(vs.P,1.0);
    pbr.world_normal=input.N;pbr.N=s.normal;pbr.V=input.V;
    pbr.material.base_color=vec4f(s.albedo,s.alpha);
    pbr.material.emissive=vec4f(s.emissive,1.0);
    pbr.material.perceptual_roughness=s.roughness;pbr.material.metallic=s.metalness;
    pbr.diffuse_occlusion=vec3f(s.ao);
    var color=main_pass_post_lighting_processing(pbr,apply_pbr_lighting(pbr));
    color.a=s.alpha;return color;
}
