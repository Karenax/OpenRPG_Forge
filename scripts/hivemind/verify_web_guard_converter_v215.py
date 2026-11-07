"""Verify real golden GLB identity, idempotence and refusal of unsupported guards."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct
from normalize_source_graph_glb import normalize_glb,lower_normal_guards,unpack_glb
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'source-graph-web-guard-converter-reopen-v215'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
conversion=W/'source-graph-web-guard-converter-v214/CONVERSION.v1.json';proof=read(conversion)
candidate=Path(proof['output']);gold=W/'townsmith-web-normal-guard-carrier-v182/SM_Door-Single-B.web-normal-guard.candidate.glb'
assert sha(candidate)==proof['output_sha256']==sha(gold)=='9bbe26cdf4dadf0bd1ede872e9a3733f3181eb8b410afd27c6d5592a9eebb340'
assert sha(Path(proof['input']))==proof['input_sha256']
assert sha(S/'normalize_source_graph_glb.py')==proof['converter_sha256']
assert candidate.read_bytes()==gold.read_bytes()
again,repeated=normalize_glb(candidate.read_bytes());assert again==candidate.read_bytes() and repeated['unchanged'] and not repeated['changes']
negative=[]
examples={
 'outside_normal_abi':'fn arbitrary()->f32 {return bitcast<f32>(0x7fc00000u);}',
 'unknown_selector_type':'fn source_unpack_normal_sample_native(packed:vec4<f32>,mode:f32)->vec4<f32>{return vec4<f32>(bitcast<f32>(0x7fc00000u));}',
 'duplicate_normal_function':'fn source_unpack_normal_sample_native(mode:u32)->f32{return bitcast<f32>(0x7fc00000u);}\nfn source_unpack_normal_sample_native(mode:u32)->f32{return bitcast<f32>(0x7fc00000u);}',
 'unknown_refusal_bits':'fn source_unpack_normal_sample_native(mode:u32)->f32{return bitcast<f32>(0x7f800000u);}' }
for name,text in examples.items():
    try:lower_normal_guards(text)
    except ValueError as exc:negative.append(dict(case=name,refused=True,reason=str(exc)))
    else:raise AssertionError('Unsupported input admitted: '+name)
# Exercise SHA validation with the actual source GLB, not a mocked contract.
raw=bytearray(Path(proof['input']).read_bytes());doc,_=unpack_glb(raw)
size=struct.unpack_from('<I',raw,12)[0];binary_start=28+size
blob=doc['materials'][0]['extensions']['OPENRPG_source_graph']['forward'];view=doc['bufferViews'][blob['buffer_view']]
raw[binary_start+view['byteOffset']]^=1
try:normalize_glb(bytes(raw))
except ValueError as exc:
    assert 'SHA mismatch' in str(exc);negative.append(dict(case='real_glb_shader_byte_corruption',refused=True,reason=str(exc)))
else:raise AssertionError('Changed source shader admitted')
for mode in [0,1,2,3,255,65535,0xffffffff]:
    bits=0x7fc00000|(mode&0x003fffff)
    assert bits&0x7f800000==0x7f800000 and bits&0x007fffff!=0 and bits&0x00400000
browser=W/'browser-source-glb-v183/RUN-RESULT.v1.json';browser_proof=read(browser)
assert browser_proof['status']=='actual_common_browser_glb_binding_draw_and_recovery_passed'
assert browser_proof['browser_gpu_executed'] and browser_proof['browser_material_draw_verified']
pins={str(p):sha(p) for p in [Path(__file__),S/'normalize_source_graph_glb.py',conversion,candidate,gold,Path(proof['input']),browser]}
result=dict(status='reusable_converter_reproduces_exact_webgpu_drawn_glb_and_refuses_unknown_guards',
    exact_gpu_reference_glb_identity=True,idempotent_byte_identity=True,negative_cases=negative,
    accepted_source_arithmetic_geometry_textures_and_metadata_preserved=True,
    gpu_evidence_reused_by_exact_artifact_identity=True,new_gpu_execution=False,
    source_render_equivalence=False,full_import=False,input_pins=pins,
    finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
