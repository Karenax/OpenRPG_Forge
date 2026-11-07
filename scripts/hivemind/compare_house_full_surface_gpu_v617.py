"""Compare complete generated surface and full WGSL candidates at explicit LOD0."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,struct,math,subprocess
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-full-surface-gpu-comparison-v617';N=W/'house-full-surface-reference-v616'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(name,v):(O/name).write_text(json.dumps(v,indent=2),'utf-8')
def explicit_lod(text,name,arity):
    cursor=0
    while True:
        start=text.find(name+'(',cursor)
        if start<0:return text
        a=start+len(name)+1;depth=1
        for b in range(a,len(text)):
            depth+=(text[b]=='(')-(text[b]==')')
            if depth==0:break
        else:raise ValueError('Unclosed texture call')
        content=text[a:b];parts=[];mark=0;depth=0
        for i,c in enumerate(content):
            depth+=(c=='(')-(c==')')
            if c==',' and depth==0:parts.append(content[mark:i]);mark=i+1
        parts.append(content[mark:]);assert len(parts)==arity
        if arity==4:assert parts[3].strip()=='i.texture_mip_bias'
        replacement='textureSampleLevel('+','.join(parts[:3])+',0.0)';text=text[:start]+replacement+text[b+1:];cursor=start+len(replacement)
assert not O.exists()
run=read(N/'RUN-RESULT.v1.json');assert run['status']=='house_full_surface_reference_captured_comparison_pending' and run['unity_exit_code']==0 and not run['source_changes'] and not run['private_importer_changes']
native=read(N/'NATIVE-RESULT.v1.json');prep=read(N/'PREPARED.v1.json');assert native['color_space']=='Linear'
topath=W/'house-native-bound-surface-candidates-v615/RESULT.v1.json';top=read(topath);assert top['materials']==6
pins={}
for proof in [prep,top]:
    for key in ['input_pins','output_pins']:
        for p,h in proof.get(key,{}).items():assert sha(p)==h,p;pins[p]=h
for m in native['materials']:
    pins[m['raw_output']]=sha(m['raw_output'])
    for t in m['textures']:pins[t['raw_output']]=sha(t['raw_output'])
for p in [Path(__file__),topath,N/'RUN-RESULT.v1.json',N/'NATIVE-RESULT.v1.json',N/'PREPARED.v1.json',S/'candidates/house_full_surface_gpu_v617.rs']:pins[str(p)]=sha(p)
O.mkdir();(O/'src').mkdir();(O/'outputs').mkdir();(O/'temp').mkdir();rows={m['material_guid']:m for m in top['rows']}
for m in native['materials']:
    row=rows[m['guid']];source=Path(row['wgsl']);assert sha(source)==row['wgsl_sha256'];text=source.read_text('utf-8-sig')
    assert [b['texture_guid'] for b in row['source_texture_bindings']]==[t['guid'] for t in m['textures']],'Resource ordering differs'
    text=explicit_lod(text,'textureSampleBias',4);text=explicit_lod(text,'textureSample',3)
    binding=read(Path(row['native_binding_manifest']));inputs=binding['required_inputs'];assert len(inputs)==1;mode=next(iter(inputs));assert inputs[mode]==2
    text+=f'''
struct Case {{uv_scale:vec4<f32>,view:vec4<f32>,tiling_offset:vec4<f32>,texture_st:vec4<f32>,control:vec4<f32>,}}
@group(0) @binding(6) var<storage,read> cases:array<Case>;
@group(0) @binding(7) var<storage,read_write> results:array<vec4<f32>>;
@compute @workgroup_size(64) fn surface(@builtin(global_invocation_id) id:vec3<u32>){{
 if(id.x>=arrayLength(&cases)){{return;}}let v=cases[id.x];var i:SourceMaterialInputs;
 i.uv0=vec4<f32>(v.uv_scale.xy,0,0);i.view_direction_ts=v.view.xyz;i.inverse_object_xz_scale=v.uv_scale.zw;i.normal_ts=vec3<f32>(0,0,1);i.normal_world=vec3<f32>(0,1,0);i.tangent_world=vec3<f32>(1,0,0);i.source_vertex_color=vec4<f32>(1);i.{mode}=2;
 let r=evaluate_original_source_material(i);let p=id.x*5u;
 results[p]=vec4<f32>(r.base_color,r.alpha);results[p+1u]=vec4<f32>(r.normal_ts,r.metallic);results[p+2u]=vec4<f32>(r.smoothness,r.occlusion,r.depth_offset,0);results[p+3u]=vec4<f32>(r.emission,0);results[p+4u]=vec4<f32>(r.bent_normal,0);
}}
'''
    target=O/'src'/(m['guid']+'.wgsl');target.write_text(text,'utf-8');pins[str(target)]=sha(target);m['wgsl_path']=str(target);m['input']=str(W/'house-pom-native-reference-v606'/m['guid']/'INPUT.f32');pins[m['input']]=sha(m['input'])
request=O/'GPU-REQUEST.v1.json';request.write_text(json.dumps(native,indent=2),'utf-8');(O/'src/main.rs').write_bytes((S/'candidates/house_full_surface_gpu_v617.rs').read_bytes())
(O/'Cargo.toml').write_text('[package]\nname="openrpg-house-surface-gpu-v617"\nversion="0.1.0"\nedition="2024"\n[workspace]\n[dependencies]\nwgpu={version="=29.0.4",default-features=false,features=["wgsl","dx12"]}\nserde_json="=1.0.151"\n','utf-8')
for p in [request,O/'src/main.rs',O/'Cargo.toml']:pins[str(p)]=sha(p)
write('PREPARED.v1.json',dict(input_pins=pins,materials=6,samples=1296,absolute_tolerance=prep['absolute_tolerance'],relative_tolerance=0,boundary='Actual generated surface function vs full WGSL candidates at explicit texture LOD0 using native decoded GPU RGBA and identical sample inputs',implicit_derivative_lod_verified=False,full_import=False))
tool=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin');env=os.environ.copy();env.update(RUSTC=str(tool/'rustc.exe'),RUSTDOC=str(tool/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'source-graph-private-boundary-v1/target-rust198-explicit-domain-v8'),TEMP=str(O/'temp'),TMP=str(O/'temp'));env['PATH']=str(tool)+os.pathsep+env['PATH']
def execute(args,log):
    with (O/log).open('x',encoding='utf-8') as f:return subprocess.run(args,cwd=O,env=env,stdout=f,stderr=subprocess.STDOUT).returncode
write('RUN-RESULT.v1.json',dict(status='building',pid=os.getpid()))
code=execute([str(tool/'cargo.exe'),'generate-lockfile','--offline'],'lock.log');assert code==0;pins[str(O/'Cargo.lock')]=sha(O/'Cargo.lock')
code=execute([str(tool/'cargo.exe'),'build','--offline','--locked','-j','1'],'build.log')
if code:write('RUN-RESULT.v1.json',dict(status='build_failed',actual_exit_code=code));raise SystemExit(code)
exe=Path(env['CARGO_TARGET_DIR'])/'debug/openrpg-house-surface-gpu-v617.exe';pins[str(exe)]=sha(exe)
code=execute([str(exe),str(request),str(O/'outputs'),str(O/'GPU-RESULT.v1.json')],'gpu.log')
if code:write('RUN-RESULT.v1.json',dict(status='gpu_failed',actual_exit_code=code));raise SystemExit(code)
failures=[];compared=0;comparisons=[];outputpins={};labels=['base_r','base_g','base_b','alpha','normal_x','normal_y','normal_z','metallic','smoothness','occlusion','depth_offset','pad0','emission_r','emission_g','emission_b','pad1','bent_normal_x','bent_normal_y','bent_normal_z','pad2']
for m in native['materials']:
    path=O/'outputs'/(m['guid']+'.f32');a=path.read_bytes();e=Path(m['raw_output']).read_bytes();assert len(a)==len(e)==m['samples']*80;actual=struct.unpack('<'+str(len(a)//4)+'f',a);expected=struct.unpack('<'+str(len(e)//4)+'f',e);maximum={k:0 for k in labels};failed=0
    for j,(x,y) in enumerate(zip(expected,actual)):
        assert math.isfinite(x) and math.isfinite(y);error=abs(x-y);label=labels[j%20];maximum[label]=max(maximum[label],error);compared+=1
        if error>prep['absolute_tolerance']:failed+=1;failures.append(dict(material_guid=m['guid'],sample=j//20,field=label,native=x,wgsl=y,error=error,limit=prep['absolute_tolerance']))
    comparisons.append(dict(material_guid=m['guid'],samples=m['samples'],failed_components=failed,maximum_errors=maximum,byte_exact=a==e));outputpins[str(path)]=sha(path)
for p,h in pins.items():assert sha(p)==h,p
write('MISMATCHES.v1.json',failures)
for p in [O/'GPU-RESULT.v1.json',O/'MISMATCHES.v1.json']:outputpins[str(p)]=sha(p)
result=dict(status='house_full_surface_lod0_gpu_numeric_pass' if not failures else 'house_full_surface_lod0_gpu_mismatches_retained',utc=datetime.now(timezone.utc).isoformat(),materials=6,samples=1296,compared_floats=compared,failed_components=len(failures),rows=comparisons,tolerance_absolute=prep['absolute_tolerance'],actual_gpu_exit_code=code,input_pins=pins,output_pins=outputpins,implicit_derivative_lod_verified=False,forge_render_verified=False,host_depth_verified=False,material_conversion_qualified=False,full_import=False,complete_conversion_increment=0)
write('RESULT.v1.json',result);write('RUN-RESULT.v1.json',dict(status=result['status'],actual_exit_code=code));print(json.dumps({k:result[k] for k in ['status','materials','samples','compared_floats','failed_components','rows']}))
