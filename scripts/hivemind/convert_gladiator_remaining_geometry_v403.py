"""Apply the unchanged reversible native converter to thirteen remaining meshes."""
from pathlib import Path
from datetime import datetime,timezone
import base64,hashlib,json,math,struct,textwrap
W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-remaining-converted-geometry-v403'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
rp=W/'gladiator-remaining-geometry-reopen-v402/RESULT.v1.json';reopened=read(rp);assert reopened['new_meshes']==13 and reopened['native_root_transforms_verified']==5
bp=W/'gladiator-source-binding-bridge-v390/RESULT.v1.json';bridge=read(bp)
pp=W/'gladiator-remaining-native-geometry-v400/PREPARED.v1.json';prep=read(pp)
helper=Path(__file__).with_name('convert_gladiator_native_geometry_v396.py');code=helper.read_text('utf-8')
fragment=textwrap.dedent(code[code.index(' O.mkdir();outputs={};converted=[]'):code.index(' result=dict(')])
compile(fragment,str(helper)+'#unchanged-reversible-converter','exec')
oldp=W/'gladiator-converted-geometry-v396/RESULT.v1.json';old=read(oldp)
assert len(old['meshes'])==4 and old['all_vertex_bytes_reopened_and_reverse_compared'] and old['all_indices_reopened_and_reverse_compared']
pins={str(p):sha(p) for p in [rp,bp,pp,helper,oldp,Path(__file__)]}
for p,h in reopened['input_pins'].items():assert sha(p)==h;pins[p]=h
for p,h in old['output_pins'].items():assert sha(p)==h
O.mkdir();outputs={};cases=[];meshes=[]
for row in reopened['cases']:
    if not row['geometry_captured_in_this_batch']:continue
    nativepath=Path(row['native_case_path']);assert sha(nativepath)==row['native_case_sha256'];pins[str(nativepath)]=sha(nativepath)
    native=read(nativepath);source=next(r for r in bridge['rows'] if r['prefab_guid']==row['prefab_guid'])
    nodes={n['path']:n for n in source['nodes']};bindings=native['bindings']
    for b in bindings:nodes[b['path']]['materials']=b['materials']
    target=O/row['prefab_guid'];scope=dict(O=target,native=native,nodes=nodes,bindings=bindings,base64=base64,struct=struct,math=math,sha=sha)
    exec(compile(fragment,str(helper)+'#unchanged-reversible-converter','exec'),scope)
    assert len(scope['converted'])==row['renderer_bindings']
    for m in scope['converted']:m.update(prefab_guid=row['prefab_guid'],source_prefab=source['source_path'],source_prefab_sha256=source['source_sha256'])
    assert not (set(outputs)&set(scope['outputs']));outputs.update(scope['outputs']);meshes.extend(scope['converted'])
    cases.append(dict(prefab_guid=row['prefab_guid'],meshes=len(scope['converted']),native_root_transform_verified=True,target=str(target)))
assert len(cases)==4 and len(meshes)==13
for p,h in {**pins,**outputs}.items():assert sha(p)==h,p
result=dict(status='thirteen_remaining_native_meshes_converted_reversibly',cases=cases,meshes=meshes,input_pins=pins,output_pins=outputs,prior_four_meshes_reused=True,total_lod_geometry_verified=17,all_vertex_bytes_reopened_and_reverse_compared=True,all_indices_reopened_and_reverse_compared=True,basis=dict(unity_to_target_diagonal=[1,1,-1],normal_z_negated=True,tangent_z_and_handedness_negated=True,winding_reversed=True,uv_channels_unchanged=True),materials_converted=False,renderer_admitted=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2,allow_nan=False),'utf-8')
print(json.dumps(dict(status=result['status'],new_meshes=len(meshes),vertices=sum(m['vertex_count'] for m in meshes),triangles=sum(p['triangles'] for m in meshes for p in m['primitives']),files=len(outputs),total_lod_geometry_verified=17)))
