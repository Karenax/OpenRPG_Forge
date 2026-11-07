"""Reuse the admitted importer and add actual camera/mesh/readback rendering."""
from pathlib import Path
import subprocess, sys

S=Path(__file__).parent
R=S.parents[1]
target=R/'crates/forge_engine/examples/verify_source_glb_draw.rs'
assert not target.exists()
text=(R/'crates/forge_engine/examples/verify_source_glb_resources.rs').read_text(encoding='utf-8')
text=text.replace('//! This performs no drawing and does not qualify source lighting or sampling.',
    '//! Draw the real source material; compare separately before claiming source fidelity.')
text=text.replace('renderer::{RenderAdapterInfo, RenderDevice}', 'renderer::{RenderAdapter, RenderAdapterInfo, RenderDevice}')
text=text.replace('SourceGraphMaterial, SourceGraphPlugin, SourceGraphShaderRegistry, SourceGraphUniforms,',
    'SourceGraphMaterial, SourceGraphPlugin, SourceGraphShaderRegistry, SourceGraphUniforms, SourceGraphSceneSampling,')
anchor='    let raw = std::fs::read('
assert text.count(anchor)==1
context='''    let context: serde_json::Value = serde_json::from_slice(&std::fs::read(input["source_context"].as_str().ok_or("Source context required")?)?)?;
    let vector = |field: &str| -> Vec3 {
        let v=&context[field]; Vec3::new(v["x"].as_f64().unwrap() as f32,v["y"].as_f64().unwrap() as f32,v["z"].as_f64().unwrap() as f32)
    };
'''
text=text.replace(anchor,context+anchor)
text=text.replace('source_world_from_target_world: Mat4::IDENTITY,','source_world_from_target_world: matrix("source_object_from_target_object")?,')
text=text.replace('source_absolute_world_from_target_world: Mat4::IDENTITY,','source_absolute_world_from_target_world: matrix("source_object_from_target_object")?,')
text=text.replace('let program = source_graph_program_from_glb(contract, &glb.views)?;', '''let policy = match context["anisotropic_filtering"].as_str().ok_or("Source sampling context absent")? {
        "ForceEnable" => SourceGraphSceneSampling::UnityForceEnableD3d,
        "Enable" => SourceGraphSceneSampling::Authored,
        other => return Err(format!("Unqualified scene sampling policy: {other}").into()),
    };
    let adapter=app.get_sub_app(RenderApp).unwrap().world().resource::<RenderAdapter>();
    let program = source_graph_program_from_glb(contract, &glb.views)?.with_scene_sampling(policy,Some(adapter))?;''')
text=text.replace('let mut mesh_rows = Vec::new();','let mut mesh_rows = Vec::new();\n    let mut mesh_handles = Vec::new();')
text=text.replace('meshes.add(imported.mesh);','mesh_handles.push(meshes.add(imported.mesh));')
anchor='    let result = serde_json::json!('
assert text.count(anchor)==1
draw='''    let adapter_name = info.name.clone(); let backend=format!("{:?}",info.backend);
    let native_binding_result=serde_json::json!({"adapter":adapter_name,"backend":backend,"textures":rows,"meshes":mesh_rows,
        "source_uniform_bytes":uniform_bytes.len(),"binding_ids":binding_ids,"gpu_validation_errors":captured});
    std::fs::write(root.join("RESOURCE-RESULT.v1.json"),serde_json::to_vec_pretty(&native_binding_result)?)?;
    let world=app.world_mut();
    world.insert_resource(GlobalAmbientLight::NONE);
    let material_handle=world.resource_mut::<Assets<SourceGraphMaterial>>().add(material);
    world.spawn((Mesh3d(mesh_handles[0].clone()),MeshMaterial3d(material_handle),Transform::IDENTITY));
    let mut target=Image::new_target_texture(512,512,TextureFormat::Rgba16Float,None);
    target.texture_descriptor.usage|=TextureUsages::COPY_SRC;
    let target_handle=world.resource_mut::<Assets<Image>>().add(target);
    let reflection=Mat4::from_scale(Vec3::new(-1.,1.,1.));
    let centre=reflection.transform_point3(vector("bounds_center"));
    let position=reflection.transform_point3(vector("camera_position"));
    let camera_transform=Transform::from_translation(position).looking_at(centre,Vec3::Y);
    world.spawn((Camera3d::default(),Camera{clear_color:ClearColorConfig::Custom(Color::linear_rgb(0.025,0.025,0.025)),..default()},
        bevy::camera::RenderTarget::Image(target_handle.clone().into()),bevy::camera::Hdr,
        Projection::Perspective(PerspectiveProjection{fov:(context["fov"].as_f64().unwrap() as f32).to_radians(),aspect_ratio:1.,
            near:context["near"].as_f64().unwrap() as f32,far:context["far"].as_f64().unwrap() as f32,..default()}),
        bevy::camera::Exposure{ev100:context["exposure_ev"].as_f64().unwrap() as f32},
        bevy::core_pipeline::tonemapping::Tonemapping::None,Msaa::Off,
        bevy::core_pipeline::prepass::DepthPrepass,camera_transform));
    world.spawn((DirectionalLight{illuminance:context["light_intensity"].as_f64().unwrap() as f32,shadows_enabled:false,..default()},camera_transform));
    let pixels:Arc<Mutex<Option<Vec<u8>>>>=Arc::new(Mutex::new(None)); let received=pixels.clone();
    world.spawn(bevy::render::gpu_readback::Readback::texture(target_handle)).observe(
        move |event:On<bevy::render::gpu_readback::ReadbackComplete>| {*received.lock().unwrap()=Some(event.data.clone());});
    let deadline=Instant::now()+Duration::from_secs(90);
    let mut frames=0; let mut previous:Option<Vec<u8>>=None; let mut stable=0;
    let captured_image=loop {
        app.update();frames+=1;
        device.wgpu_device().poll(PollType::Poll)?;
        let cache=app.get_sub_app(RenderApp).unwrap().world().resource::<PipelineCache>();
        let pending=cache.waiting_pipelines().count();
        if frames>30 && pending==0 {
            if let Some(bytes)=pixels.lock().unwrap().take() {
                assert_eq!(bytes.len(),512*512*8);
                let varied=bytes.chunks_exact(8).filter(|p|*p!=&bytes[..8]).count();
                if varied>100 {
                    if previous.as_ref()==Some(&bytes) {stable+=1;} else {stable=0;}
                    previous=Some(bytes.clone());
                    if stable>=2 {break bytes;}
                }
            }
        }
        if Instant::now()>deadline {return Err("No stable nonblank source-material camera draw in 90 seconds".into());}
        std::thread::sleep(Duration::from_millis(20));
    };
    assert!(errors.lock().unwrap().is_empty(),"GPU validation errors after drawing");
    std::fs::write(root.join("FORGE-LINEAR.rgba16f"),&captured_image)?;
'''
text=text.replace(anchor,draw+anchor)
start=text.index('    let result = serde_json::json!(')
end=text.index('    std::fs::write(\n        root.join("NATIVE-RESULT.v1.json")',start)
text=text[:start]+'''    let result=serde_json::json!({"status":"actual_source_glb_lod0_camera_draw_and_stable_gpu_readback",
        "adapter":adapter_name,"backend":backend,"lod":0,"frames":frames,"consecutive_equal_readbacks":stable+1,
        "native_images":10,"native_mips":120,"authored_texture_substitution":false,
        "scene_sampling_policy":format!("{policy:?}"),"source_context":input["source_context"],
        "camera_position":position.to_array(),"camera_target":centre.to_array(),"camera_ev100":context["exposure_ev"],
        "source_state":"Actual common material state; depth equal with original custom depth prepass",
        "gpu_draw_executed":true,"image":"FORGE-LINEAR.rgba16f","image_bytes":captured_image.len(),
        "target_format":"Rgba16Float","tonemapping":"None","source_reference_tonemapping":"None",
        "source_ambient_light_qualified":false,"source_sampling_parity_qualified":false,"source_lighting_parity_qualified":false,
        "source_render_parity_qualified":false,"forge_editor_verified":false,"full_import":false});
'''+text[end:]
target.write_text(text,encoding='utf-8')
print(str(target))
