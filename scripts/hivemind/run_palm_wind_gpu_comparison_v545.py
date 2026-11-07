"""Compare exact source vertex WGSL with frozen Unity Forward compute inputs."""
from pathlib import Path
import datetime,hashlib,json,math,os,re,struct,subprocess
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-palm-wind-gpu-comparison-v545'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(name,value):
    (O/name).write_text(json.dumps(value,indent=2),'utf-8')
assert not O.exists()
proofpath=W/'pirate-palm-scalar-repair-delivery-v544/RESULT.v1.json'
proof=read(proofpath)
pins=dict(proof['pins'])
for p,h in pins.items():assert sha(p)==h,p
shaderpath=W/'pirate-palm-wgsl-v543/hivemind.pirate-island/abc00000000004284671107665666980/source_material.wgsl'
irpath=W/'pirate-palm-native-default-binding-v536/native-bound-material-ir.json'
batch=read(W/'pirate-palm-wgsl-input-v543/BATCH.v1.json')
assert sha(irpath)==batch['rows'][0]['output_sha256']
ir=read(irpath);nodes={n['id']:n for n in ir['nodes']};required=set()
def visit(value):
    if 'ref' not in value:return
    nid=value['ref']
    if nid in required:return
    required.add(nid)
    for inp in nodes[nid]['inputs']:visit(inp['value'])
visit(ir['terminals']['VertexDescription.Position'])
assert all(nodes[n]['opcode']!='SampleTexture2DNode' for n in required)
source=shaderpath.read_text('utf-8-sig')
lines=[line for line in source.splitlines() if (m:=re.match(r'let n(\d+)=',line)) and m[1] in required]
assert len(lines)==len(required)
def function(name):
    start=source.index('fn '+name+'(');brace=source.index('{',start);depth=1;end=brace+1
    while depth:
        depth+=(source[end]=='{')-(source[end]=='}');end+=1
    return source[start:end]
helpers='\n'.join(function(n) for n in ['source_hlsl_remainder2','source_hlsl_remainder','source_gradient_noise_dir_v0','source_gradient_noise_v0'])
input_struct=re.search(r'struct SourceMaterialInputs \{[^}]+\}',source).group()
root=ir['terminals']['VertexDescription.Position']['ref']
wgsl=helpers+'\n'+input_struct+'''
struct InputCase { object_position:vec4<f32>,world_position:vec4<f32>,normal:vec4<f32>,tangent:vec4<f32>,uv:vec4<f32>,time:vec4<f32>, }
@group(0) @binding(0) var<storage,read> cases:array<InputCase>;
@group(0) @binding(1) var<storage,read_write> results:array<vec4<f32>>;
@compute @workgroup_size(64) fn wind(@builtin(global_invocation_id) id:vec3<u32>){
if id.x>=arrayLength(&cases){return;}
let v=cases[id.x];var i:SourceMaterialInputs;
i.position_object=v.object_position.xyz;i.position_world=v.world_position.xyz;
i.normal_object=v.normal.xyz;i.tangent_object=v.tangent.xyz;
i.uv0=v.uv;i.source_time_parameters=v.time.xyz;
'''+ '\n'.join(lines)+f'''
let position=n{root}.xyz;
results[id.x*3u]=vec4<f32>(position,position.x-v.object_position.x);
results[id.x*3u+1u]=vec4<f32>(i.normal_object,0.0);
results[id.x*3u+2u]=vec4<f32>(i.tangent_object,0.0);
}}
'''
rust=r'''
use std::{future::Future,io::Write,sync::Arc,task::{Context,Poll,Wake,Waker},time::Duration};
struct TW(std::thread::Thread);
impl Wake for TW {fn wake(self:Arc<Self>){self.0.unpark();}fn wake_by_ref(self:&Arc<Self>){self.0.unpark();}}
fn block_on<T>(f:impl Future<Output=T>)->T{let mut f=std::pin::pin!(f);let w=Waker::from(Arc::new(TW(std::thread::current())));let mut cx=Context::from_waker(&w);loop{match f.as_mut().poll(&mut cx){Poll::Ready(v)=>return v,Poll::Pending=>std::thread::park_timeout(Duration::from_millis(1))}}}
fn save(p:&str,b:&[u8]){let mut f=std::fs::OpenOptions::new().write(true).create_new(true).open(p).unwrap();f.write_all(b).unwrap();f.sync_all().unwrap();}
fn main(){
let args:Vec<_>=std::env::args().collect();let data=std::fs::read(&args[1]).unwrap();assert_eq!(data.len(),3192*96);
let instance=wgpu::Instance::new(wgpu::InstanceDescriptor{backends:wgpu::Backends::DX12,..wgpu::InstanceDescriptor::new_without_display_handle()});
let adapter=block_on(instance.request_adapter(&wgpu::RequestAdapterOptions{power_preference:wgpu::PowerPreference::HighPerformance,force_fallback_adapter:false,compatible_surface:None})).unwrap();
let info=adapter.get_info();assert_eq!(info.backend,wgpu::Backend::Dx12);
let (device,queue)=block_on(adapter.request_device(&wgpu::DeviceDescriptor{label:Some("Source palm wind parity"),required_limits:adapter.limits(),..Default::default()})).unwrap();
let module=device.create_shader_module(wgpu::ShaderModuleDescriptor{label:Some("Exact source vertex dependency slice"),source:wgpu::ShaderSource::Wgsl(include_str!("wind.wgsl").into())});
let pipeline=device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor{label:None,layout:None,module:&module,entry_point:Some("wind"),compilation_options:Default::default(),cache:None});
let input=device.create_buffer(&wgpu::BufferDescriptor{label:None,size:data.len() as u64,usage:wgpu::BufferUsages::STORAGE|wgpu::BufferUsages::COPY_DST,mapped_at_creation:false});queue.write_buffer(&input,0,&data);
let size=3192*48u64;
let output=device.create_buffer(&wgpu::BufferDescriptor{label:None,size,usage:wgpu::BufferUsages::STORAGE|wgpu::BufferUsages::COPY_SRC,mapped_at_creation:false});
let readback=device.create_buffer(&wgpu::BufferDescriptor{label:None,size,usage:wgpu::BufferUsages::MAP_READ|wgpu::BufferUsages::COPY_DST,mapped_at_creation:false});
let binding=device.create_bind_group(&wgpu::BindGroupDescriptor{label:None,layout:&pipeline.get_bind_group_layout(0),entries:&[wgpu::BindGroupEntry{binding:0,resource:input.as_entire_binding()},wgpu::BindGroupEntry{binding:1,resource:output.as_entire_binding()}]});
let mut encoder=device.create_command_encoder(&Default::default());{let mut pass=encoder.begin_compute_pass(&wgpu::ComputePassDescriptor{label:None,timestamp_writes:None});pass.set_pipeline(&pipeline);pass.set_bind_group(0,&binding,&[]);pass.dispatch_workgroups(3192u32.div_ceil(64),1,1);}
encoder.copy_buffer_to_buffer(&output,0,&readback,0,size);let submission=queue.submit([encoder.finish()]);
let(tx,rx)=std::sync::mpsc::channel();readback.slice(..).map_async(wgpu::MapMode::Read,move |r|tx.send(r).unwrap());
device.poll(wgpu::PollType::Wait{submission_index:Some(submission),timeout:Some(Duration::from_secs(30))}).unwrap();rx.recv_timeout(Duration::from_secs(30)).unwrap().unwrap();
let mapped=readback.slice(..).get_mapped_range();assert_eq!(mapped.len(),size as usize);for b in mapped.chunks_exact(4){assert!(f32::from_le_bytes(b.try_into().unwrap()).is_finite());}save(&args[2],&mapped);drop(mapped);readback.unmap();
let receipt=serde_json::json!({"adapter":info.name,"backend":format!("{:?}",info.backend),"driver":info.driver,"driver_info":info.driver_info,"samples":3192,"output_bytes":size,"actual_mesh_samples":false,"full_import":false});save(&args[3],&serde_json::to_vec_pretty(&receipt).unwrap());println!("{}",receipt);
}
'''
O.mkdir();(O/'src').mkdir()
(O/'src/main.rs').write_text(rust,'utf-8');(O/'src/wind.wgsl').write_text(wgsl,'utf-8')
(O/'Cargo.toml').write_text('''[package]
name="openrpg-palm-wind-gpu-v545"
version="0.1.0"
edition="2024"
[workspace]
[dependencies]
wgpu={version="=29.0.4",default-features=false,features=["wgsl","dx12"]}
serde_json="=1.0.151"
''','utf-8')
inputpath=W/'pirate-palm-wind-native-reference-v541/INPUT.f32'
referencepath=W/'pirate-palm-wind-native-reference-v541/native-output.f32'
for p in [Path(__file__),proofpath,irpath,shaderpath,O/'src/main.rs',O/'src/wind.wgsl',O/'Cargo.toml']:pins[str(p)]=sha(p)
write('PREPARED.v1.json',dict(pins=pins,dependency_nodes=sorted(required,key=int),samples=3192,tolerance=dict(absolute=1e-5,relative=0,normal_tangent_exact=True),scope='Synthetic fixed input source vertex math; not mesh host transforms, textures or final rendered parity',full_import=False))
tool=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
env=os.environ.copy();env.update(RUSTC=str(tool/'rustc.exe'),RUSTDOC=str(tool/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'source-graph-private-boundary-v1/target-rust198-explicit-domain-v8'));env['PATH']=str(tool)+os.pathsep+env['PATH']
def run(args,log):
    with (O/log).open('w',encoding='utf-8') as f:return subprocess.run(args,cwd=O,env=env,stdout=f,stderr=subprocess.STDOUT).returncode
write('RUN-RESULT.v1.json',dict(status='building',pid=os.getpid(),full_import=False))
lock=run([str(tool/'cargo.exe'),'generate-lockfile','--offline'],'lock.log')
assert lock==0,'Offline lock generation failed; inspect lock.log'
pins[str(O/'Cargo.lock')]=sha(O/'Cargo.lock')
build=run([str(tool/'cargo.exe'),'build','--offline','--locked','-j','1'],'build.log')
if build!=0:
    write('RUN-RESULT.v1.json',dict(status='build_failed',exit_code=build,full_import=False));raise SystemExit(build)
exe=Path(env['CARGO_TARGET_DIR'])/'debug/openrpg-palm-wind-gpu-v545.exe'
pins[str(exe)]=sha(exe)
code=run([str(exe),str(inputpath),str(O/'wgsl-output.f32'),str(O/'GPU-RESULT.v1.json')],'gpu.log')
if code!=0:
    write('RUN-RESULT.v1.json',dict(status='gpu_failed',exit_code=code,full_import=False));raise SystemExit(code)
for p,h in pins.items():assert sha(p)==h,p
expected=referencepath.read_bytes();actual=(O/'wgsl-output.f32').read_bytes();assert len(expected)==len(actual)==3192*48
e=struct.unpack('<'+str(len(expected)//4)+'f',expected);a=struct.unpack('<'+str(len(actual)//4)+'f',actual)
failures=[];maximum=0;max_sample=None;category_max={}
labels=['position_x','position_y','position_z','wind_delta','normal_x','normal_y','normal_z','normal_w','tangent_x','tangent_y','tangent_z','tangent_w']
for j,(x,y) in enumerate(zip(e,a)):
    assert math.isfinite(x) and math.isfinite(y)
    error=abs(x-y);label=labels[j%12];category_max[label]=max(category_max.get(label,0),error)
    if error>maximum:maximum=error;max_sample=j//12
    limit=1e-5 if j%12<4 else 0
    if error>limit:failures.append(dict(sample=j//12,field=label,native=x,wgsl=y,absolute_error=error,limit=limit))
write('MISMATCHES.v1.json',failures)
result=dict(status='gpu_numeric_comparison_pass' if not failures else 'gpu_numeric_comparison_failed',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),samples=3192,compared_floats=len(e),failed_components=len(failures),max_absolute_error=maximum,max_error_sample=max_sample,category_max=category_max,tolerance_absolute=1e-5,tolerance_relative=0,normal_tangent_exact=True,native_backend='Direct3D11',wgsl_backend='Dx12',pins=pins,output_sha256=sha(O/'wgsl-output.f32'),full_import=False,actual_mesh_samples=False,studio_verified=False,texture_parity=False,render_parity=False)
write('RESULT.v1.json',result);write('RUN-RESULT.v1.json',dict(status=result['status'],exit_code=code,full_import=False))
print(json.dumps(result))
