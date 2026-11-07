"""Compare real-texture SDK float reference with WGSL at the source's LOD zero."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,struct,math,subprocess
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-pom-gpu-comparison-v607';N=W/'house-pom-native-reference-v606'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(name,value):(O/name).write_text(json.dumps(value,indent=2),'utf-8')
assert not O.exists()
state=read(N/'RUN-RESULT.v1.json');assert state['status']=='house_pom_native_reference_captured_comparison_pending' and state['unity_exit_code']==0 and not state['source_changes']
prep=read(N/'PREPARED.v1.json');native=read(N/'NATIVE-RESULT.v1.json');assert len(native['materials'])==6
pins=dict(prep['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
for row in native['materials']:
    for key in ['raw_output','height_output']:pins[row[key]]=sha(row[key])
for p in [N/'RUN-RESULT.v1.json',N/'PREPARED.v1.json',N/'NATIVE-RESULT.v1.json',Path(__file__)]:pins[str(p)]=sha(p)
# Assert source graph's fixed steps, primitive size, tiling, offset and LOD rather than infer defaults.
ir=read(W/'house-main-material-lot-v603/material-family-active-ir-v1/2209a3797c1d23e49aa921b11b3859d1.json')
clamp=next(n for n in ir['nodes'] if n['opcode']=='ClampNode')
assert all(next(v for v in clamp['inputs'] if v['name']==name)['value']['literal']['x']==32 for name in ['Min','Max'])
pom=next(n for n in ir['nodes'] if n['opcode']=='ParallaxOcclusionMappingNode')
slots={v['name']:v['value'] for v in pom['inputs']}
assert slots['LOD']['literal']==0 and slots['LODThreshold']['literal']==0
assert slots['Tiling']['literal']==dict(x=1.0,y=1.0) and slots['Offset']['literal']==dict(x=0.0,y=0.0) and slots['PrimitiveSize']['literal']==dict(x=1.0,y=1.0)
O.mkdir();(O/'src').mkdir();(O/'outputs').mkdir();(O/'temp').mkdir()
source=S/'candidates/house_source_pom_v604.wgsl';rust=S/'candidates/house_pom_gpu_v607.rs'
wgsl=source.read_text('utf-8-sig')+'''
struct InputCase { uv_scale:vec4<f32>,view:vec4<f32>,tiling_offset:vec4<f32>,texture_st:vec4<f32>,control:vec4<f32>, }
@group(0) @binding(2) var<storage,read> cases:array<InputCase>;
@group(0) @binding(3) var<storage,read_write> results:array<vec4<f32>>;
@compute @workgroup_size(64) fn pom(@builtin(global_invocation_id) id:vec3<u32>){
 if(id.x>=arrayLength(&cases)){return;}let v=cases[id.x];
 let world_to_object=mat4x4<f32>(vec4<f32>(v.uv_scale.z,0,0,0),vec4<f32>(0,1,0,0),vec4<f32>(0,0,v.uv_scale.w,0),vec4<f32>(0,0,0,1));
 let r=evaluate_source_pom(v.uv_scale.xy,v.view.xyz,world_to_object,v.control.x,v.control.y,v.tiling_offset.xy,v.tiling_offset.zw,vec2<f32>(1),v.texture_st.xy,v.texture_st.zw,v.control.z,v.control.w);
 results[id.x]=vec4<f32>(r.uv,r.pixel_depth_offset,r.sampled_height);
}
'''
(O/'src/pom.wgsl').write_text(wgsl,'utf-8');(O/'src/main.rs').write_bytes(rust.read_bytes())
(O/'Cargo.toml').write_text('[package]\nname="openrpg-house-pom-gpu-v607"\nversion="0.1.0"\nedition="2024"\n[workspace]\n[dependencies]\nwgpu={version="=29.0.4",default-features=false,features=["wgsl","dx12"]}\nserde_json="=1.0.151"\n','utf-8')
for p in [source,rust,O/'src/pom.wgsl',O/'src/main.rs',O/'Cargo.toml']:pins[str(p)]=sha(p)
write('PREPARED.v1.json',dict(input_pins=pins,absolute_tolerance=prep['absolute_comparison_tolerance'],relative_tolerance=0,scope='SDK explicit float POM numeric comparison on actual native GPU red mip0; not generated graph precision, entire surface shading, depth host or prefab render qualification',full_import=False))
tool=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin');env=os.environ.copy();env.update(RUSTC=str(tool/'rustc.exe'),RUSTDOC=str(tool/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'source-graph-private-boundary-v1/target-rust198-explicit-domain-v8'),TEMP=str(O/'temp'),TMP=str(O/'temp'));env['PATH']=str(tool)+os.pathsep+env['PATH']
def run(args,log):
    with (O/log).open('x',encoding='utf-8') as f:return subprocess.run(args,cwd=O,env=env,stdout=f,stderr=subprocess.STDOUT).returncode
write('RUN-RESULT.v1.json',dict(status='building',pid=os.getpid(),full_import=False))
lock=run([str(tool/'cargo.exe'),'generate-lockfile','--offline'],'lock.log');assert lock==0
pins[str(O/'Cargo.lock')]=sha(O/'Cargo.lock')
build=run([str(tool/'cargo.exe'),'build','--offline','--locked','-j','1'],'build.log')
if build:write('RUN-RESULT.v1.json',dict(status='build_failed',exit_code=build));raise SystemExit(build)
exe=Path(env['CARGO_TARGET_DIR'])/'debug/openrpg-house-pom-gpu-v607.exe';pins[str(exe)]=sha(exe)
code=run([str(exe),str(N/'NATIVE-RESULT.v1.json'),str(O/'outputs'),str(O/'GPU-RESULT.v1.json')],'gpu.log')
if code:write('RUN-RESULT.v1.json',dict(status='gpu_failed',exit_code=code));raise SystemExit(code)
failures=[];rows=[];outputpins={};labels=['uv_x','uv_y','pixel_depth_offset','sampled_height']
for row in native['materials']:
    actualpath=O/'outputs'/(row['guid']+'.f32');a=actualpath.read_bytes();e=Path(row['raw_output']).read_bytes();assert len(a)==len(e)==row['samples']*16
    expected=struct.unpack('<'+str(len(e)//4)+'f',e);actual=struct.unpack('<'+str(len(a)//4)+'f',a);maximum={k:0 for k in labels};count=0
    for j,(x,y) in enumerate(zip(expected,actual)):
        assert math.isfinite(x) and math.isfinite(y);error=abs(x-y);label=labels[j%4];maximum[label]=max(maximum[label],error)
        if error>prep['absolute_comparison_tolerance']:count+=1;failures.append(dict(material_guid=row['guid'],sample=j//4,field=label,native=x,wgsl=y,absolute_error=error,limit=prep['absolute_comparison_tolerance']))
    rows.append(dict(material_guid=row['guid'],samples=row['samples'],failed_components=count,maximum_errors=maximum,native_filter=row['filter'],native_aniso=row['aniso']));outputpins[str(actualpath)]=sha(actualpath)
for p,h in pins.items():assert sha(p)==h,p
for p in [O/'GPU-RESULT.v1.json']:outputpins[str(p)]=sha(p)
write('MISMATCHES.v1.json',failures)
result=dict(status='house_pom_numeric_comparison_pass' if not failures else 'house_pom_numeric_comparison_mismatches_retained',utc=datetime.now(timezone.utc).isoformat(),materials=6,samples=1296,compared_floats=5184,failed_components=len(failures),rows=rows,absolute_tolerance=prep['absolute_comparison_tolerance'],relative_tolerance=0,actual_gpu_exit_code=code,input_pins=pins,output_pins=outputpins,source_inputs_unchanged=True,actual_generated_graph_precision_verified=False,complete_conversion_increment=0,material_conversion_qualified=False,forge_render_verified=False,full_import=False)
write('RESULT.v1.json',result);write('RUN-RESULT.v1.json',dict(status=result['status'],exit_code=code));print(json.dumps({k:result[k] for k in ['status','materials','samples','failed_components','rows']}))
