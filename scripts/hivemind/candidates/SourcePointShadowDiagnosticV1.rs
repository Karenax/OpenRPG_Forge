 {
  use bevy::render::render_resource::*;
  use bevy::render::renderer::RenderQueue;
  let rw=app.get_sub_app_mut(RenderApp).unwrap().world_mut();
  let device=rw.resource::<RenderDevice>().clone();let queue=rw.resource::<RenderQueue>().clone();
  let shader=device.create_and_validate_shader_module(ShaderModuleDescriptor{label:Some("source-point-record-probe"),source:ShaderSource::Wgsl(std::fs::read_to_string(folder.join("point-record-probe.wgsl"))?.into())});
  let pipeline=device.create_compute_pipeline(&RawComputePipelineDescriptor{label:Some("source-point-record-probe"),layout:None,module:&shader,entry_point:Some("main"),compilation_options:default(),cache:None});
  let output=device.create_buffer(&BufferDescriptor{label:Some("source-point-record-output"),size:80,usage:BufferUsages::STORAGE|BufferUsages::COPY_SRC,mapped_at_creation:false});
  let staging=device.create_buffer(&BufferDescriptor{label:Some("source-point-record-readback"),size:80,usage:BufferUsages::COPY_DST|BufferUsages::MAP_READ,mapped_at_creation:false});
  let meta=rw.resource::<bevy::pbr::GlobalClusterableObjectMeta>();
  assert!(meta.gpu_clustered_lights.max_clustered_lights().is_none(),"Point probe requires actual storage buffer");
  assert_eq!(meta.entity_to_index.len(),1,"Point probe requires the sole clustered light; no assumed slot with multiple lights");
  assert_eq!(*meta.entity_to_index.values().next().unwrap(),0);
  let binding=meta.gpu_clustered_lights.binding().ok_or("Point GPU buffer absent")?;
  let layout=pipeline.get_bind_group_layout(0);
  let bindings=device.wgpu_device().create_bind_group(&BindGroupDescriptor{label:Some("source-point-record-bindings"),layout:&layout,entries:&[BindGroupEntry{binding:0,resource:binding},BindGroupEntry{binding:1,resource:output.as_entire_binding()}]});
  let mut encoder=device.create_command_encoder(&CommandEncoderDescriptor{label:Some("source-point-record-copy")});
  {let mut pass=encoder.begin_compute_pass(&ComputePassDescriptor{label:Some("source-point-record-copy"),timestamp_writes:None});pass.set_pipeline(&pipeline);pass.set_bind_group(0,&bindings,&[]);pass.dispatch_workgroups(1,1,1);}
  encoder.copy_buffer_to_buffer(&output,0,&staging,0,80);queue.submit([encoder.finish()]);
  let slice=staging.slice(..);let(sender,receiver)=std::sync::mpsc::channel();slice.map_async(MapMode::Read,move|result|{let _=sender.send(result.is_ok());});
  let deadline=Instant::now()+Duration::from_secs(30);loop{device.poll(PollType::Poll)?;match receiver.try_recv(){Ok(mapped)=>{assert!(mapped);break;},Err(std::sync::mpsc::TryRecvError::Empty)=>{},Err(e)=>return Err(e.into())};if Instant::now()>deadline{return Err("Point record GPU readback timeout".into());}std::thread::sleep(Duration::from_millis(5));}
  let data=slice.get_mapped_range();assert_eq!(data.len(),80);
  std::fs::write(folder.join(format!("POINT-RECORD-GPU-{view}.bin")),&data)?;drop(data);staging.unmap();
  let mut query=rw.query::<(&bevy::pbr::ShadowView,&bevy::render::view::ExtractedView)>();
  let phases=rw.resource::<bevy::render::render_phase::ViewBinnedRenderPhases<bevy::pbr::Shadow>>();
  let rows=query.iter(rw).filter(|(_,v)|v.retained_view_entity.main_entity==point_entity.into()&&v.clip_from_view.w_axis.w==0.).map(|(_,v)|{
   let phase=phases.0.get(&v.retained_view_entity);
   serde_json::json!({"face":v.retained_view_entity.subview_index,"shadow_phase_exists":phase.is_some(),"multidraw_bins":phase.map(|p|p.multidrawable_meshes.len()),"batchable_bins":phase.map(|p|p.batchable_meshes.len()),"unbatchable_bins":phase.map(|p|p.unbatchable_meshes.len()),"non_mesh_bins":phase.map(|p|p.non_mesh_items.len()),"clip_from_view":v.clip_from_view.to_cols_array(),"world_from_view":v.world_from_view.to_matrix().to_cols_array()})
  }).collect::<Vec<_>>();
  assert_eq!(rows.len(),6,"Expected six actual source point shadow views");
  let mut q=rw.query::<&bevy::pbr::ViewShadowBindings>();let textures=q.iter(rw).map(|b|serde_json::json!({"width":b.point_light_depth_texture.width(),"height":b.point_light_depth_texture.height(),"layers":b.point_light_depth_texture.depth_or_array_layers(),"format":format!("{:?}",b.point_light_depth_texture.format())})).collect::<Vec<_>>();assert_eq!(textures.len(),1);
  std::fs::write(folder.join(format!("POINT-SHADOW-PHASES-{view}.json")),serde_json::to_vec_pretty(&serde_json::json!({"view":view,"clustered_entity_mapping_count_cpu":1,"clustered_index":0,"actual_gpu_record_bytes":80,"gpu_buffer_read_by_compute":true,"point_shadow_views":rows,"shadow_texture":textures,"bin_counts_are_not_draw_counts":true,"full_import":false}))?)?;
 }
