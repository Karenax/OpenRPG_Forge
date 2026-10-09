use std::{fs,path::Path,time::{Duration,Instant},future::Future,sync::{Arc,mpsc},task::{Wake,Context,Poll}};
use serde_json::{Value,json};
struct ThreadWake(std::thread::Thread);
impl Wake for ThreadWake {fn wake(self:Arc<Self>){self.0.unpark();}}
fn block_on<F:Future>(future:F)->F::Output {
 let waker=Arc::new(ThreadWake(std::thread::current())).into();let mut cx=Context::from_waker(&waker);let mut future=Box::pin(future);
 loop {match future.as_mut().poll(&mut cx){Poll::Ready(v)=>return v,Poll::Pending=>std::thread::park_timeout(Duration::from_millis(5))}}
}
fn main(){
 let root=std::env::args().nth(1).unwrap();let root=Path::new(&root);
 let manifest:Value=serde_json::from_slice(&fs::read(root.join("INPUT.v1.json")).unwrap()).unwrap();
 let instance=wgpu::Instance::new(wgpu::InstanceDescriptor{backends:wgpu::Backends::DX12,..wgpu::InstanceDescriptor::new_without_display_handle()});
 let adapter=block_on(instance.request_adapter(&wgpu::RequestAdapterOptions{power_preference:wgpu::PowerPreference::HighPerformance,force_fallback_adapter:false,compatible_surface:None})).unwrap();
 assert!(adapter.features().contains(wgpu::Features::TEXTURE_COMPRESSION_BC));
 let (device,queue)=block_on(adapter.request_device(&wgpu::DeviceDescriptor{required_features:wgpu::Features::TEXTURE_COMPRESSION_BC,required_limits:adapter.limits(),..Default::default()})).unwrap();
 let shader=device.create_shader_module(wgpu::ShaderModuleDescriptor{label:Some("native source resource load/decode control"),source:wgpu::ShaderSource::Wgsl(fs::read_to_string(root.join("load-decode.wgsl")).unwrap().into())});
 let pipeline=device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor{label:None,layout:None,module:&shader,entry_point:Some("main"),compilation_options:Default::default(),cache:None});
 let mut results=Vec::new();
 for row in manifest["rows"].as_array().unwrap() {
  let format=match row["encoding"].as_str().unwrap(){"bc1_rgba_unorm_srgb"=>wgpu::TextureFormat::Bc1RgbaUnormSrgb,"bc3_rgba_unorm_srgb"=>wgpu::TextureFormat::Bc3RgbaUnormSrgb,"bc3_rgba_unorm"=>wgpu::TextureFormat::Bc3RgbaUnorm,"rgba8_unorm"=>wgpu::TextureFormat::Rgba8Unorm,_=>panic!("unknown native format")};
  let width=row["width"].as_u64().unwrap() as u32;let height=row["height"].as_u64().unwrap() as u32;
  let texture=device.create_texture(&wgpu::TextureDescriptor{label:None,size:wgpu::Extent3d{width,height,depth_or_array_layers:1},mip_level_count:row["mips"].as_array().unwrap().len() as u32,sample_count:1,dimension:wgpu::TextureDimension::D2,format,usage:wgpu::TextureUsages::COPY_DST|wgpu::TextureUsages::TEXTURE_BINDING,view_formats:&[]});
  let data=fs::read(row["native_storage"].as_str().unwrap()).unwrap();let(block_w,block_h)=format.block_dimensions();let block_bytes=format.block_copy_size(None).unwrap();
  let mut coordinates=Vec::new();
  for (level,mip) in row["mips"].as_array().unwrap().iter().enumerate(){
   let w=mip["width"].as_u64().unwrap() as u32;let h=mip["height"].as_u64().unwrap() as u32;let off=mip["offset"].as_u64().unwrap() as usize;let n=mip["bytes"].as_u64().unwrap() as usize;
   queue.write_texture(wgpu::TexelCopyTextureInfo{texture:&texture,mip_level:level as u32,origin:wgpu::Origin3d::ZERO,aspect:wgpu::TextureAspect::All},&data[off..off+n],wgpu::TexelCopyBufferLayout{offset:0,bytes_per_row:Some(w.div_ceil(block_w)*block_bytes),rows_per_image:Some(h.div_ceil(block_h))},wgpu::Extent3d{width:w.div_ceil(block_w)*block_w,height:h.div_ceil(block_h)*block_h,depth_or_array_layers:1});
   let points:[(f32,f32);9]=[(-0.125,0.375),(0.0,0.0),(0.125,0.875),(0.49937,0.50391),(1.0,1.0),(1.125,-0.125),(0.99975,0.00025),(-1.375,2.625),(0.333333,0.777777)];
   for (x,y) in points {
    for v in [x,y,level as f32+0.25,0.0]{coordinates.extend_from_slice(&v.to_le_bytes());}
   }
  }
  let count=(coordinates.len()/16) as u32;assert_eq!(count,108);
  let normal=row["normal_decoder_profile_index"].is_number();let mode=if normal{2u32}else{0};
  let coord=device.create_buffer(&wgpu::BufferDescriptor{label:None,size:coordinates.len() as u64,usage:wgpu::BufferUsages::STORAGE|wgpu::BufferUsages::COPY_DST,mapped_at_creation:false});queue.write_buffer(&coord,0,&coordinates);
  let params=device.create_buffer(&wgpu::BufferDescriptor{label:None,size:16,usage:wgpu::BufferUsages::UNIFORM|wgpu::BufferUsages::COPY_DST,mapped_at_creation:false});
  let bytes:Vec<u8>=[count,mode,u32::from(normal),0].into_iter().flat_map(u32::to_le_bytes).collect();queue.write_buffer(&params,0,&bytes);
  let size=u64::from(count)*32;
  let output=device.create_buffer(&wgpu::BufferDescriptor{label:None,size,usage:wgpu::BufferUsages::STORAGE|wgpu::BufferUsages::COPY_SRC,mapped_at_creation:false});
  let staging=device.create_buffer(&wgpu::BufferDescriptor{label:None,size,usage:wgpu::BufferUsages::MAP_READ|wgpu::BufferUsages::COPY_DST,mapped_at_creation:false});
  let sampling=&row["sampler"];
  assert_eq!(sampling["filter_mode"],"Bilinear");
  for key in ["wrap_u","wrap_v","wrap_w"] {assert_eq!(sampling[key],"Repeat");}
  assert_eq!(sampling["aniso_level"],1);assert_eq!(sampling["mip_map_bias"],0.0);
  let sampler=device.create_sampler(&wgpu::SamplerDescriptor{address_mode_u:wgpu::AddressMode::Repeat,address_mode_v:wgpu::AddressMode::Repeat,address_mode_w:wgpu::AddressMode::Repeat,mag_filter:wgpu::FilterMode::Linear,min_filter:wgpu::FilterMode::Linear,mipmap_filter:wgpu::MipmapFilterMode::Linear,..Default::default()});
  let view=texture.create_view(&Default::default());
  let bind=device.create_bind_group(&wgpu::BindGroupDescriptor{label:None,layout:&pipeline.get_bind_group_layout(0),entries:&[
   wgpu::BindGroupEntry{binding:0,resource:wgpu::BindingResource::TextureView(&view)},wgpu::BindGroupEntry{binding:1,resource:coord.as_entire_binding()},wgpu::BindGroupEntry{binding:2,resource:params.as_entire_binding()},wgpu::BindGroupEntry{binding:3,resource:output.as_entire_binding()},wgpu::BindGroupEntry{binding:4,resource:wgpu::BindingResource::Sampler(&sampler)}]});
  let mut encoder=device.create_command_encoder(&Default::default());
  {let mut pass=encoder.begin_compute_pass(&Default::default());pass.set_pipeline(&pipeline);pass.set_bind_group(0,&bind,&[]);pass.dispatch_workgroups(count.div_ceil(64),1,1);}
  encoder.copy_buffer_to_buffer(&output,0,&staging,0,size);queue.submit([encoder.finish()]);
  let (sender,receiver)=mpsc::channel();staging.slice(..).map_async(wgpu::MapMode::Read,move|v|{sender.send(v.is_ok()).unwrap();});let deadline=Instant::now()+Duration::from_secs(30);
  loop{device.poll(wgpu::PollType::Poll).unwrap();match receiver.try_recv(){Ok(ok)=>{assert!(ok);break},Err(mpsc::TryRecvError::Empty)=>{},Err(e)=>panic!("{e}")};assert!(Instant::now()<deadline,"GPU readback timed out");std::thread::sleep(Duration::from_millis(5));}
  let payload=staging.slice(..).get_mapped_range().to_vec();staging.unmap();let slot=row["authored_texture_index"].as_u64().unwrap();let file=root.join(format!("texture-{slot:02}.load-decode.float32"));fs::write(&file,payload).unwrap();
  results.push(json!({"guid":row["guid"],"authored_texture_index":slot,"samples":count,"normal_decode_enabled":normal,"common_unpack_mode":mode,"file":file.to_string_lossy()}));
 }
 let info=adapter.get_info();fs::write(root.join("NATIVE-RESULT.v1.json"),serde_json::to_vec_pretty(&json!({"status":"native_filtered_resource_gpu_control_complete","adapter":info.name,"backend":format!("{:?}",info.backend),"rows":results,"full_import":false,"actual_engine_material_render_qualified":false})).unwrap()).unwrap();
}
