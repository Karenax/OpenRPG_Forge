
 {
  use bevy::render::render_resource::*;
  use bevy::render::renderer::RenderQueue;
  let (device,queue,texture,source_bias_rows)={
   let rw=app.get_sub_app_mut(RenderApp).unwrap().world_mut();
   let device=rw.resource::<RenderDevice>().clone();let queue=rw.resource::<RenderQueue>().clone();
   let mut query=rw.query::<&bevy::pbr::ViewShadowBindings>();let bindings=query.iter(rw).collect::<Vec<_>>();assert_eq!(bindings.len(),1,"Ambiguous native shadow bindings");let texture=bindings[0].directional_light_depth_texture.clone();
   let phases=rw.resource::<bevy::render::render_phase::ViewBinnedRenderPhases<bevy::pbr::Shadow>>();
   let mut active=std::collections::HashSet::new();for phase in phases.0.values(){
    for key in phase.multidrawable_meshes.keys(){active.insert(key.pipeline);}
    for (key,_) in phase.batchable_meshes.keys().chain(phase.unbatchable_meshes.keys()).chain(phase.non_mesh_items.keys()){active.insert(key.pipeline);}
   }
   let cache=rw.resource::<PipelineCache>();let mut rows=Vec::new();
   for pipeline_id in active{
    let d=cache.get_render_pipeline_descriptor(pipeline_id);
    if !shader_ids.contains(&d.vertex.shader.id()){continue;}
    let shadow=d.vertex.shader_defs.iter().any(|def|matches!(def,bevy::shader::ShaderDefVal::Bool(name,true) if name=="SOURCE_GRAPH_SHADOW_PASS"));
    let ortho=d.vertex.shader_defs.iter().any(|def|matches!(def,bevy::shader::ShaderDefVal::Bool(name,true) if name=="VIEW_PROJECTION_ORTHOGRAPHIC"));
    if !shadow||!ortho{continue;}
    let depth=d.depth_stencil.as_ref().ok_or("Native source shadow lacks depth descriptor")?;
    assert!(matches!(cache.get_render_pipeline_state(pipeline_id),CachedPipelineState::Ok(_)));assert_eq!(depth.format,TextureFormat::Depth32Float);assert_eq!(depth.bias.constant,-1);assert_eq!(depth.bias.slope_scale.to_bits(),(-2.5f32).to_bits());assert_eq!(depth.bias.clamp,0.0);
    rows.push(serde_json::json!({"source_shadow":shadow,"orthographic":ortho,"ready":true,"format":format!("{:?}",depth.format),"constant":depth.bias.constant,"slope_scale":depth.bias.slope_scale,"clamp":depth.bias.clamp,"depth_compare":format!("{:?}",depth.depth_compare)}));
   }
   if view==0{assert!(!rows.is_empty(),"No active source orthographic raster pipeline");}else{assert!(rows.is_empty(),"Disabled shadow view still draws source shadow pipelines");}(device,queue,texture,rows)
  };
  let dimensions=texture.size();assert_eq!((dimensions.width,dimensions.height,dimensions.depth_or_array_layers),(512,512,4));assert_eq!(texture.format(),TextureFormat::Depth32Float);
  let size=4194304u64;let (staging,stage_submissions)={let rw=app.get_sub_app(RenderApp).unwrap().world();let probe=rw.resource::<source_depth_stage::Probe>();(probe.staging.clone(),probe.submissions.load(std::sync::atomic::Ordering::SeqCst))};assert!(stage_submissions>0,"Depth stage did not execute");
  let slice=staging.slice(..);let (sender,receiver)=std::sync::mpsc::channel();slice.map_async(MapMode::Read,move|result|{let _=sender.send(result.is_ok());});
  let deadline=Instant::now()+Duration::from_secs(30);loop{device.poll(PollType::Poll)?;match receiver.try_recv(){Ok(mapped)=>{assert!(mapped);break;},Err(std::sync::mpsc::TryRecvError::Empty)=>{},Err(error)=>return Err(error.into())};if Instant::now()>deadline{return Err("Native shadow depth map timeout".into());}std::thread::sleep(Duration::from_millis(5));}
  let data=slice.get_mapped_range();for sample in data.chunks_exact(4){let value=f32::from_le_bytes(sample.try_into().unwrap());assert!(value.is_finite()&&(0.0..=1.0).contains(&value));}
  std::fs::write(folder.join(format!("NATIVE-SHADOW-DEPTH-{view}.float32")),&data)?;drop(data);staging.unmap();
  std::fs::write(folder.join(format!("NATIVE-SHADOW-DEPTH-{view}.json")),serde_json::to_vec_pretty(&serde_json::json!({"view":view,"width":512,"height":512,"layers":4,"format":"Depth32Float","bytes":size,"actual_gpu_depth_readback":true,"copied_inside_render_schedule_after_shadow_pass":true,"stage_submissions":stage_submissions,"raster_pipelines":source_bias_rows,"full_import":false}))?)?;
 }
