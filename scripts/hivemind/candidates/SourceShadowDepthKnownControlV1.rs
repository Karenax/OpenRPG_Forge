  if view==0{
   let control=device.create_texture(&TextureDescriptor{label:Some("known-depth-probe-control"),size:Extent3d{width:512,height:512,depth_or_array_layers:4},mip_level_count:1,sample_count:1,dimension:TextureDimension::D2,format:TextureFormat::Depth32Float,usage:TextureUsages::RENDER_ATTACHMENT|TextureUsages::TEXTURE_BINDING,view_formats:&[]});
   let output=device.create_buffer(&BufferDescriptor{label:Some("known-depth-probe-output"),size:4194304,usage:BufferUsages::STORAGE|BufferUsages::COPY_SRC,mapped_at_creation:false});
   let known_staging=device.create_buffer(&BufferDescriptor{label:Some("known-depth-probe-readback"),size:4194304,usage:BufferUsages::COPY_DST|BufferUsages::MAP_READ,mapped_at_creation:false});
   let mut encoder=device.create_command_encoder(&CommandEncoderDescriptor{label:Some("known-depth-control-clear-and-copy")});
   for layer in 0..4{let layer_view=control.create_view(&TextureViewDescriptor{dimension:Some(TextureViewDimension::D2),base_array_layer:layer,array_layer_count:Some(1),..default()});
    let _pass=encoder.begin_render_pass(&RenderPassDescriptor{label:Some("known-depth-control-clear"),color_attachments:&[],depth_stencil_attachment:Some(RenderPassDepthStencilAttachment{view:&layer_view,depth_ops:Some(Operations{load:LoadOp::Clear((layer+1) as f32*0.25),store:StoreOp::Store}),stencil_ops:None}),timestamp_writes:None,occlusion_query_set:None,multiview_mask:None});
   }
   {let rw=app.get_sub_app(RenderApp).unwrap().world();let probe=rw.resource::<source_depth_stage::Probe>();let atlas=control.create_view(&TextureViewDescriptor{dimension:Some(TextureViewDimension::D2Array),..default()});let layout=probe.pipeline.get_bind_group_layout(0);
    let group=device.wgpu_device().create_bind_group(&BindGroupDescriptor{label:Some("known-depth-probe-bindings"),layout:&layout,entries:&[BindGroupEntry{binding:0,resource:BindingResource::TextureView(&atlas)},BindGroupEntry{binding:1,resource:output.as_entire_binding()}]});
    {let mut pass=encoder.begin_compute_pass(&ComputePassDescriptor{label:Some("known-depth-probe-copy"),timestamp_writes:None});pass.set_pipeline(&probe.pipeline);pass.set_bind_group(0,&group,&[]);pass.dispatch_workgroups(64,64,4);}
   }
   encoder.copy_buffer_to_buffer(&output,0,&known_staging,0,4194304);queue.submit([encoder.finish()]);
   let slice=known_staging.slice(..);let (sender,receiver)=std::sync::mpsc::channel();slice.map_async(MapMode::Read,move|result|{let _=sender.send(result.is_ok());});let deadline=Instant::now()+Duration::from_secs(30);
   loop{device.poll(PollType::Poll)?;match receiver.try_recv(){Ok(mapped)=>{assert!(mapped);break;},Err(std::sync::mpsc::TryRecvError::Empty)=>{},Err(e)=>return Err(e.into())};if Instant::now()>deadline{return Err("Known depth control map timeout".into());}std::thread::sleep(Duration::from_millis(5));}
   let data=slice.get_mapped_range();let mut mismatches=[0usize;4];let mut first=[0f32;4];for (i,sample) in data.chunks_exact(4).enumerate(){let layer=i/262144;let value=f32::from_le_bytes(sample.try_into().unwrap());if i%262144==0{first[layer]=value;}if value.to_bits()!=(((layer+1) as f32)*0.25).to_bits(){mismatches[layer]+=1;}}
   std::fs::write(folder.join("KNOWN-DEPTH-PROBE-CONTROL.float32"),&data)?;drop(data);known_staging.unmap();
   std::fs::write(folder.join("KNOWN-DEPTH-PROBE-CONTROL.json"),serde_json::to_vec_pretty(&serde_json::json!({"expected":[0.25,0.5,0.75,1.0],"first_per_layer":first,"mismatches_per_layer":mismatches,"samples":1048576,"same_gpu_device":true,"same_compute_pipeline":true,"full_import":false}))?)?;
   assert_eq!(mismatches,[0,0,0,0],"Depth readback probe fails known clear values");
  }
