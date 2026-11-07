"""Measure native GPU color uniforms and packed/decoded normals before surface parity."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,subprocess,sys,re
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-surface-resources-v613';PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
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
    cp=W/'house-surface-candidate-checkpoint-v612/RESULT.v1.json';proof=read(cp);assert proof['all_six_full_surface_candidates_naga_composed']
    pins=dict(proof['input_pins'])
    for p,h in pins.items():assert sha(p)==h,p
    ap=W/'house-generated-material-contract-v609/HELPER-OWNERSHIP.v1.json';audit=read(ap);assert len(audit['helpers'])==20
    assert not any((PROJECT/'Assets').rglob('*.dll')) and {str(p) for p in (PROJECT/'Assets').rglob('*.cs')}=={h['private_path'] for h in audit['helpers']}
    for h in audit['helpers']:assert sha(h['private_path'])==sha(h['controlled_source'])==h['sha256']
    generated=W/'house-generated-material-contract-v609/source-generated.shader';text=generated.read_text('utf-8-sig');properties=block(text,text.index('Properties'))
    sdk=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/BuiltInPackages');functions=sdk/'com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl';source=functions.read_text('utf-8-sig');gamma=block(source,source.index('bool IsGammaSpace()'))
    shader='Shader "Hidden/OpenRPG/HouseSurfaceResourcesV613" {\n'+properties+'''
SubShader {Pass {Cull Off ZWrite Off ZTest Always
HLSLPROGRAM
#pragma target 4.5
#pragma vertex Vert
#pragma fragment Frag
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _AlbedoTintBlend;float _AlbedoContrast,_L1_Roughness_Intensity,_L1_Roughness_Contrast,_Metallic;
CBUFFER_END
Texture2D<float4> _ProbeUvs;
TEXTURE2D(_L1_Height);SAMPLER(sampler_L1_Height);
TEXTURE2D(_L1_Albedo);SAMPLER(sampler_L1_Albedo);
TEXTURE2D(_L1_Normal);SAMPLER(sampler_L1_Normal);
'''+gamma+'''
float4 Vert(uint id:SV_VertexID):SV_POSITION {float2 uv=float2((id<<1)&2,id&2);return float4(uv*2-1,0,1);}
float4 Frag(float4 pixel:SV_POSITION):SV_Target {
 uint x=(uint)pixel.x,y=(uint)pixel.y;float2 uv=_ProbeUvs.Load(int3(x,0,0)).xy;
 if(y==0)return SAMPLE_TEXTURE2D_LOD(_L1_Height,sampler_L1_Height,uv,0);
 if(y==1)return SAMPLE_TEXTURE2D_LOD(_L1_Albedo,sampler_L1_Albedo,uv,0);
 if(y==2)return SAMPLE_TEXTURE2D_LOD(_L1_Normal,sampler_L1_Normal,uv,0);
 if(y==3)return float4(UnpackNormal(SAMPLE_TEXTURE2D_LOD(_L1_Normal,sampler_L1_Normal,uv,0)),1);
 if(y==4)return _AlbedoTintBlend;
 if(y==5)return IsGammaSpace()?LinearToSRGB(_AlbedoTintBlend):_AlbedoTintBlend;
 if(y==6)return float4(_AlbedoContrast,_L1_Roughness_Intensity,_L1_Roughness_Contrast,_Metallic);
 if(y==7){
 float noDxt=0,astc=0;
 #ifdef UNITY_NO_DXT5nm
 noDxt=1;
 #endif
 #ifdef UNITY_ASTC_NORMALMAP_ENCODING
 astc=1;
 #endif
 return float4(REAL_IS_HALF,noDxt,astc,IsGammaSpace()?1:0);
 }
 return float4(x,8,613,1);
}
ENDHLSL
}}}
'''
    queuepath=W/'queue/house-on-a-hill/prefabs.json';q=read(queuepath);mats=read(W/'house-main-material-lot-v603/SOURCE-QUEUE.v1.json')['materials'];copies=read(W/'house-generated-material-contract-v609/PREPARED.v1.json')['private_copies']
    for m in mats.values():
        for propertyname in ['_L1_Albedo','_L1_Normal']:
            rel=q['guid_paths'][m['textures'][propertyname]['guid']]
            for suffix in ['', '.meta']:
                a=Path(q['source_root'])/(rel+suffix);b=PROJECT/(rel+suffix);assert a.is_file() and b.is_file();pins[str(a)]=sha(a);copies.append(dict(source=str(a),copy=str(b),source_sha256=sha(a),private_before_sha256=sha(b)))
    O.mkdir();(O/'temp').mkdir();controlled=O/'HouseSurfaceResourcesV613.shader';controlled.write_text(shader,'utf-8');dst=PROJECT/'Assets/Editor'/controlled.name;assert not dst.exists();dst.write_bytes(controlled.read_bytes())
    helperbase=S/'candidates/HouseSurfaceResourcesV613.cs';helper=PROJECT/'Assets/Editor'/helperbase.name;assert not helper.exists();helper.write_bytes(helperbase.read_bytes());audit['helpers'].append(dict(private_path=str(helper),controlled_source=str(helperbase),sha256=sha(helper)));audit.update(previous_audit=str(ap),checked_utc=datetime.now(timezone.utc).isoformat());auditpath=O/'HELPER-OWNERSHIP.v1.json';auditpath.write_text(json.dumps(audit,indent=2),'utf-8')
    uvs=read(W/'house-pom-native-reference-v606/PREPARED.v1.json')['sample_domains']['uv'];cases=[]
    for guid in sorted(mats):
        folder=O/guid;folder.mkdir();cases.append(dict(guid=guid,raw_output=str(folder/'resource-probe.f32')))
    request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(output=str(O/'NATIVE-RESULT.v1.json'),shader_path=dst.relative_to(PROJECT).as_posix(),uvs=[dict(x=u[0],y=u[1]) for u in uvs],materials=cases),indent=2),'utf-8')
    for p in [Path(__file__),cp,ap,generated,functions,controlled,dst,helperbase,helper,auditpath,request,queuepath,S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']:pins[str(p)]=sha(p)
    for filename in ['Common.hlsl','Color.hlsl','Packing.hlsl']:p=sdk/'com.unity.render-pipelines.core/ShaderLibrary'/filename;pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_copies=copies,materials=6,cases=12,rows=9,row_meanings=['height_rgba_mip0','albedo_rgba_mip0','normal_packed_rgba_mip0','native_unpack_normal_xyz','native_gpu_tint_uniform','native_graph_tint_expression','native_gpu_scalar_uniforms','real_half_no_dxt_astc_gamma_flags','orientation_marker_x_8_613_1'],scope='Actual GPU material resource/uniform boundary; not complete surface or mesh rendering',purchased_scripts_copied_or_executed=False,full_import=False),indent=2),'utf-8');save(dict(status='house_surface_resources_prepared'))
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=proc.pid,materials=6)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8-sig'));nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run'];code=ast.unparse(ast.Module(body=nodes,type_ignores=[])).replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGHouseSurfaceResourcesV613.Export')
    old="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))";assert code.count(old)==1
    code=code.replace(old,"assert len(native['materials'])==6 and native['source_materials_unchanged'] and native['graphics_api']=='Direct3D11' and all(Path(m['raw_output']).stat().st_size==m['output_bytes']==12*9*16 for m in native['materials'])")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','house_surface_resources_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','house_surface_resources_captured_review_pending').replace('remaining_geometry_capture_refusal_retained','house_surface_resources_refusal_retained').replace('new_meshes=13','resource_materials=6')
    scope=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save);exec(compile(code,str(recipe)+'#house-native-surface-resources','exec'),scope);scope['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
