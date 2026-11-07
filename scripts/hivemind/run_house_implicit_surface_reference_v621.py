"""Render actual generated House surface with native implicit texture derivatives."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,subprocess,sys
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-implicit-surface-reference-v621';PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),converted=False,full_import=False);p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def block(text,start):
    a=text.index('{',start);depth=1
    for i in range(a+1,len(text)):
        depth+=(text[i]=='{')-(text[i]=='}')
        if depth==0:return text[start:i+1]
    raise ValueError('Unclosed source block')
def prepare():
    assert not O.exists()
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    checkpoint=W/'house-native-texture-mips-checkpoint-v620/RESULT.v1.json';proof=read(checkpoint);assert proof['mip_levels']==216
    pins=dict(proof['input_pins'])
    for p,h in pins.items():assert sha(p)==h,p
    for p,h in proof['output_pins'].items():assert sha(p)==h,p;pins[p]=h
    ap=W/'house-native-texture-mips-v619/HELPER-OWNERSHIP.v1.json';audit=read(ap);assert len(audit['helpers'])==23
    assert not any((PROJECT/'Assets').rglob('*.dll')) and {str(p) for p in (PROJECT/'Assets').rglob('*.cs')}=={h['private_path'] for h in audit['helpers']}
    for h in audit['helpers']:assert sha(h['private_path'])==sha(h['controlled_source'])==h['sha256']
    source=W/'house-full-surface-reference-v616/HouseFullSurfaceV616.compute';text=source.read_text('utf-8-sig');body=text[:text.index('StructuredBuffer<float4> _Input;')]
    remove=['#pragma kernel CSMain','#pragma kernel CopyTexture','// Controlled numeric LOD-zero boundary, not an implicit-derivative renderer qualification.','#undef SAMPLE_TEXTURE2D','#define SAMPLE_TEXTURE2D(texture,sampler,coord) SAMPLE_TEXTURE2D_LOD(texture,sampler,coord,0)']
    for line in remove:assert body.count(line)==1;body=body.replace(line,'')
    assert body.count('CBUFFER_START(UnityPerMaterial)')==1;body=body.replace('CBUFFER_START(UnityPerMaterial)','CBUFFER_START(UnityPerMaterial)\nfloat4 _ProbeTiles,_ProbeView,_ProbeScale;float _ProbeMode;')
    generated=W/'house-generated-material-contract-v609/source-generated.shader';original=generated.read_text('utf-8-sig');properties=block(original,original.index('Properties'));assert properties.endswith('}');properties=properties[:-1]+'''[HideInInspector]_ProbeTiles("Probe tiles",Vector)=(1,1,0,0)
[HideInInspector]_ProbeView("Probe view",Vector)=(0,0,1,0)
[HideInInspector]_ProbeScale("Probe inverse scale",Vector)=(1,1,0,0)
[HideInInspector]_ProbeMode("Probe mode",Float)=0
}'''
    shader='Shader "Hidden/OpenRPG/HouseImplicitSurfaceV621" {\n'+properties+'''
SubShader{Pass{Cull Off ZWrite Off ZTest Always
HLSLPROGRAM
#pragma target 4.5
#pragma vertex Vert
#pragma fragment Frag
'''+body+'''
struct ProbeVaryings{float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
ProbeVaryings Vert(uint id:SV_VertexID){ProbeVaryings o;float2 uv=float2((id<<1)&2,id&2);o.position=float4(uv*2-1,0,1);o.uv=uv;return o;}
float4 Frag(ProbeVaryings v):SV_Target{
 if(_ProbeMode>4.5)return float4(v.position.xy,v.uv);
 _OpenRPGWorldToObject=float4x4(_ProbeScale.x,0,0,0,0,1,0,0,0,0,_ProbeScale.y,0,0,0,0,1);
 SurfaceDescriptionInputs i=(SurfaceDescriptionInputs)0;i.uv0=float4(v.uv*_ProbeTiles.xy+_ProbeTiles.zw,0,0);i.TangentSpaceViewDirection=_ProbeView.xyz;i.TangentSpaceNormal=float3(0,0,1);i.WorldSpaceNormal=float3(0,1,0);i.WorldSpaceTangent=float3(1,0,0);i.WorldSpaceBiTangent=float3(0,0,1);i.VertexColor=float4(1,1,1,1);
 SurfaceDescription r=SurfaceDescriptionFunction(i);
 if(_ProbeMode<0.5)return float4(r.BaseColor,r.Alpha);
 if(_ProbeMode<1.5)return float4(r.NormalTS,r.Metallic);
 if(_ProbeMode<2.5)return float4(r.Smoothness,r.Occlusion,r.DepthOffset,0);
 if(_ProbeMode<3.5)return float4(r.Emission,0);
 return float4(r.BentNormal,0);
}
ENDHLSL
}}}
'''
    copies=read(W/'house-native-texture-mips-v619/PREPARED.v1.json')['private_copies']
    for row in copies:assert sha(row['copy'])==row['private_before_sha256']
    mats=read(W/'house-main-material-lot-v603/SOURCE-QUEUE.v1.json')['materials']
    O.mkdir();(O/'temp').mkdir();(O/'frames').mkdir();controlled=O/'HouseImplicitSurfaceV621.shader';controlled.write_text(shader,'utf-8');dst=PROJECT/'Assets/Editor'/controlled.name;assert not dst.exists();dst.write_bytes(controlled.read_bytes())
    base=S/'candidates/HouseImplicitSurfaceV621.cs';helper=PROJECT/'Assets/Editor'/base.name;assert not helper.exists();helper.write_bytes(base.read_bytes());audit['helpers'].append(dict(private_path=str(helper),controlled_source=str(base),sha256=sha(helper)));audit.update(previous_audit=str(ap),checked_utc=datetime.now(timezone.utc).isoformat());ap2=O/'HELPER-OWNERSHIP.v1.json';ap2.write_text(json.dumps(audit,indent=2),'utf-8')
    request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(output=str(O/'NATIVE-RESULT.v1.json'),shader_path=dst.relative_to(PROJECT).as_posix(),folder=str(O/'frames'),material_guids=sorted(mats),tiles=[1,4,16,64],views=[dict(x=.2,y=.1,z=1,w=0),dict(x=.75,y=.25,z=.1,w=0)],size=64),indent=2),'utf-8')
    for p in [Path(__file__),checkpoint,ap,source,generated,controlled,dst,base,helper,ap2,request,S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']:pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_copies=copies,materials=6,frames=48,planes=288,size=64,texture_derivatives='Original SDK SAMPLE_TEXTURE2D in fragment stage; no explicit LOD replacement',output_planes=['base_color_alpha','normal_metallic','smoothness_occlusion_depth','emission','bent_normal','pixel_position_uv_orientation_marker'],scope='Generated surface shader on controlled plane; no prefab mesh, lighting/shadows or depth-host qualification',purchased_scripts_copied_or_executed=False,full_import=False),indent=2),'utf-8');save(dict(status='house_implicit_surface_reference_prepared'))
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=proc.pid,frames=48,planes=288)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8-sig'));nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run'];code=ast.unparse(ast.Module(body=nodes,type_ignores=[])).replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGHouseImplicitSurfaceV621.Export')
    old="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))";assert code.count(old)==1
    code=code.replace(old,"assert len(native['frames'])==48 and native['source_materials_unchanged'] and native['graphics_api']=='Direct3D11' and all(len(f['planes'])==6 and f['source_material_unchanged'] and all(Path(p).stat().st_size==f['width']*f['height']*16 for p in f['planes']) for f in native['frames'])")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','house_implicit_surface_reference_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','house_implicit_surface_reference_captured_comparison_pending').replace('remaining_geometry_capture_refusal_retained','house_implicit_surface_reference_refusal_retained').replace('new_meshes=13','surface_frames=48')
    scope=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save);exec(compile(code,str(recipe)+'#house-fragment-implicit-derivatives','exec'),scope);scope['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
