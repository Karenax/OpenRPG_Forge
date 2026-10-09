"""Prepare native PCF5 sampling of an atlas rendered from original caster buffers."""
from pathlib import Path
import hashlib,json,struct,shutil
W=Path('F:/OpenRPG_ImportWork')
S=Path(__file__).parent
SEED=W/'firepit-punctual-d16-caster-shader-mapped-v1'
ROOT=W/'firepit-punctual-d16-tent5-receiver-v1'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists()
 old=json.loads((SEED/'CASE.v1.json').read_text(encoding='utf-8'))['input_pins']
 # Every dependency of the retained replay must still match its revision.
 for p,h in old.items():assert sha(p)==h,p
 ROOT.mkdir();(ROOT/'src').mkdir()
 names=['Cargo.toml','Cargo.lock','vertices.float32','indices.uint32']+[f'face-{i}.uniform' for i in range(6)]
 for name in names:shutil.copy2(SEED/name,ROOT/name)
 source=json.loads((W/'firepit-source-point-shadow-raster-v3/SOURCE-SHADOW-RASTER-0.json').read_text(encoding='utf-8'))
 requests=json.loads(source['requests_json']);cases=[]
 for request in requests:
  v=request['dynamicAtlasViewport']
  for y in range(1,10):
   for x in range(1,10):
    for z in [.1,.3,.6]:
     cases.append([(v['x']+v['width']*x/10)/4096,(v['y']+v['height']*y/10)/4096,z,0.])
 assert len(cases)==1458
 (ROOT/'receiver-cases.float32').write_bytes(b''.join(struct.pack('<4f',*row) for row in cases))
 shader=(S/'candidates/SourceShadowTent5.wgsl').read_text(encoding='utf-8')+'\n'+(S/'candidates/SourceShadowTent5Receiver.wgsl').read_text(encoding='utf-8')+'''
@group(0) @binding(0) var atlas_depth:texture_depth_2d;
@group(0) @binding(1) var atlas_compare:sampler_comparison;
@group(0) @binding(2) var<storage,read> receiver_cases:array<vec4<f32>>;
@group(0) @binding(3) var<storage,read_write> receiver_output:array<vec4<f32>>;
@compute @workgroup_size(64) fn receiver(@builtin(global_invocation_id) id:vec3<u32>) {
 if(id.x>=arrayLength(&receiver_cases)){return;}
 let item=receiver_cases[id.x];
 let visibility=source_tent5_compare(atlas_depth,atlas_compare,vec4<f32>(1./4096.,1./4096.,4096.,4096.),item.xyz,1./65536.);
 receiver_output[id.x]=vec4<f32>(visibility,item.xyz);
}
'''
 (ROOT/'receiver.wgsl').write_text(shader,encoding='utf-8')
 text=(SEED/'src/main.rs').read_text(encoding='utf-8')
 marker=' let mut measurements=Vec::new();'
 assert text.count(marker)==1
 setup=r'''
 let receiver_shader=device.create_shader_module(ShaderModuleDescriptor{label:Some("pcf5-native-atlas-receiver"),source:ShaderSource::Wgsl(std::fs::read_to_string("receiver.wgsl")?.into())});
 let receiver_pipeline=device.create_compute_pipeline(&ComputePipelineDescriptor{label:Some("pcf5-native-atlas-receiver"),layout:None,module:&receiver_shader,entry_point:Some("receiver"),compilation_options:Default::default(),cache:None});
 let receiver_input=device.create_buffer_init(&util::BufferInitDescriptor{label:Some("fixed-receiver-protocol"),contents:&std::fs::read("receiver-cases.float32")?,usage:BufferUsages::STORAGE});
 let receiver_size=1458u64*16;
 let receiver_output=device.create_buffer(&BufferDescriptor{label:None,size:receiver_size,usage:BufferUsages::STORAGE|BufferUsages::COPY_SRC,mapped_at_creation:false});
 let receiver_staging=device.create_buffer(&BufferDescriptor{label:None,size:receiver_size,usage:BufferUsages::MAP_READ|BufferUsages::COPY_DST,mapped_at_creation:false});
 let comparison=device.create_sampler(&SamplerDescriptor{label:Some("reversed-depth-linear-clamp"),address_mode_u:AddressMode::ClampToEdge,address_mode_v:AddressMode::ClampToEdge,address_mode_w:AddressMode::ClampToEdge,mag_filter:FilterMode::Linear,min_filter:FilterMode::Linear,compare:Some(CompareFunction::GreaterEqual),..Default::default()});
 let receiver_group=device.create_bind_group(&BindGroupDescriptor{label:None,layout:&receiver_pipeline.get_bind_group_layout(0),entries:&[
 BindGroupEntry{binding:0,resource:BindingResource::TextureView(atlas.view())},
 BindGroupEntry{binding:1,resource:BindingResource::Sampler(&comparison)},
 BindGroupEntry{binding:2,resource:receiver_input.as_entire_binding()},
 BindGroupEntry{binding:3,resource:receiver_output.as_entire_binding()}]});
'''
 text=text.replace(marker,setup+marker)
 marker=' encoder.copy_buffer_to_buffer(&storage,0,&staging,0,size);queue.submit([encoder.finish()]);'
 assert text.count(marker)==1
 text=text.replace(marker,r'''
 {let mut pass=encoder.begin_compute_pass(&ComputePassDescriptor::default());pass.set_pipeline(&receiver_pipeline);pass.set_bind_group(0,&receiver_group,&[]);pass.dispatch_workgroups(1458u32.div_ceil(64),1,1);}
 encoder.copy_buffer_to_buffer(&receiver_output,0,&receiver_staging,0,receiver_size);
'''+marker)
 marker=' drop(bytes);staging.unmap();'
 assert text.count(marker)==1
 text=text.replace(marker,marker+r'''
 let (tx,rx)=std::sync::mpsc::channel();receiver_staging.slice(..).map_async(MapMode::Read,move|r|{let _=tx.send(r);});
 let deadline=Instant::now()+Duration::from_secs(30);
 loop{device.poll(PollType::Poll)?;if let Ok(r)=rx.try_recv(){r?;break;}if Instant::now()>deadline{return Err("PCF5 receiver readback timeout".into());}std::thread::sleep(Duration::from_millis(5));}
 let bytes=receiver_staging.slice(..).get_mapped_range();
 for row in bytes.chunks_exact(16){let v=f32::from_le_bytes(row[..4].try_into().unwrap());assert!(v.is_finite()&&v>=-2e-6&&v<=1.+2e-6);if !active{assert!((v-1.).abs()<2e-6);}}
 std::fs::write(if active{"active-receiver.rgba32f"}else{"inactive-receiver.rgba32f"},&*bytes)?;
 drop(bytes);receiver_staging.unmap();
''')
 text=text.replace('"status":"shader_mapped_original_lod0_d16_depth_replay_captured_comparison_pending"','"status":"native_d16_tent5_receiver_dispatched_comparison_pending","receiver_cases_per_state":1458,"comparison_bias":1.0/65536.0,"source_depth_pixels_used_as_render_input":false')
 (ROOT/'src/main.rs').write_text(text,encoding='utf-8')
 pins=old.copy()
 for p in [Path(__file__),S/'candidates/SourceShadowTent5.wgsl',S/'candidates/SourceShadowTent5Receiver.wgsl',*[p for p in ROOT.rglob('*') if p.is_file()]]:pins[str(p)]=sha(p)
 case={'sub_lot':'L01.PCF5.3','cases_per_state':1458,'input_pins':pins,'source_depth_pixels_used_as_render_input':False,'original_caster_buffers_reused':True,'source_receiver_projection_qualified':False,'scene_fidelity_qualified':False,'editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0}
 (ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2,ensure_ascii=False),encoding='utf-8')
 print(str(ROOT))
if __name__=='__main__':main()
