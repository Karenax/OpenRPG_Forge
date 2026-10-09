from pathlib import Path
S=Path(__file__).parent
def main():
 code=(S/'candidates/SourceNativeTextureLoadV2.rs').read_text(encoding='utf-8')
 start=code.index('   for (x,y) in [(0,0)');end=code.index('\n  }\n  let count=',start)
 code=code[:start]+'''   let points:[(f32,f32);9]=[(-0.125,0.375),(0.0,0.0),(0.125,0.875),(0.49937,0.50391),(1.0,1.0),(1.125,-0.125),(0.99975,0.00025),(-1.375,2.625),(0.333333,0.777777)];
   for (x,y) in points {
    for v in [x,y,level as f32+0.25,0.0]{coordinates.extend_from_slice(&v.to_le_bytes());}
   }'''+code[end:]
 anchor='  let view=texture.create_view(&Default::default());';assert code.count(anchor)==1
 code=code.replace(anchor,'''  let sampling=&row["sampler"];
  assert_eq!(sampling["filter_mode"],"Bilinear");
  for key in ["wrap_u","wrap_v","wrap_w"] {assert_eq!(sampling[key],"Repeat");}
  assert_eq!(sampling["aniso_level"],1);assert_eq!(sampling["mip_map_bias"],0.0);
  let sampler=device.create_sampler(&wgpu::SamplerDescriptor{address_mode_u:wgpu::AddressMode::Repeat,address_mode_v:wgpu::AddressMode::Repeat,address_mode_w:wgpu::AddressMode::Repeat,mag_filter:wgpu::FilterMode::Linear,min_filter:wgpu::FilterMode::Linear,mipmap_filter:wgpu::MipmapFilterMode::Nearest,..Default::default()});
'''+anchor)
 anchor='wgpu::BindGroupEntry{binding:3,resource:output.as_entire_binding()}]';assert code.count(anchor)==1
 code=code.replace(anchor,'wgpu::BindGroupEntry{binding:3,resource:output.as_entire_binding()},wgpu::BindGroupEntry{binding:4,resource:wgpu::BindingResource::Sampler(&sampler)}]')
 code=code.replace('native_resource_load_decode_gpu_control_complete','native_filtered_resource_gpu_control_complete')
 (S/'candidates/SourceFilteredNativeTextureV1.rs').write_text(code,encoding='utf-8')
 queue=(S/'queue_native_texture_gpu_control_v2.py').read_text(encoding='utf-8')
 queue=queue.replace('townsmith-s-paint-native-texture-gpu-control-v2','townsmith-s-paint-native-filtered-gpu-control-v1').replace('SourceNativeTextureLoadV2.rs','SourceFilteredNativeTextureV1.rs')
 queue=queue.replace('coordinates:array<vec4<u32>>','coordinates:array<vec4<f32>>')
 queue=queue.replace('@compute @workgroup_size(64)','@group(0) @binding(4) var source_sampler:sampler;\n@compute @workgroup_size(64)')
 queue=queue.replace('textureLoad(source_texture,vec2<i32>(p.xy),i32(p.z))','textureSampleLevel(source_texture,source_sampler,p.xy,p.z)')
 (S/'queue_townsmith_filtered_gpu_v1.py').write_text(queue,encoding='utf-8')
 print('Native filtered GPU control prepared with captured Bilinear/Repeat/1x/zero-bias guards')
if __name__=='__main__':main()
