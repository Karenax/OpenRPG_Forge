"""Compare native and WGSL surface rasterization with complete mip chains."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,struct,math,subprocess
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-implicit-surface-gpu-comparison-v622';N=W/'house-implicit-surface-reference-v621'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(name,v):(O/name).write_text(json.dumps(v,indent=2),'utf-8')
assert not O.exists()
run=read(N/'RUN-RESULT.v1.json');assert run['status']=='house_implicit_surface_reference_captured_comparison_pending' and run['unity_exit_code']==0 and not run['source_changes'] and not run['private_importer_changes']
native=read(N/'NATIVE-RESULT.v1.json');prep=read(N/'PREPARED.v1.json');assert len(native['frames'])==48 and native['color_space']=='Linear'
tp=W/'house-native-bound-surface-candidates-v615/RESULT.v1.json';top=read(tp);assert top['materials']==6
mp=W/'house-native-texture-mips-checkpoint-v620/MIP-CHAINS.v1.json';chains={r['texture_guid']:r for r in read(mp)}
pins={}
for proof in [prep,top]:
    for key in ['input_pins','output_pins']:
        for p,h in proof.get(key,{}).items():assert sha(p)==h,p;pins[p]=h
for f in native['frames']:
    for p in f['planes']:pins[p]=sha(p)
for p in [Path(__file__),tp,mp,N/'RUN-RESULT.v1.json',N/'NATIVE-RESULT.v1.json',N/'PREPARED.v1.json',S/'candidates/house_implicit_surface_gpu_v622.rs']:pins[str(p)]=sha(p)
O.mkdir();(O/'src').mkdir();(O/'outputs').mkdir();(O/'temp').mkdir();materials=[]
for material in top['rows']:
    guid=material['material_guid'];source=Path(material['wgsl']);assert sha(source)==material['wgsl_sha256'];text=source.read_text('utf-8-sig');binding=read(Path(material['native_binding_manifest']));mode=next(iter(binding['required_inputs']));assert binding['required_inputs'][mode]==2
    text+=f'''
struct FrameParams{{tiles:vec4<f32>,view:vec4<f32>,scale_mode:vec4<f32>,}}
@group(0) @binding(6) var<uniform> frame:FrameParams;
struct ProbeVaryings{{@builtin(position) position:vec4<f32>,@location(0) uv:vec2<f32>,}}
@vertex fn vert(@builtin(vertex_index) id:u32)->ProbeVaryings{{var o:ProbeVaryings;let uv=vec2<f32>(f32((id<<1u)&2u),f32(id&2u));o.position=vec4<f32>(uv*2.0-vec2<f32>(1),0,1);o.uv=uv;return o;}}
@fragment fn frag(v:ProbeVaryings)->@location(0) vec4<f32>{{
 if(frame.scale_mode.z>4.5){{return vec4<f32>(v.position.xy,v.uv);}}
 var i:SourceMaterialInputs;i.uv0=vec4<f32>(v.uv*frame.tiles.xy+frame.tiles.zw,0,0);i.view_direction_ts=frame.view.xyz;i.inverse_object_xz_scale=frame.scale_mode.xy;i.normal_ts=vec3<f32>(0,0,1);i.normal_world=vec3<f32>(0,1,0);i.tangent_world=vec3<f32>(1,0,0);i.source_vertex_color=vec4<f32>(1);i.{mode}=2;
 let r=evaluate_original_source_material(i);
 if(frame.scale_mode.z<0.5){{return vec4<f32>(r.base_color,r.alpha);}}
 if(frame.scale_mode.z<1.5){{return vec4<f32>(r.normal_ts,r.metallic);}}
 if(frame.scale_mode.z<2.5){{return vec4<f32>(r.smoothness,r.occlusion,r.depth_offset,0);}}
 if(frame.scale_mode.z<3.5){{return vec4<f32>(r.emission,0);}}
 return vec4<f32>(r.bent_normal,0);
}}
'''
    target=O/'src'/(guid+'.wgsl');target.write_text(text,'utf-8');pins[str(target)]=sha(target);frames=[]
    for frame in native['frames']:
        if frame['material_guid']!=guid:continue
        frame=dict(frame);frame['key']=guid+'/'+Path(frame['folder']).name;frames.append(frame)
    assert len(frames)==8
    textures=[chains[t['texture_guid']] for t in material['source_texture_bindings']];assert len(textures)==3
    materials.append(dict(guid=guid,wgsl_path=str(target),textures=textures,frames=frames))
request=O/'GPU-REQUEST.v1.json';request.write_text(json.dumps(dict(materials=materials),indent=2),'utf-8');(O/'src/main.rs').write_bytes((S/'candidates/house_implicit_surface_gpu_v622.rs').read_bytes())
(O/'Cargo.toml').write_text('[package]\nname="openrpg-house-implicit-gpu-v622"\nversion="0.1.0"\nedition="2024"\n[workspace]\n[dependencies]\nwgpu={version="=29.0.4",default-features=false,features=["wgsl","dx12"]}\nserde_json="=1.0.151"\n','utf-8')
for p in [request,O/'src/main.rs',O/'Cargo.toml']:pins[str(p)]=sha(p)
write('PREPARED.v1.json',dict(input_pins=pins,materials=6,frames=48,planes=288,absolute_tolerance=1e-5,relative_tolerance=0,orientation_marker_tolerance=0,scope='Actual generated surface rasterization vs WGSL on controlled planes with native mip chains/implicit derivatives; no lighting, prefab mesh or depth-host qualification',full_import=False))
tool=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin');env=os.environ.copy();env.update(RUSTC=str(tool/'rustc.exe'),RUSTDOC=str(tool/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'source-graph-private-boundary-v1/target-rust198-explicit-domain-v8'),TEMP=str(O/'temp'),TMP=str(O/'temp'));env['PATH']=str(tool)+os.pathsep+env['PATH']
def execute(args,log):
    with (O/log).open('x',encoding='utf-8') as f:return subprocess.run(args,cwd=O,env=env,stdout=f,stderr=subprocess.STDOUT).returncode
write('RUN-RESULT.v1.json',dict(status='building',pid=os.getpid()))
code=execute([str(tool/'cargo.exe'),'generate-lockfile','--offline'],'lock.log');assert code==0;pins[str(O/'Cargo.lock')]=sha(O/'Cargo.lock')
code=execute([str(tool/'cargo.exe'),'build','--offline','--locked','-j','1'],'build.log')
if code:write('RUN-RESULT.v1.json',dict(status='build_failed',actual_exit_code=code));raise SystemExit(code)
exe=Path(env['CARGO_TARGET_DIR'])/'debug/openrpg-house-implicit-gpu-v622.exe';pins[str(exe)]=sha(exe)
code=execute([str(exe),str(request),str(O/'outputs'),str(O/'GPU-RESULT.v1.json')],'gpu.log')
if code:write('RUN-RESULT.v1.json',dict(status='gpu_failed',actual_exit_code=code));raise SystemExit(code)
summary=[];failures=[];failed_total=0;compared=0;outputpins={};marker_errors=0
for material in materials:
    for frame in material['frames']:
        for mode,path in enumerate(frame['planes']):
            actualpath=O/'outputs'/frame['key']/('plane-'+str(mode)+'.f32');a=actualpath.read_bytes();e=Path(path).read_bytes();assert len(a)==len(e)==64*64*16
            actual=struct.unpack('<16384f',a);expected=struct.unpack('<16384f',e);limit=0 if mode==5 else 1e-5;maximum=0;failed=0
            for j,(x,y) in enumerate(zip(expected,actual)):
                assert math.isfinite(x) and math.isfinite(y);error=abs(x-y);maximum=max(maximum,error);compared+=1
                if error>limit:
                    failed+=1;failed_total+=1;marker_errors+=mode==5
                    if len(failures)<20000:failures.append(dict(key=frame['key'],plane=mode,pixel=j//4,component=j%4,native=x,wgsl=y,error=error,limit=limit))
            summary.append(dict(key=frame['key'],plane=mode,failed_components=failed,max_absolute_error=maximum,byte_exact=a==e));outputpins[str(actualpath)]=sha(actualpath)
for p,h in pins.items():assert sha(p)==h,p
write('MISMATCHES.v1.json',failures);write('PLANE-COMPARISONS.v1.json',summary)
for p in [O/'GPU-RESULT.v1.json',O/'MISMATCHES.v1.json',O/'PLANE-COMPARISONS.v1.json']:outputpins[str(p)]=sha(p)
result=dict(status='house_implicit_surface_gpu_comparison_pass' if not failed_total else 'house_implicit_surface_gpu_mismatches_retained',utc=datetime.now(timezone.utc).isoformat(),materials=6,frames=48,planes=288,compared_floats=compared,failed_components=failed_total,orientation_marker_failed_components=marker_errors,max_absolute_error=max(r['max_absolute_error'] for r in summary),mismatch_examples_retained=len(failures),input_pins=pins,output_pins=outputpins,actual_gpu_exit_code=code,implicit_sampling_verified=failed_total==0,host_depth_verified=False,prefab_mesh_render_verified=False,forge_render_verified=False,full_import=False,complete_conversion_increment=0)
write('RESULT.v1.json',result);write('RUN-RESULT.v1.json',dict(status=result['status'],actual_exit_code=code));print(json.dumps({k:result[k] for k in ['status','frames','planes','failed_components','orientation_marker_failed_components','max_absolute_error']}))
