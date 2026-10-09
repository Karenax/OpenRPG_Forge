"""Private causal control: fixed-function shadow depth when original depth offset is disabled."""
from pathlib import Path
import json,struct,hashlib,datetime
W=Path('F:/OpenRPG_ImportWork');OLD=W/'firepit-source-shadow-raster-case-v1';ROOT=W/'firepit-source-fixed-shadow-depth-case-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert not ROOT.exists()
prior=read(OLD/'RESULT.v1.json');case=read(OLD/'CASE.v1.json');fixture=Path(case['fixture']);assert sha(fixture)==case['fixture_sha256']
for name,h in prior['input_pins'].items():assert sha(Path(name))==h,name
geometry_path=W/'firepit-source-caster-geometry-reopen-v1/RESULT.v1.json';geometry=read(geometry_path);assert geometry['source_depth_offset_and_alpha_clip_disabled']
for name,h in geometry['input_pins'].items():assert sha(Path(name))==h,name
b=fixture.read_bytes();n=struct.unpack_from('<I',b,12)[0];g=json.loads(b[20:20+n]);blob=bytearray(b[28+n:]);prefix=bytes(blob);patches={};changes=[]
needle='@fragment\nfn fragment(in:SourceGraphSurfaceVertex,@builtin(front_facing)is_front:bool)->SourceGraphNativePrepassOutput {'
branch="""#ifdef SOURCE_GRAPH_SHADOW_PASS
#ifdef UNCLIPPED_DEPTH_ORTHO_EMULATION
source_fixed_shadow_depth_requires_actual_hardware_unclipped_depth();
#endif
@fragment
fn fragment(in:SourceGraphSurfaceVertex,@builtin(front_facing)is_front:bool) {
    let surface=source_graph_build_native_surface(in,is_front);
}
#else
"""
for material in g['materials']:
 graph=material['extensions']['OPENRPG_source_graph'];item=graph['prepass'];key=(item['buffer_view'],item['sha256'])
 if key not in patches:
  v=g['bufferViews'][item['buffer_view']];src=bytes(blob[v['byteOffset']:v['byteOffset']+v['byteLength']]);assert hashlib.sha256(src).hexdigest()==item['sha256']
  text=src.decode().replace("\r\n","\n");assert text.count(needle)==1 and text.endswith('}\n#endif\n')
  patched=text.replace(needle,branch+needle);patched=patched[:-len('}\n#endif\n')]+'}\n#endif\n#endif\n';raw=patched.encode()
  while len(blob)%4:blob.append(0)
  after={'buffer_view':len(g['bufferViews']),'sha256':hashlib.sha256(raw).hexdigest()};g['bufferViews'].append({'buffer':0,'byteOffset':len(blob),'byteLength':len(raw)});blob.extend(raw);patches[key]=after;changes.append({'before':item,'after':after})
 graph['prepass']=patches[key].copy()
while len(blob)%4:blob.append(0)
assert bytes(blob[:len(prefix)])==prefix
g['buffers'][0]['byteLength']=len(blob);g.setdefault('extras',{})['private_fixed_shadow_depth_control']=True
j=json.dumps(g,separators=(',',':')).encode();j+=b' '*((-len(j))%4)
output=struct.pack('<III',0x46546c67,2,28+len(j)+len(blob))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(blob),0x004e4942)+blob
ROOT.mkdir();newfixture=ROOT/'private-fixed-shadow-depth.glb';newfixture.write_bytes(output);case.update(fixture=str(newfixture),fixture_sha256=sha(newfixture));case['private_fixed_shadow_depth_control']={'original_depth_offset_disabled':True,'shadow_fragment_returns_no_explicit_depth':True,'geometry_and_bias_unchanged':True,'full_fidelity_qualified':False}
(ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2),encoding='utf-8')
files=[Path(__file__),geometry_path,OLD/'CASE.v1.json',OLD/'RESULT.v1.json',fixture,newfixture,ROOT/'CASE.v1.json']
result={**prior,'status':'private_source_fixed_shadow_depth_causal_control_prepared','created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'private_fixture':str(newfixture),'private_fixture_sha256':sha(newfixture),'prepass_changes':changes,'original_binary_prefix_unchanged':True,'original_binary_prefix_bytes':len(prefix),'only_changed_behavior':'Shadow fragment output no longer writes frag_depth; fixed function depth matches original depth-offset-disabled source shader. Non-shadow prepass unchanged.','original_geometry_and_raster_bias_unchanged':True,'full_shadow_fidelity_qualified':False,'full_import':False,'input_pins':{**prior['input_pins'],**{str(p):sha(p) for p in files}}}
(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({'root':str(ROOT),'private_fixture':str(newfixture),'prepass_changes':len(changes),'original_binary_prefix_unchanged':True}))
