"""GPU-evaluate the actual generated Forward vertex function on explicit input samples."""
from pathlib import Path
from datetime import datetime, timezone
import ast, hashlib, json, math, re, struct, subprocess, sys
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-palm-wind-native-reference-v541'
PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),converted=False,full_import=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def block(text,start):
    opening=text.index('{',start);depth=1
    for i in range(opening+1,len(text)):
        depth+=(text[i]=='{')-(text[i]=='}')
        if depth==0:return text[start:i+1]
    raise ValueError('Unclosed source block')
def function(text,name):
    matches=list(re.finditer(r'\b(?:void|float[234]?|VertexDescription)\s+'+re.escape(name)+r'\s*\(',text))
    assert len(matches)==1,name
    return block(text,matches[0].start())
def prepare():
    assert not O.exists()
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    proofpath=W/'pirate-palm-wgsl-delivery-v540/RESULT.v1.json';proof=read(proofpath)
    assert proof['actual_naga_exit_code']==0
    pins=dict(proof['input_pins'])
    for p,h in pins.items():assert sha(p)==h,p
    auditpath=W/'pirate-palm-native-contract-v535/HELPER-OWNERSHIP.v1.json';audit=read(auditpath)
    assert not audit['asset_dll_found']
    for h in audit['helpers']:
        assert sha(h['private_path'])==sha(h['controlled_source'])==h['sha256']
    assert {str(p) for p in (PROJECT/'Assets').rglob('*.cs')}=={h['private_path'] for h in audit['helpers']}
    assert not any((PROJECT/'Assets').rglob('*.dll'))
    generated=W/'pirate-palm-native-contract-v535/source-generated.shader';text=generated.read_text('utf-8')
    passes=[block(text,m.start()) for m in re.finditer(r'\bPass\s*\{',text)]
    selected=[p for p in passes if re.search(r'\bName\s+"Forward"',p.split('HLSLPROGRAM',1)[0])]
    assert len(selected)==1;forward=selected[0]
    vertex=function(forward,'VertexDescriptionFunction')
    hashes=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl')
    functions=[];visited=set()
    def add(name):
        if name in visited:return
        visited.add(name)
        body=function(hashes.read_text('utf-8') if name.startswith('Hash_') else forward,name)
        for child in sorted(set(re.findall(r'\b((?:Unity_|Hash_)[A-Za-z0-9_]+)\s*\(',body))-{name}):add(child)
        functions.append(body)
    for name in sorted(set(re.findall(r'\b((?:Unity_|Hash_)[A-Za-z0-9_]+)\s*\(',vertex))):add(name)
    structs=[]
    for name in ['VertexDescriptionInputs','VertexDescription']:
        match=re.search(r'\bstruct\s+'+name+r'\b',forward);assert match
        structs.append(block(forward,match.start())+';')
    kernel='#pragma kernel CSMain\nfloat _WindSpeed,_WindStrength;\n'+ '\n'.join(structs+functions+[vertex])+r'''
struct InputCase {float4 object_position,world_position,normal,tangent,uv,time;};
StructuredBuffer<InputCase> _Input;RWStructuredBuffer<float4> _Output;uint _Count;
[numthreads(64,1,1)] void CSMain(uint3 id:SV_DispatchThreadID){if(id.x>=_Count)return;InputCase v=_Input[id.x];VertexDescriptionInputs i=(VertexDescriptionInputs)0;i.ObjectSpacePosition=v.object_position.xyz;i.WorldSpacePosition=v.world_position.xyz;i.ObjectSpaceNormal=v.normal.xyz;i.ObjectSpaceTangent=v.tangent.xyz;i.uv0=v.uv;i.TimeParameters=v.time.xyz;VertexDescription r=VertexDescriptionFunction(i);_Output[id.x*3]=float4(r.Position,r.Position.x-v.object_position.x);_Output[id.x*3+1]=float4(r.Normal,0);_Output[id.x*3+2]=float4(r.Tangent,0);}
'''
    native=read(W/'pirate-palm-native-contract-v535/NATIVE-RESULT.v1.json');props={p['name']:p for p in native['materials'][0]['properties']}
    O.mkdir();(O/'temp').mkdir()
    shader=PROJECT/'Assets/Editor/PiratePalmWindReferenceV541.compute';assert not shader.exists()
    controlled=O/'native-forward-wind.compute';controlled.write_text(kernel,'utf-8');shader.write_bytes(controlled.read_bytes())
    base=S/'candidates/PiratePalmWindReferenceV541.cs';helper=PROJECT/'Assets/Editor'/base.name;assert not helper.exists();helper.write_bytes(base.read_bytes())
    audit['helpers'].append(dict(private_path=str(helper),controlled_source=str(base),sha256=sha(helper)))
    audit.update(checked_utc=datetime.now(timezone.utc).isoformat(),previous_audit=str(auditpath))
    (O/'HELPER-OWNERSHIP.v1.json').write_text(json.dumps(audit,indent=2),'utf-8')
    xs=[-2048.125,-289.5,-289,-288.999,-42.75,-3.5,-1,-0.001,0,0.125,1,3.75,41.5,288.999,289,290,512.5,1024,2048]
    times=[0,0.01,0.125,0.5,1,3.14,7.125,31];uvs=[0,0.25,0.5,1,2,-0.5,1.3];offsets=[0,2.25,-14.375]
    samples=[]
    for x in xs:
        for t in times:
            for uv in uvs:
                for offset in offsets:
                    samples.append([x,2.5,-4,0,x+offset,2.5,-4,0,0,1,0,0,1,0,0,0,0.375,uv,0,0,t,math.sin(t),math.cos(t),0])
    inputpath=O/'INPUT.f32';inputpath.write_bytes(b''.join(struct.pack('<24f',*s) for s in samples));count=len(samples);assert count==3192
    request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(output=str(O/'NATIVE-RESULT.v1.json'),raw_output=str(O/'native-output.f32'),input=str(inputpath),count=count,shader_path=shader.relative_to(PROJECT).as_posix(),wind_speed=props['_WindSpeed']['value_float'],wind_strength=props['_WindStrength']['value_float']),indent=2),'utf-8')
    for p in [Path(__file__),proofpath,auditpath,hashes,generated,controlled,shader,base,helper,inputpath,request,O/'HELPER-OWNERSHIP.v1.json',S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']:pins[str(p)]=sha(p)
    copies=read(W/'pirate-palm-native-contract-v535/PREPARED.v1.json')['private_copies']
    for row in copies:assert sha(row['copy'])==row['private_before_sha256'],row['copy']
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_copies=copies,samples=count,source_pass='Forward',source_vertex_function=vertex,source_helper_functions=functions,sample_domains=dict(object_x=xs,time=times,uv_y=uvs,world_x_offset=offsets),scope='Vertex equations with explicit inputs; synthetic numeric samples, no mesh/render/host transformation qualification.',purchased_scripts_copied_or_executed=False),indent=2),'utf-8')
    save(dict(status='native_forward_wind_gpu_reference_prepared'))
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=proc.pid,samples=count)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8'))
    nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run']
    code=ast.unparse(ast.Module(body=nodes,type_ignores=[])).replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGPiratePalmWindReferenceV541.Export')
    old="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))"
    assert code.count(old)==1
    code=code.replace(old,"assert native['samples']==prep['samples'] and native['source_materials_unchanged'] and native['graphics_api']=='Direct3D11' and Path(native['raw_output']).stat().st_size==native['output_bytes']==native['samples']*48")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','native_forward_wind_gpu_reference_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','native_forward_wind_gpu_reference_captured_review_pending').replace('new_meshes=13','native_wind_samples=3192')
    scope=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save)
    exec(compile(code,str(recipe)+'#actual-forward-function-gpu-reference','exec'),scope);scope['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
