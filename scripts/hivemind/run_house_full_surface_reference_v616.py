"""Execute the actual generated surface on explicit inputs at texture LOD zero."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,re,subprocess,sys
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-full-surface-reference-v616';PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
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
    proofpath=W/'house-native-bound-surface-candidates-v615/RESULT.v1.json';proof=read(proofpath);assert proof['materials']==6 and proof['native_normal_decode_numeric_verified']
    pins=dict(proof['input_pins'])
    for p,h in pins.items():assert sha(p)==h,p
    ap=W/'house-surface-resources-v613/HELPER-OWNERSHIP.v1.json';audit=read(ap);assert len(audit['helpers'])==21
    assert not any((PROJECT/'Assets').rglob('*.dll')) and {str(p) for p in (PROJECT/'Assets').rglob('*.cs')}=={h['private_path'] for h in audit['helpers']}
    for h in audit['helpers']:assert sha(h['private_path'])==sha(h['controlled_source'])==h['sha256']
    source=W/'house-generated-contract-audit-v610/generated-forward.shader';text=source.read_text('utf-8-sig')
    cstart=text.index('CBUFFER_START(UnityPerMaterial)');cend=text.index('CBUFFER_END',cstart)+len('CBUFFER_END');cb=text[cstart:cend];assert cb.count('UNITY_TEXTURE_STREAMING_DEBUG_VARS;')==1;cb=cb.replace('UNITY_TEXTURE_STREAMING_DEBUG_VARS;','')
    variables=re.findall(r'\bfloat[234]?\s+(\w+)\s*;',cb)
    a=text.index('// Object and Global properties');b=text.index('// -- Property used by ScenePickingPass',a);textures=text[a:b]
    a=text.index('// Graph Functions');b=text.index('// Graph Vertex',a);functions=text[a:b]
    inputs=block(text,re.search(r'\bstruct SurfaceDescriptionInputs\b',text).start())+';'
    surface_struct=block(text,re.search(r'\bstruct SurfaceDescription\b',text).start())+';'
    surface=block(text,text.index('SurfaceDescription SurfaceDescriptionFunction('))
    sdk=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/BuiltInPackages');sp=sdk/'com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl';helpers=sp.read_text('utf-8-sig');selected='\n'.join(block(helpers,helpers.index(name)) for name in ['bool IsGammaSpace()','float4 DecodeHDRSample('])
    shader='''#pragma kernel CSMain
#pragma kernel CopyTexture
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
// Controlled numeric LOD-zero boundary, not an implicit-derivative renderer qualification.
#undef SAMPLE_TEXTURE2D
#define SAMPLE_TEXTURE2D(texture,sampler,coord) SAMPLE_TEXTURE2D_LOD(texture,sampler,coord,0)
static float4x4 _OpenRPGWorldToObject;
float4x4 GetWorldToObjectMatrix(){return _OpenRPGWorldToObject;}
'''+cb+'\n'+textures+'\n'+selected+'\n'+inputs+'\n'+functions+'\n'+surface_struct+'\n'+surface+'''
StructuredBuffer<float4> _Input;RWStructuredBuffer<float4> _Output;
Texture2D<float4> _ProbeTexture;RWStructuredBuffer<float4> _Plane;uint _Count,_Width,_HeightSize;
[numthreads(8,8,1)]void CopyTexture(uint3 id:SV_DispatchThreadID){if(id.x>=_Width||id.y>=_HeightSize)return;_Plane[id.y*_Width+id.x]=_ProbeTexture.Load(int3(id.xy,0));}
[numthreads(64,1,1)]void CSMain(uint3 id:SV_DispatchThreadID){
 if(id.x>=_Count)return;uint p=id.x*5;float4 u=_Input[p],v=_Input[p+1];
 _OpenRPGWorldToObject=float4x4(u.z,0,0,0,0,1,0,0,0,0,u.w,0,0,0,0,1);
 SurfaceDescriptionInputs i=(SurfaceDescriptionInputs)0;i.uv0=float4(u.xy,0,0);i.TangentSpaceViewDirection=v.xyz;i.TangentSpaceNormal=float3(0,0,1);i.WorldSpaceNormal=float3(0,1,0);i.WorldSpaceTangent=float3(1,0,0);i.WorldSpaceBiTangent=float3(0,0,1);i.VertexColor=float4(1,1,1,1);
 SurfaceDescription r=SurfaceDescriptionFunction(i);
 _Output[p]=float4(r.BaseColor,r.Alpha);_Output[p+1]=float4(r.NormalTS,r.Metallic);_Output[p+2]=float4(r.Smoothness,r.Occlusion,r.DepthOffset,0);_Output[p+3]=float4(r.Emission,0);_Output[p+4]=float4(r.BentNormal,0);
}
'''
    qpath=W/'queue/house-on-a-hill/prefabs.json';q=read(qpath);mats=read(W/'house-main-material-lot-v603/SOURCE-QUEUE.v1.json')['materials'];copies=read(W/'house-surface-resources-v613/PREPARED.v1.json')['private_copies']
    for row in copies:assert sha(row['copy'])==row['private_before_sha256']
    for m in mats.values():
        for name,t in m['textures'].items():
            if name not in ['_L1_Height','_L1_Albedo','_L1_Normal','_L2_Height','_L2_Albedo','_L2_Normal','_BlendMap']:continue
            rel=q['guid_paths'][t['guid']]
            for suffix in ['', '.meta']:
                a=Path(q['source_root'])/(rel+suffix);b=PROJECT/(rel+suffix);assert a.is_file() and b.is_file();pins[str(a)]=sha(a);copies.append(dict(source=str(a),copy=str(b),source_sha256=sha(a),private_before_sha256=sha(b)))
    O.mkdir();(O/'temp').mkdir();controlled=O/'HouseFullSurfaceV616.compute';controlled.write_text(shader,'utf-8');dst=PROJECT/'Assets/Editor'/controlled.name;assert not dst.exists();dst.write_bytes(controlled.read_bytes())
    base=S/'candidates/HouseFullSurfaceV616.cs';helper=PROJECT/'Assets/Editor'/base.name;assert not helper.exists();helper.write_bytes(base.read_bytes());audit['helpers'].append(dict(private_path=str(helper),controlled_source=str(base),sha256=sha(helper)));audit.update(previous_audit=str(ap),checked_utc=datetime.now(timezone.utc).isoformat());ap2=O/'HELPER-OWNERSHIP.v1.json';ap2.write_text(json.dumps(audit,indent=2),'utf-8')
    cases=[]
    for guid in sorted(mats):
        folder=O/guid;folder.mkdir();inp=W/'house-pom-native-reference-v606'/guid/'INPUT.f32';pins[str(inp)]=sha(inp);cases.append(dict(guid=guid,input=str(inp),raw_output=str(folder/'native-surface.f32'),folder=str(folder),count=216))
    request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(output=str(O/'NATIVE-RESULT.v1.json'),shader_path=dst.relative_to(PROJECT).as_posix(),uniform_names=variables,materials=cases),indent=2),'utf-8')
    for p in [Path(__file__),proofpath,ap,source,sp,controlled,dst,base,helper,ap2,request,qpath,S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']:pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_copies=copies,samples=1296,materials=6,output_stride=80,surface_output_fields=['base_color_xyz_alpha','normal_ts_xyz_metallic','smoothness_occlusion_depth_offset_zero','emission_xyz_zero','bent_normal_xyz_zero'],boundary_changes=['Explicit LOD zero for regular samples instead of implicit derivatives','Explicit per-sample world-to-object matrix','Texture streaming debug fields omitted'],scope='Exact generated surface function and helper math under explicit numeric inputs/LOD0; no mesh, lighting, implicit LOD or depth-host qualification',absolute_tolerance=1e-5,relative_tolerance=0,purchased_scripts_copied_or_executed=False,full_import=False),indent=2),'utf-8');save(dict(status='house_full_surface_reference_prepared'))
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=proc.pid,materials=6,samples=1296)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8-sig'));nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run'];code=ast.unparse(ast.Module(body=nodes,type_ignores=[])).replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGHouseFullSurfaceV616.Export')
    old="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))";assert code.count(old)==1
    code=code.replace(old,"assert len(native['materials'])==6 and native['source_materials_unchanged'] and native['graphics_api']=='Direct3D11' and all(Path(m['raw_output']).stat().st_size==m['output_bytes']==m['samples']*80 for m in native['materials'])")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','house_full_surface_reference_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','house_full_surface_reference_captured_comparison_pending').replace('remaining_geometry_capture_refusal_retained','house_full_surface_reference_refusal_retained').replace('new_meshes=13','surface_samples=1296')
    scope=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save);exec(compile(code,str(recipe)+'#house-full-surface-reference','exec'),scope);scope['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
