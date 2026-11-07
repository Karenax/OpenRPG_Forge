"""Compare correct DXT5nm GPU decode with retained actual native texel samples."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,os,struct,math,subprocess
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-normal-decode-gpu-v614';N=W/'house-surface-resources-v613'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(name,v):(O/name).write_text(json.dumps(v,indent=2),'utf-8')
assert not O.exists()
run=read(N/'RUN-RESULT.v1.json');assert run['unity_exit_code']==0 and not run['source_changes'] and not run['private_importer_changes']
native=read(N/'NATIVE-RESULT.v1.json');assert native['color_space']=='Linear' and native['cases']==12 and len(native['materials'])==6
pins=dict(read(N/'PREPARED.v1.json')['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
inputs=[];expected=[];bindings=[]
contract=read(W/'house-generated-material-contract-v609/NATIVE-RESULT.v1.json');properties={m['guid']:{p['name']:p for p in m['properties']} for m in contract['materials']}
for material in native['materials']:
    path=Path(material['raw_output']);pins[str(path)]=sha(path);v=struct.unpack('<432f',path.read_bytes());rows=[v[i*48:(i+1)*48] for i in range(9)]
    if rows[0][2]==613:rows=rows[::-1]
    assert all(rows[8][i*4:i*4+4]==(float(i),8.0,613.0,1.0) for i in range(12)),material['guid']
    assert all(rows[7][i*4:i*4+4]==(0.0,0.0,0.0,0.0) for i in range(12)),'Target precision/normal/colorspace differs'
    tint=tuple(properties[material['guid']]['_AlbedoTintBlend']['value_vector'][k] for k in ['x','y','z','w'])
    assert all(rows[4][i*4:i*4+4]==rows[5][i*4:i*4+4]==tint for i in range(12)),'GPU tint differs from native captured binding'
    uniforms=tuple(properties[material['guid']][k]['value_float'] for k in ['_AlbedoContrast','_L1_Roughness_Intensity','_L1_Roughness_Contrast','_Metallic']);assert all(rows[6][i*4:i*4+4]==uniforms for i in range(12))
    normal=next(t for t in material['textures'] if t['property']=='_L1_Normal');assert normal['format']=='DXT5' and normal['graphics_format']=='RGBA_DXT5_UNorm'
    inputs.extend(rows[2]);expected.extend(rows[3]);bindings.append(dict(material_guid=material['guid'],sample_offset=len(inputs)//4-12,normal_guid=normal['guid'],storage=normal['graphics_format'],decode='UnpackNormalMapRGorAG: alpha*=red, AG xy, reconstructed z',gpu_tint=tint,scalar_uniforms=uniforms,real_is_half=False,no_dxt5nm=False,astc_normal=False,gamma_space=False))
source_recipe=S/'run_palm_wind_gpu_comparison_v545.py';tree=ast.parse(source_recipe.read_text('utf-8-sig'));assign=next(n for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='rust' for t in n.targets));rust=ast.literal_eval(assign.value)
rust=rust.replace('3192','72').replace('72*96','72*16').replace('72*48u64','72*16u64').replace('include_str!("wind.wgsl")','include_str!("normal.wgsl")').replace('Some("wind")','Some("normal_decode")').replace('Source palm wind parity','Source House native normal decode parity').replace('Exact source vertex dependency slice','SDK native DXT5nm decode')
wgsl='''
// Source SDK Packing.hlsl UnpackNormalMapRGorAG / UnpackNormalAG, native target flags pinned.
@group(0) @binding(0) var<storage,read> cases:array<vec4<f32>>;
@group(0) @binding(1) var<storage,read_write> results:array<vec4<f32>>;
@compute @workgroup_size(64) fn normal_decode(@builtin(global_invocation_id) id:vec3<u32>){
 if(id.x>=arrayLength(&cases)){return;}let encoded=cases[id.x];
 let alpha=encoded.a*encoded.r;let xy=vec2<f32>(alpha,encoded.g)*2.0-vec2<f32>(1);
 let z=max(1.0e-16,sqrt(1.0-clamp(dot(xy,xy),0.0,1.0)));
 results[id.x]=vec4<f32>(xy,z,1);
}
'''
for p in [Path(__file__),source_recipe,N/'RUN-RESULT.v1.json',N/'PREPARED.v1.json',N/'NATIVE-RESULT.v1.json',W/'house-generated-material-contract-v609/NATIVE-RESULT.v1.json']:pins[str(p)]=sha(p)
O.mkdir();(O/'src').mkdir();(O/'temp').mkdir();(O/'src/main.rs').write_text(rust,'utf-8');(O/'src/normal.wgsl').write_text(wgsl,'utf-8')
(O/'INPUT.f32').write_bytes(struct.pack('<288f',*inputs));(O/'native-normal.f32').write_bytes(struct.pack('<288f',*expected));write('NATIVE-BINDINGS.v1.json',bindings)
(O/'Cargo.toml').write_text('[package]\nname="openrpg-house-normal-gpu-v614"\nversion="0.1.0"\nedition="2024"\n[workspace]\n[dependencies]\nwgpu={version="=29.0.4",default-features=false,features=["wgsl","dx12"]}\nserde_json="=1.0.151"\n','utf-8')
for p in [O/'src/main.rs',O/'src/normal.wgsl',O/'INPUT.f32',O/'native-normal.f32',O/'Cargo.toml',O/'NATIVE-BINDINGS.v1.json']:pins[str(p)]=sha(p)
write('PREPARED.v1.json',dict(input_pins=pins,samples=72,absolute_tolerance=1e-5,relative_tolerance=0,scope='Native GPU packed normal sample decode and actual material GPU uniforms; no entire surface or mesh render qualification',full_import=False))
tool=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin');env=os.environ.copy();env.update(RUSTC=str(tool/'rustc.exe'),RUSTDOC=str(tool/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'source-graph-private-boundary-v1/target-rust198-explicit-domain-v8'),TEMP=str(O/'temp'),TMP=str(O/'temp'));env['PATH']=str(tool)+os.pathsep+env['PATH']
def execute(args,log):
    with (O/log).open('x',encoding='utf-8') as f:return subprocess.run(args,cwd=O,env=env,stdout=f,stderr=subprocess.STDOUT).returncode
write('RUN-RESULT.v1.json',dict(status='building',pid=os.getpid()))
lock=execute([str(tool/'cargo.exe'),'generate-lockfile','--offline'],'lock.log');assert lock==0;pins[str(O/'Cargo.lock')]=sha(O/'Cargo.lock')
code=execute([str(tool/'cargo.exe'),'build','--offline','--locked','-j','1'],'build.log')
if code:write('RUN-RESULT.v1.json',dict(status='build_failed',actual_exit_code=code));raise SystemExit(code)
exe=Path(env['CARGO_TARGET_DIR'])/'debug/openrpg-house-normal-gpu-v614.exe';pins[str(exe)]=sha(exe)
code=execute([str(exe),str(O/'INPUT.f32'),str(O/'wgsl-normal.f32'),str(O/'GPU-RESULT.v1.json')],'gpu.log')
if code:write('RUN-RESULT.v1.json',dict(status='gpu_failed',actual_exit_code=code));raise SystemExit(code)
actual=struct.unpack('<288f',(O/'wgsl-normal.f32').read_bytes());failures=[];maximum=0
for i,(x,y) in enumerate(zip(expected,actual)):
    assert math.isfinite(x) and math.isfinite(y);error=abs(x-y);maximum=max(maximum,error)
    if error>1e-5:failures.append(dict(sample=i//4,field='xyzw'[i%4],native=x,wgsl=y,error=error))
for p,h in pins.items():assert sha(p)==h,p
write('MISMATCHES.v1.json',failures)
result=dict(status='house_native_normal_decode_and_gpu_bindings_verified' if not failures else 'house_native_normal_decode_mismatches_retained',utc=datetime.now(timezone.utc).isoformat(),materials=6,samples=72,compared_floats=288,failed_components=len(failures),maximum_absolute_error=maximum,tolerance_absolute=1e-5,actual_gpu_exit_code=code,input_pins=pins,output_pins={str(p):sha(p) for p in [O/'wgsl-normal.f32',O/'GPU-RESULT.v1.json',O/'MISMATCHES.v1.json']},native_dxt5nm_decode_verified=not failures,native_gpu_color_and_scalar_bindings_verified=True,native_target_precision_float=True,complete_surface_gpu_verified=False,material_conversion_qualified=False,full_import=False,complete_conversion_increment=0)
write('RESULT.v1.json',result);write('RUN-RESULT.v1.json',dict(status=result['status'],actual_exit_code=code));print(json.dumps({k:result[k] for k in ['status','samples','failed_components','maximum_absolute_error']}))
