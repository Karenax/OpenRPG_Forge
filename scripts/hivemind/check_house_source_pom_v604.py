"""Validate the SDK-derived POM candidate; preserve runtime qualification gaps."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,subprocess
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-source-pom-wgsl-v604'
SDK=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/BuiltInPackages')
TOOLS=W/'native-hdrp-nine-source-programs-v1/source-graph-real-lighting-composer-v2'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
proofpath=W/'house-main-material-lot-v603/RESULT.v1.json';proof=read(proofpath)
assert proof['materials_compiled']==6 and not proof['errors']
for p,h in proof['input_pins'].items():assert sha(Path(p))==h,p
for p,h in proof['output_pins'].items():assert sha(Path(p))==h,p
sdk_node=SDK/'com.unity.shadergraph/Editor/Data/Nodes/UV/ParallaxOcclusionMappingNode.cs'
sdk_pom=SDK/'com.unity.render-pipelines.core/ShaderLibrary/PerPixelDisplacement.hlsl'
assert 'private Channel m_Channel = Channel.Red;' in sdk_node.read_text('utf-8-sig')
assert '#define POM_SECANT_METHOD 1' in sdk_pom.read_text('utf-8-sig')
closure=read(W/'house-main-material-lot-v603/SHADER-SUBGRAPH-CLOSURE.v1.json')
original_nodes=[n for n in closure['graphs'][0]['objects'] if n.get('m_Type','').endswith('.ParallaxOcclusionMappingNode')]
assert len(original_nodes)==2 and all('m_Channel' not in n for n in original_nodes)
source=S/'candidates/house_source_pom_v604.wgsl';tool=TOOLS/'validate_real_bevy_closure.exe';modules=TOOLS/'module-map.tsv';toolproof=read(TOOLS/'RESULT.v1.json')
assert sha(tool)==toolproof['exe_sha256'] and sha(modules)==toolproof['module_map_sha256']
pins={str(p):sha(p) for p in [Path(__file__),source,proofpath,sdk_node,sdk_pom,tool,modules,TOOLS/'RESULT.v1.json']}
O.mkdir();probe=O/'pom-fragment-type-probe.wgsl'
probe.write_text(source.read_text('utf-8')+'''
@fragment fn pom_type_admission(@location(0) uv:vec2<f32>)->@location(0) vec4<f32> {
    let identity=mat4x4<f32>(vec4<f32>(1.0,0.0,0.0,0.0),vec4<f32>(0.0,1.0,0.0,0.0),vec4<f32>(0.0,0.0,1.0,0.0),vec4<f32>(0.0,0.0,0.0,1.0));
    let r=evaluate_source_pom(uv,vec3<f32>(0.2,0.1,1.0),identity,0.005,16.0,vec2<f32>(1.0),vec2<f32>(0.0),vec2<f32>(1.0),vec2<f32>(1.0),vec2<f32>(0.0),0.0,0.0);
    return vec4<f32>(r.uv,r.pixel_depth_offset,r.sampled_height);
}
''','utf-8');pins[str(probe)]=sha(probe)
run=subprocess.run([str(tool),str(modules),str(probe),'MATERIAL_BIND_GROUP=0'],capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW)
(O/'stdout.log').write_bytes(run.stdout);(O/'stderr.log').write_bytes(run.stderr)
passed=run.returncode==0 and b'PASS_REAL_BEVY0191_NAGA_OIL_COMPOSITION' in run.stdout
for p,h in pins.items():assert sha(Path(p))==h,p
result=dict(status='source_pom_wgsl_type_composition_passed' if passed else 'source_pom_wgsl_type_refusal_retained',actual_exit_code=run.returncode,input_pins=pins,materials_in_lot=6,prefab_cards_in_lot=24,source_red_channel_default_pinned=True,algorithm_scope='SDK linear search, three secant iterations, explicit sampled LOD, LOD fade, centimetre-to-metre amplitude, original texture UV transform and inverse object scale.',gpu_executed=False,native_sampling_parity_verified=False,source_precision_verified=False,pixel_depth_host_verified=False,complete_material_conversion=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
