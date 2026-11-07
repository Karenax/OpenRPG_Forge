"""Package measured source states for the existing common Studio host."""
from pathlib import Path
from datetime import datetime, timezone
import copy
import hashlib
import json
import struct
import bind_native_scene_render_state_v1 as binding

W=Path('F:/OpenRPG_ImportWork')
O=W/'gladiator-runtime-state-glb-v452'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def identity(guid,file_id,kind):
    return dict(guid=guid,file_id=str(file_id),type=kind,exact_asset_identity_available=True)
review_path=W/'gladiator-native-component-reopen-v451/RESULT.v1.json'
native_path=W/'gladiator-native-component-state-v447/NATIVE-RESULT.v1.json'
assembly_path=W/'gladiator-autonomous-glb-v442/RESULT.v1.json'
review,native,assembly=map(read,[review_path,native_path,assembly_path])
assert review['prefabs']==5 and review['renderers']==17 and review['box_colliders']==3
pins=dict(review['input_pins'])
for p in [Path(__file__),Path(binding.__file__),review_path,native_path,assembly_path]:pins[str(p)]=sha(p)
for p,h in pins.items():assert sha(p)==h,p
native_prefabs={p['identity']['guid']:p for p in native['prefabs']}
O.mkdir();rows=[]
for row in assembly['rows']:
    source=Path(row['glb']);assert sha(source)==row['sha256'];pins[str(source)]=row['sha256']
    doc,tail_offset,header=binding.load_json(source)
    original=copy.deepcopy(doc)
    guid=row['prefab_guid'];captured=native_prefabs[guid]
    actual={n['node_path']:n for n in captured['nodes']}
    source_nodes={n['extras']['unity_source_node']['path']:n['extras']['unity_source_node'] for n in doc['nodes']}
    assert set(source_nodes)==set(actual)
    for node in doc['nodes']:
        s=node['extras']['unity_source_node'];path=s['path'];a=actual[path]
        node['extras']['original_unity_source_node_before_host_normalization']=copy.deepcopy(s)
        s['parent_path']=None if path=='root' else path.rsplit('/',1)[0]
        s['gameobject']=identity(guid,s['game_object_file_id'],'UnityEngine.GameObject')
        s['active_self']=bool(s['active'])
        assert s['active_self']==a['active_self']
        ancestors=[];p=path
        while True:
            ancestors.append(p)
            if p=='root':break
            p=p.rsplit('/',1)[0]
        s['active_source_parent_chain']=all(bool(source_nodes[p]['active']) for p in ancestors)
        s['prefab_asset_active_in_hierarchy_observed']=a['active_in_hierarchy']
        assert a['active_in_hierarchy'] is False
        s['renderers']=[]
        old=s['renderer']
        if old:
            assert len(a['renderers'])==1
            r=a['renderers'][0]
            assert r['identity']['guid']==guid and r['identity']['file_id']==old['source_renderer_file_id']
            s['renderers'].append(dict(type='UnityEngine.MeshRenderer',
                component=identity(guid,old['source_renderer_file_id'],'UnityEngine.MeshRenderer'),
                enabled=bool(old['enabled']),shadow_casting_mode=old['cast_shadows'],receive_shadows=bool(old['receive_shadows']),
                original_mesh_identity=identity(old['mesh_guid'],old['mesh_file_id'],'UnityEngine.Mesh'),
                original_material_slots=old['material_slots']))
        else:assert not a['renderers']
        s['native_collider_observations']=a['colliders']
        s['collider_runtime_admission_pending']=True
    for group in doc['extras']['original_lod_groups']:
        measured=next(n['lod_group'] for n in captured['nodes'] if n['lod_group']['identity']['file_id']==group['source_file_id'])
        path=next(n['node_path'] for n in captured['nodes'] if n['lod_group']['identity']['file_id']==group['source_file_id'])
        group['node_path']=path
        group['local_reference_point']=[group['reference_point'][x] for x in 'xyz']
        group['animate_cross_fading']=measured['animate_cross_fading']
        for level in group['levels']:
            level['fade_transition_width']=level['fade_width']
            level['renderer_node_paths']=[r['path'] for r in level['renderers']]
            level['renderer_components']=[identity(guid,r['source_renderer_file_id'],'UnityEngine.MeshRenderer') for r in level['renderers']]
    counts=binding.bind(doc,native)
    assert counts['renderers']==row['meshes'] and counts['lod_groups']==1
    # Only JSON metadata changes. No authored renderer/LOD fields are replaced.
    for old,new in zip(original['nodes'],doc['nodes']):
        assert new['extras']['original_unity_source_node_before_host_normalization']==old['extras']['unity_source_node']
        assert new['matrix']==old['matrix'] and new.get('mesh')==old.get('mesh')
    for field in ['materials','meshes','accessors','bufferViews','buffers']:
        assert doc[field]==original[field],field
    assert doc['extras']['source_frame_binding']==original['extras']['source_frame_binding']
    negatives=[]
    # The trusted binder must reject changed original component identity,
    # activation and source LOD values rather than fill defaults.
    pre_bind=copy.deepcopy(doc)
    for n in pre_bind['nodes']:
        for r in n['extras']['unity_source_node']['renderers']:del r['force_rendering_off']
    for g in pre_bind['extras']['original_lod_groups']:
        del g['enabled'];del g['source_component_identity']
    for label in ['renderer_identity','active_self','lod_threshold']:
        bad=copy.deepcopy(pre_bind)
        if label=='renderer_identity':
            next(n for n in bad['nodes'] if n['extras']['unity_source_node']['renderers'])['extras']['unity_source_node']['renderers'][0]['component']['guid']='0'*32
        elif label=='active_self':bad['nodes'][0]['extras']['unity_source_node']['active_self']=not bad['nodes'][0]['extras']['unity_source_node']['active_self']
        else:
            level=bad['extras']['original_lod_groups'][0]['levels'][0]
            value=level['screen_relative_height']
            level['screen_relative_height']=struct.unpack('<f',struct.pack('<I',struct.unpack('<I',struct.pack('<f',value))[0]+1))[0]
        try:binding.bind(bad,native)
        except (AssertionError,StopIteration):negatives.append(label)
        else:raise AssertionError('Corrupt source binding accepted: '+label)
    encoded=json.dumps(doc,separators=(',',':'),ensure_ascii=False,allow_nan=False).encode('utf-8')
    encoded+=b' '*(-len(encoded)%4)
    assert len(encoded)<=16*1024**2
    target=O/(guid+'.native-state.glb')
    total=20+len(encoded)+source.stat().st_size-tail_offset
    old_tail=hashlib.sha256()
    with source.open('rb') as src,target.open('xb') as dst:
        src.seek(tail_offset);dst.write(header[:8]+struct.pack('<III',total,len(encoded),0x4e4f534a)+encoded)
        while block:=src.read(4*1024**2):old_tail.update(block);dst.write(block)
    reopened,new_offset,_=binding.load_json(target)
    assert reopened==doc
    new_tail=hashlib.sha256()
    with target.open('rb') as f:
        f.seek(new_offset)
        while block:=f.read(4*1024**2):new_tail.update(block)
    assert new_tail.digest()==old_tail.digest()
    rows.append(dict(prefab_guid=guid,source=str(source),source_sha256=row['sha256'],glb=str(target),sha256=sha(target),
        unchanged_binary_chunk_sha256=new_tail.hexdigest(),negative_controls_refused=negatives,counts=counts,
        box_colliders=sum(len(n['colliders']) for n in captured['nodes']),cards=row['cards'],
        actual_host_state_plan_accepted=False,collider_runtime_admission_qualified=False,
        gpu_rendered=False,studio_verified=False,full_import=False))
assert len(rows)==5 and sum(r['counts']['renderers'] for r in rows)==17
assert all(sha(p)==h for p,h in pins.items())
result=dict(status='five_native_scene_state_glbs_packaged_with_unchanged_binary_payloads',
    observed_utc=datetime.now(timezone.utc).isoformat(),input_pins=pins,rows=rows,full_import=False,complete_asset_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],glbs=5,renderers=17,lod_groups=5,colliders_preserved=3)))
