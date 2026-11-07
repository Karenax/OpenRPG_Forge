"""Reuse the reversible native buffer conversion for all 24 House prefabs."""
from pathlib import Path
from datetime import datetime,timezone
import ast,base64,hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');N=W/'house-native-prefab-geometry-v651';O=W/'house-converted-native-geometry-v652';S=Path(__file__).resolve().parent
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists();run=read(N/'RUN-RESULT.v1.json');assert run['unity_exit_code']==0 and not run['source_changes'] and not run['private_importer_changes']
native=read(N/'NATIVE-RESULT.v1.json');assert len(native['cases'])==24 and sum(len(c['meshes']) for c in native['cases'])==42
pins={}
for p,h in read(N/'PREPARED.v1.json')['input_pins'].items():assert sha(p)==h,p;pins[p]=h
recipe=S/'convert_gladiator_native_geometry_v396.py';tree=ast.parse(recipe.read_text('utf-8-sig'));main=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='main');loop=next(n for n in main.body if isinstance(n,ast.For) and ast.unparse(n.target)=='mesh');code=ast.unparse(loop)
old="folder = O / identity['file_id']";assert code.count(old)==1;code=code.replace(old,"folder = O / identity['guid'] / identity['file_id']").replace('folder.mkdir()','folder.mkdir(parents=True)')
for p in [Path(__file__),recipe,N/'RUN-RESULT.v1.json',N/'NATIVE-RESULT.v1.json',N/'PREPARED.v1.json']:pins[str(p)]=sha(p)
O.mkdir();outputs={};cases=[]
for case in native['cases']:
    assert case['source_components_unchanged'];folder=O/case['prefab_guid'];folder.mkdir();nodes={n['path']:dict(n) for n in case['nodes']}
    for b in case['bindings']:nodes[b['path']]['materials']=b['materials']
    context=dict(O=folder,native=case,bindings=case['bindings'],nodes=nodes,outputs=outputs,converted=[],base64=base64,json=json,math=math,struct=struct,sha=sha)
    exec(compile(code,str(recipe)+'#house-original-buffers','exec'),context)
    converted=context['converted'];assert len(converted)==len(case['meshes'])
    cases.append(dict(card_id=case['card_id'],prefab_guid=case['prefab_guid'],meshes=converted,source_nodes=case['nodes'],source_bindings=case['bindings'],unassigned_mesh_colliders=case['unassigned_mesh_colliders'],source_component_snapshots=case['component_snapshots']))
for p,h in pins.items():assert sha(p)==h,p
result=dict(status='house_twentyfour_prefab_geometry_converted_reversibly',utc=datetime.now(timezone.utc).isoformat(),lot='L03.HOUSE.MAIN.01',prefab_cards=24,meshes=42,render_mesh_bindings=sum(b['role']=='mesh_filter' for c in cases for b in c['source_bindings']),collider_mesh_bindings=sum(b['role']=='mesh_collider' for c in cases for b in c['source_bindings']),unassigned_mesh_colliders=sum(len(c['unassigned_mesh_colliders']) for c in cases),vertex_count=sum(m['vertex_count'] for c in cases for m in c['meshes']),triangles=sum(p['triangles'] for c in cases for m in c['meshes'] for p in m['primitives']),basis=dict(unity_to_target_diagonal=[1,1,-1],normal_z_negated=True,tangent_z_and_handedness_negated=True,winding_reversed=True,uv_channels_unchanged=True),all_vertex_bytes_reopened_and_reverse_compared=True,all_indices_reopened_and_reverse_compared=True,source_inputs_unchanged=True,cases=cases,input_pins=pins,output_pins=outputs,autonomous_glb_written=False,forge_render_verified=False,studio_verified=False,full_import=False,complete_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2,allow_nan=False),'utf-8');print(json.dumps({k:result[k] for k in ['status','prefab_cards','meshes','render_mesh_bindings','collider_mesh_bindings','unassigned_mesh_colliders','vertex_count','triangles']}))
