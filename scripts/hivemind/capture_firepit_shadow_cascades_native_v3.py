"""Read the actual native lights uniform buffer, retaining the original pixels."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_shadow_cascades_native_v1.py')
source=BASE.read_text('utf-8').replace('firepit-shadow-cascades-native-v1','firepit-shadow-cascades-native-v3')
needle='source=source.replace(needle,extra+needle)'
assert source.count(needle)==1
addition=r'''
extra += r"""
 {
 use bevy::render::render_resource::{BufferDescriptor,BufferUsages,CommandEncoderDescriptor,MapMode,PollType,ShaderType};
 use bevy::pbr::{LightMeta,ViewLightsUniformOffset,GpuLights,GpuDirectionalCascade};
 let rw=app.get_sub_app_mut(RenderApp).unwrap().world_mut();
 let offsets={let mut q=rw.query::<&ViewLightsUniformOffset>();q.iter(rw).map(|v|v.offset).collect::<Vec<_>>()};
 assert_eq!(offsets.len(),1,"Expected the sole camera lights uniform");
 let buffer=rw.resource::<LightMeta>().view_gpu_lights.buffer().ok_or("Lights GPU buffer absent")?.clone();
 let gpu_device=rw.resource::<RenderDevice>().clone();let gpu_queue=rw.resource::<bevy::render::renderer::RenderQueue>().clone();
 let packet_size=GpuLights::min_size().get();let cascade_stride=GpuDirectionalCascade::min_size().get();assert_eq!(cascade_stride,80);
 let staging=gpu_device.create_buffer(&BufferDescriptor{label:Some("source-shadow-uniform-readback"),size:packet_size,usage:BufferUsages::COPY_DST|BufferUsages::MAP_READ,mapped_at_creation:false});
 let mut encoder=gpu_device.create_command_encoder(&CommandEncoderDescriptor{label:Some("source-shadow-uniform-copy")});
 encoder.copy_buffer_to_buffer(&buffer,u64::from(offsets[0]),&staging,0,packet_size);gpu_queue.submit([encoder.finish()]);
 let slice=staging.slice(..);let (sender,receiver)=std::sync::mpsc::channel();slice.map_async(MapMode::Read,move|result|{let _=sender.send(result.is_ok());});
 let deadline=Instant::now()+Duration::from_secs(30);loop{gpu_device.poll(PollType::Poll)?;match receiver.try_recv(){Ok(mapped)=>{assert!(mapped);break;},Err(std::sync::mpsc::TryRecvError::Empty)=>{},Err(error)=>return Err(error.into())};if Instant::now()>deadline{return Err("Lights GPU buffer readback timed out".into());}std::thread::sleep(Duration::from_millis(5));}
 let bytes=slice.get_mapped_range();std::fs::write(folder.join(format!("NATIVE-LIGHTS-GPU-{view}.bin")),&bytes)?;let mut read_rows=Vec::new();
 for (index,cascade) in cascades.iter().enumerate(){
  let base=index*cascade_stride as usize;let scalars:Vec<f32>=(0..18).map(|i|f32::from_le_bytes(bytes[base+i*4..base+i*4+4].try_into().unwrap())).collect();
  for (actual,expected) in scalars[..16].iter().zip(cascade.clip_from_world.to_cols_array()){assert_eq!(actual.to_bits(),expected.to_bits());}
  assert_eq!(scalars[16].to_bits(),cascade.texel_size.to_bits());assert_eq!(scalars[17].to_bits(),cascade_config.bounds[index].to_bits());
  read_rows.push(serde_json::json!({"index":index,"clip_from_world":&scalars[..16],"world_texel_size":scalars[16],"far_bound":scalars[17]}));
 }
 std::fs::write(folder.join(format!("NATIVE-LIGHTS-GPU-{view}.json")),serde_json::to_vec_pretty(&serde_json::json!({"view":view,"bytes":bytes.len(),"uniform_offset":offsets[0],"view_offsets_count":offsets.len(),"cascade_stride":cascade_stride,"cascades":read_rows,"gpu_buffer_copied":true,"cpu_gpu_scalar_bits_equal":true,"private_copy_src_usage_only":true,"shader_settings_changed":false}))?)?;
 drop(bytes);staging.unmap();
 }
"""
source=source.replace('rust=rust.replace(needle,observer+needle)',"rust=rust.replace(needle,observer+needle)\n    rust=rust.replace('app.finish();app.cleanup();','app.finish();app.cleanup();app.get_sub_app_mut(RenderApp).unwrap().world_mut().resource_mut::<bevy::pbr::LightMeta>().view_gpu_lights.add_usages(bevy::render::render_resource::BufferUsages::COPY_SRC);')")
'''
source=source.replace(needle,addition+needle)
source=source.replace('ok_or("Directional cascade config absent")?;', 'ok_or("Directional cascade config absent")?.clone();')
source=source.replace('ok_or("Camera cascade state absent")?;', 'ok_or("Camera cascade state absent")?.clone();')
exec(compile(source,str(BASE),'exec'),globals())
