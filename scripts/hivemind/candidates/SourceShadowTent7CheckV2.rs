use bevy::{app::PluginsState,prelude::*,render::{RenderApp,RenderPlugin,pipelined_rendering::PipelinedRenderingPlugin,renderer::{RenderDevice,RenderQueue,RenderAdapterInfo},settings::{RenderCreation,WgpuSettings,Backends},render_resource::*},window::ExitCondition,winit::WinitPlugin};
use std::{time::{Instant,Duration},path::Path};
fn main()->Result<(),Box<dyn std::error::Error>> {
 let args:Vec<_>=std::env::args().collect();let root=Path::new(&args[1]);
 let protocol:serde_json::Value=serde_json::from_slice(&std::fs::read(root.join("PROTOCOL.v1.json"))?)?;
 let cases=protocol["cases"].as_array().unwrap();let input_bytes=cases.iter().flat_map(|row|row.as_array().unwrap().iter().flat_map(|v|(v.as_f64().unwrap() as f32).to_le_bytes())).collect::<Vec<_>>();
 let mut app=App::new();app.add_plugins(DefaultPlugins.set(WindowPlugin{primary_window:None,exit_condition:ExitCondition::DontExit,..default()}).set(RenderPlugin{render_creation:RenderCreation::Automatic(Box::new(WgpuSettings{backends:Some(Backends::DX12),force_fallback_adapter:false,..default()})),..default()}).disable::<WinitPlugin>().disable::<PipelinedRenderingPlugin>());
 let deadline=Instant::now()+Duration::from_secs(30);while app.plugins_state()!=PluginsState::Ready{if Instant::now()>deadline{return Err("GPU initialization timeout".into());}std::thread::sleep(Duration::from_millis(10));}app.finish();app.cleanup();
 let rw=app.get_sub_app(RenderApp).unwrap().world();let device=rw.resource::<RenderDevice>();let queue=rw.resource::<RenderQueue>();let info=rw.resource::<RenderAdapterInfo>();
 let code=std::fs::read_to_string(root.join("shader.wgsl"))?;
 let shader=device.create_and_validate_shader_module(ShaderModuleDescriptor{label:Some("source-tent7-contract"),source:ShaderSource::Wgsl(code.into())});
 let pipeline=device.wgpu_device().create_compute_pipeline(&RawComputePipelineDescriptor{label:Some("source-tent7-contract"),layout:None,module:&shader,entry_point:Some("main"),compilation_options:default(),cache:None});
 let input=device.create_buffer_with_data(&BufferInitDescriptor{label:Some("source-tent7-cases"),contents:&input_bytes,usage:BufferUsages::STORAGE});
 let size=(cases.len()*16*16) as u64;
 let output=device.create_buffer(&BufferDescriptor{label:Some("source-tent7-output"),size,usage:BufferUsages::STORAGE|BufferUsages::COPY_SRC,mapped_at_creation:false});
 let staging=device.create_buffer(&BufferDescriptor{label:Some("source-tent7-readback"),size,usage:BufferUsages::COPY_DST|BufferUsages::MAP_READ,mapped_at_creation:false});
 let layout=pipeline.get_bind_group_layout(0);let group=device.wgpu_device().create_bind_group(&BindGroupDescriptor{label:Some("source-tent7-bindings"),layout:&layout,entries:&[BindGroupEntry{binding:0,resource:input.as_entire_binding()},BindGroupEntry{binding:1,resource:output.as_entire_binding()}]});
 let mut encoder=device.create_command_encoder(&CommandEncoderDescriptor{label:Some("source-tent7-dispatch")});
 {let mut pass=encoder.begin_compute_pass(&ComputePassDescriptor{label:Some("source-tent7-dispatch"),timestamp_writes:None});pass.set_pipeline(&pipeline);pass.set_bind_group(0,&group,&[]);pass.dispatch_workgroups((cases.len() as u32).div_ceil(64),1,1);}
 encoder.copy_buffer_to_buffer(&output,0,&staging,0,size);queue.submit([encoder.finish()]);
 let slice=staging.slice(..);let (sender,receiver)=std::sync::mpsc::channel();slice.map_async(MapMode::Read,move|result|{let _=sender.send(result.is_ok());});
 let deadline=Instant::now()+Duration::from_secs(30);loop{device.poll(PollType::Poll)?;match receiver.try_recv(){Ok(mapped)=>{assert!(mapped);break;},Err(std::sync::mpsc::TryRecvError::Empty)=>{},Err(error)=>return Err(error.into())};if Instant::now()>deadline{return Err("GPU map timeout".into());}std::thread::sleep(Duration::from_millis(5));}
 let data=slice.get_mapped_range();for value in data.chunks_exact(4){assert!(f32::from_le_bytes(value.try_into().unwrap()).is_finite());}std::fs::write(root.join("NATIVE-TENT7.rgba32f"),&data)?;
 drop(data);staging.unmap();
 let result=serde_json::json!({"status":"actual_native_tent7_compute_shader_dispatched_and_read_back","cases":cases.len(),"taps_per_case":16,"bytes":size,"adapter":info.name,"backend":format!("{:?}",info.backend),"gpu_readback":true,"source_fidelity_qualified":false,"full_import":false});
 std::fs::write(root.join("NATIVE-RESULT.v1.json"),serde_json::to_vec_pretty(&result)?)?;Ok(())
}
