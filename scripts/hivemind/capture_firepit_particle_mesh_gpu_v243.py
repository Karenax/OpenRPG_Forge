"""Render all retained converted particle meshes with native BC3 storage.
Isolated GPU transport proof, not current product integration or Unity mesh parity.
"""
from pathlib import Path
import json,hashlib,struct,shutil,subprocess,os,sys,datetime
import numpy as np
W=Path('F:/OpenRPG_ImportWork');O=W/'firepit-particle-mesh-gpu-v243';B=W/'delta-firepit-ra-shader-gpu-v2';F=W/'firepit-particle-forge-buffers-v232';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin');NAME='openrpg-firepit-particle-mesh-gpu-v243'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(n,d):(O/n).write_text(json.dumps(d,indent=2),'utf-8')
def once(s,a,b):assert s.count(a)==1,a;return s.replace(a,b)
def mat(d):return np.array([[d[f'e{i}{j}'] for j in range(4)] for i in range(4)])
def main():
 assert not O.exists();O.mkdir();(O/'src').mkdir();(O/'streams').mkdir();(O/'temp').mkdir()
 prior=read(B/'RUN-RESULT.v1.json');assert prior['fixture_shader_actual_ra_gpu_parity_verified'] and prior['maximum_rgba_error']==0
 pins={str(p):sha(p) for p in [Path(__file__),B/'RUN-RESULT.v1.json',B/'INPUT.v1.json',F/'RESULT.v1.json']}
 input=read(B/'INPUT.v1.json');tex=input['texture'];pins[tex['storage_blob']]=sha(Path(tex['storage_blob'])) if 'storage_blob' in tex else ''
 # All source texture storage and sampler proofs pinned by the acquired GPU input.
 for key,digest in input['input_pins'].items():
  if Path(key).is_file() and sha(Path(key))==digest:pins[key]=digest
 frames=[]
 for fp in sorted(F.glob('*/FRAME.v1.json')):
  frame=read(fp);pins[str(fp)]=sha(fp)
  for stream in frame['streams'].values():assert sha(Path(stream['path']))==stream['sha256'];pins[stream['path']]=stream['sha256']
  positions=np.frombuffer(Path(frame['streams']['positions.f32']['path']).read_bytes(),dtype='<f4').reshape(-1,3)
  colors=Path(frame['streams']['colors.rgba8']['path']).read_bytes();uv=np.frombuffer(Path(frame['streams']['uv0.f32']['path']).read_bytes(),dtype='<f4').reshape(-1,4)
  instance=np.array(frame['target_instance_matrix_column_major']).reshape(4,4).T;reflection=np.diag([-1.,1.,1.,1.])
  clip=(mat(frame['source_projection'])@mat(frame['source_world_to_camera'])@reflection@instance@np.c_[positions,np.ones(len(positions))].T).T
  # Source projection has [-w,w] depth; wgpu uses [0,w]. No fitted camera.
  clip[:,2]=(clip[:,2]+clip[:,3])*.5;assert np.isfinite(clip).all()
  packed=b''.join(struct.pack('<4f',*clip[i])+colors[4*i:4*i+4]+struct.pack('<2f',*uv[i,:2]) for i in range(len(positions)))
  vp=O/'streams'/f'{fp.parent.name}.vertices';vp.write_bytes(packed);pins[str(vp)]=sha(vp)
  binding=W/'delta-firepit-legacy-material-bindings-v1'/frame['material_guid']/'MATERIAL-UNIFORMS.bin';pins[str(binding)]=sha(binding)
  frames.append(dict(frame_path=str(fp),vertex_blob=str(vp),index_blob=frame['streams']['indices.u32']['path'],index_count=frame['triangles']*3,uniform_blob=str(binding),vertex_color=[1,1,1,1],background=[.02,.025,.03,.25],material_guid=frame['material_guid']))
 assert len(frames)==336
 text=(B/'src/main.rs').read_text('utf-8')
 attrs='&[wgpu::VertexBufferLayout {array_stride:28,step_mode:wgpu::VertexStepMode::Vertex,attributes:&[wgpu::VertexAttribute{format:wgpu::VertexFormat::Float32x4,offset:0,shader_location:0},wgpu::VertexAttribute{format:wgpu::VertexFormat::Unorm8x4,offset:16,shader_location:1},wgpu::VertexAttribute{format:wgpu::VertexFormat::Float32x2,offset:20,shader_location:2}]}]'
 text=once(text,'buffers: &[]','buffers: '+attrs)
 text=once(text,'wgpu::CompareFunction::GreaterEqual','wgpu::CompareFunction::Always')
 text=once(text,'let vertex_bytes: Vec<u8> =','let mesh_vertices=std::fs::read(fixture["vertex_blob"].as_str().unwrap()).unwrap();let mesh_indices=std::fs::read(fixture["index_blob"].as_str().unwrap()).unwrap();let mesh_vertex_buffer=device.create_buffer_init(&wgpu::util::BufferInitDescriptor{label:None,contents:&mesh_vertices,usage:wgpu::BufferUsages::VERTEX});let mesh_index_buffer=device.create_buffer_init(&wgpu::util::BufferInitDescriptor{label:None,contents:&mesh_indices,usage:wgpu::BufferUsages::INDEX});let vertex_bytes: Vec<u8> =')
 text=once(text,'width: 16, height: 16','width: 256, height: 256')
 text=once(text,'size: 4096,','size: 524288,')
 text=once(text,'bytes_per_row: Some(256)','bytes_per_row: Some(2048)')
 text=once(text,'rows_per_image: Some(16)','rows_per_image: Some(256)')
 text=once(text,'mapped.chunks_exact(256).flat_map(|row| row[..128]','mapped.chunks_exact(2048).flat_map(|row| row[..2048]')
 text=once(text,'raw_pixels.len(), 2048','raw_pixels.len(), 524288')
 text=once(text,'pass.draw(0..3, 0..1);','pass.set_vertex_buffer(0,mesh_vertex_buffer.slice(..));pass.set_index_buffer(mesh_index_buffer.slice(..),wgpu::IndexFormat::Uint32);pass.draw_indexed(0..fixture["index_count"].as_u64().unwrap() as u32,0,0..1);')
 text=once(text,'results.len(), 16','results.len(), 336').replace('"width":16,"height":16','"width":256,"height":256')
 shader=(B/'src/fixture.wgsl').read_text('utf-8');begin=shader.index('@vertex fn vertex(');end=shader.index('@fragment fn fragment(',begin)
 shader=shader[:begin]+'@vertex fn vertex(@location(0) clip:vec4<f32>,@location(1) color:vec4<f32>,@location(2) uv:vec2<f32>) -> VertexOutput {var output:VertexOutput;output.position=clip;output.uv=uv*material.st.xy+material.st.zw;output.color=source_particle_alpha_vertex_color(color*fixture.color,material.tint);return output;}\n'+shader[end:]
 (O/'src/main.rs').write_text(text,'utf-8');(O/'src/fixture.wgsl').write_text(shader,'utf-8')
 for n in ['imported_texture.rs','sampler.rs']:shutil.copyfile(B/'src'/n,O/'src'/n)
 for n in ['Cargo.toml','Cargo.lock']:(O/n).write_text((B/n).read_text('utf-8').replace('openrpg-firepit-ra-shader-gpu-v2',NAME),'utf-8')
 for p in list((O/'src').iterdir())+[O/'Cargo.toml',O/'Cargo.lock']:pins[str(p)]=sha(p)
 # Never repin missing/changed source storage: GPU's imported-texture reader verifies its full blob hash.
 pins={k:v for k,v in pins.items() if v}
 save('INPUT.v1.json',dict(input_pins=pins,texture=tex,fixtures=frames,full_import=False))
 env=os.environ.copy();env.update(RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',TEMP=str(O/'temp'),TMP=str(O/'temp'),PATH=str(BIN)+os.pathsep+env['PATH'])
 state=dict(status='building_converted_particle_mesh_gpu',expected_meshes=336,product_renderer_integrated=False,source_unity_mesh_parity=False,full_import=False);save('RUN-RESULT.v1.json',state)
 with (O/'build.log').open('xb') as log:r=subprocess.run([str(BIN/'cargo.exe'),'build','--offline','--locked','--manifest-path',str(O/'Cargo.toml')],env=env,stdout=log,stderr=log)
 state['build_exit_code']=r.returncode;save('RUN-RESULT.v1.json',state);assert r.returncode==0
 exe=Path(env['CARGO_TARGET_DIR'])/'debug'/f'{NAME}.exe';state['executable_sha256']=sha(exe);state['status']='rendering_converted_particle_meshes';save('RUN-RESULT.v1.json',state)
 with (O/'gpu.log').open('xb') as log:r=subprocess.run([str(exe),str(O/'INPUT.v1.json'),str(O/'gpu')],env=env,stdout=log,stderr=log)
 state.update(gpu_exit_code=r.returncode,status='particle_mesh_gpu_terminal',finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat());save('RUN-RESULT.v1.json',state);assert r.returncode==0
 print(json.dumps(state))
if __name__=='__main__':main()
