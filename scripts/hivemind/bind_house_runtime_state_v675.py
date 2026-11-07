"""Bind measured House activation/render state and native matrices without changing BIN chunks."""
from pathlib import Path
import copy
import datetime
import hashlib
import json
import struct
import bind_native_scene_render_state_v1 as binding

W=Path('F:/OpenRPG_ImportWork'); O=W/'house-runtime-state-glb-v675'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assembly_path=W/'house-autonomous-glb-v669/RESULT.v1.json'
frame_path=W/'house-native-component-frame-checkpoint-v674/RESULT.v1.json'
native_path=W/'house-native-component-state-v673/NATIVE-RESULT.v1.json'
assembly,frames,native=map(read,[assembly_path,frame_path,native_path])
assert frames['prefabs']==24 and frames['renderers']==24 and frames['lod_groups']==0
pins=dict(frames['input_pins'])
for p in [assembly_path,frame_path,native_path,Path(__file__),Path(binding.__file__)]:pins[str(p)]=sha(p)
assert all(sha(p)==h for p,h in pins.items())
by_guid={r['prefab_guid']:r for r in frames['rows']}
O.mkdir();rows=[]
for row in assembly['rows']:
    source=Path(row['glb']); assert sha(source)==row['sha256'];pins[str(source)]=row['sha256']
    doc,offset,header=binding.load_json(source); original=copy.deepcopy(doc)
    measured={n['native_node']['node_path']:n for n in by_guid[row['prefab_guid']]['nodes']}
    assert {n['extras']['unity_source_node']['path'] for n in doc['nodes']}==set(measured)
    assert doc['extras']['original_lod_groups']==[]
    for node in doc['nodes']:
        s=node['extras']['unity_source_node'];path=s['path'];captured=measured[path];a=captured['native_node']
        node['extras']['original_unity_source_node_before_host_normalization']=copy.deepcopy(s)
        node['extras']['previous_computed_local_matrix']=node['matrix']
        node['matrix']=captured['target_local_matrix_column_major']
        node['extras']['target_world_matrix']=captured['target_world_matrix_column_major']
        s['parent_path']=None if path=='root' else path.rsplit('/',1)[0]
        s['gameobject']=dict(**a['gameobject'],exact_asset_identity_available=True)
        s['active_self']=a['active_self']
        chain=[path];p=path
        while p!='root':
            p=p.rsplit('/',1)[0];chain.append(p)
        s['active_source_parent_chain']=all(measured[p]['native_node']['active_self'] for p in chain)
        s['prefab_asset_active_in_hierarchy_observed']=a['active_in_hierarchy']
        s['renderers']=[]
        for r in a['renderers']:
            assert 'mesh' in node
            s['renderers'].append(dict(type=r['identity']['type'],component=dict(**r['identity'],exact_asset_identity_available=True),enabled=r['enabled'],shadow_casting_mode=r['shadow_casting_mode'],receive_shadows=r['receive_shadows']))
        s['native_collider_observations']=a['colliders']
        s['collider_runtime_admission_pending']=True
    pre_bind=copy.deepcopy(doc)
    counts=binding.bind(doc,native)
    assert counts['renderers']==1 and counts['lod_groups']==0
    negatives=[]
    for label in ['renderer_identity','active_self']:
        bad=copy.deepcopy(pre_bind)
        if label=='renderer_identity':
            next(n for n in bad['nodes'] if n['extras']['unity_source_node']['renderers'])['extras']['unity_source_node']['renderers'][0]['component']['guid']='0'*32
        else:bad['nodes'][0]['extras']['unity_source_node']['active_self']=not bad['nodes'][0]['extras']['unity_source_node']['active_self']
        try:binding.bind(bad,native)
        except (AssertionError,StopIteration):negatives.append(label)
        else:raise AssertionError('Changed original component accepted')
    for field in ['materials','meshes','accessors','bufferViews','buffers']:
        assert doc[field]==original[field]
    encoded=json.dumps(doc,separators=(',',':'),ensure_ascii=False,allow_nan=False).encode('utf-8');encoded+=b' '*(-len(encoded)%4)
    assert len(encoded)<=16*1024**2
    target=O/(row['prefab_guid']+'.native-state.glb')
    tail_hash=hashlib.sha256()
    with source.open('rb') as src,target.open('xb') as dst:
        src.seek(offset);dst.write(header[:8]+struct.pack('<III',20+len(encoded)+source.stat().st_size-offset,len(encoded),0x4e4f534a)+encoded)
        while block:=src.read(4*1024**2):tail_hash.update(block);dst.write(block)
    reopened,new_offset,_=binding.load_json(target);assert reopened==doc
    check_hash=hashlib.sha256()
    with target.open('rb') as f:
        f.seek(new_offset)
        while block:=f.read(4*1024**2):check_hash.update(block)
    assert check_hash.digest()==tail_hash.digest()
    rows.append(dict(prefab_guid=row['prefab_guid'],cards=row['cards'],glb=str(target),sha256=sha(target),source_glb=str(source),source_sha256=row['sha256'],unchanged_binary_chunk_sha256=tail_hash.hexdigest(),counts=counts,negative_controls_refused=negatives,actual_host_state_plan_accepted=False,gpu_rendered=False,studio_verified=False,full_import=False))
assert len(rows)==24 and all(sha(p)==h for p,h in pins.items())
result=dict(schema='hivemind.house-runtime-state-glb.v675',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),rows=rows,input_pins=pins,glbs=24,renderers=24,native_matrices_bound=True,all_binary_chunks_unchanged=True,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(glbs=24,renderers=24,binary_chunks_unchanged=True)))
