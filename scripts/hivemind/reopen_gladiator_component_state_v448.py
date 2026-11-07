"""Compare captured prefab state to original YAML, preserving exact float bits."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import re
import struct
import sys
sys.path.insert(0,'F:/OpenRPG_AssetVault/cache/skill-dev-python')
import yaml

W=Path('F:/OpenRPG_ImportWork')
I=W/'gladiator-native-component-state-v447'
O=W/'gladiator-native-component-reopen-v448'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def samefloat(a,b):assert struct.pack('<f',a)==struct.pack('<f',b),(a,b)
def vector(a,b):
    assert len(a)==len(b)
    for x,y in zip(a,b):samefloat(x,y)
run=read(I/'RUN-RESULT.v1.json')
assert run['status']=='five_prefab_renderer_lod_and_collider_states_captured_review_pending'
assert run['unity_exit_code']==0 and not run['source_changes'] and not run['private_prefab_changes']
native=read(I/'NATIVE-RESULT.v1.json')
assert sha(I/'NATIVE-RESULT.v1.json')==run['native_sha256']
bridge_path=W/'gladiator-source-binding-bridge-v390/RESULT.v1.json'
bridge=read(bridge_path)
by_guid={r['prefab_guid']:r for r in bridge['rows']}
pins=dict(run['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
for p in [Path(__file__),I/'RUN-RESULT.v1.json',I/'NATIVE-RESULT.v1.json',I/'PREPARED.v1.json',I/'unity.log',bridge_path]:pins[str(p)]=sha(p)
rows=[]
for prefab in native['prefabs']:
    guid=prefab['identity']['guid']
    scene=by_guid[guid]
    source=Path(scene['source_path'])
    assert sha(source)==scene['source_sha256']
    parts=re.split(r'^--- !u!(\d+) &(-?\d+)\s*\n',source.read_text('utf-8-sig'),flags=re.M)
    objects={str(parts[i+1]):next(iter(yaml.safe_load(parts[i+2]).values())) for i in range(1,len(parts),3)}
    expected={n['path']:n for n in scene['nodes']}
    assert {n['node_path'] for n in prefab['nodes']}==set(expected)
    groups={g['source_file_id']:g for g in scene['lod_groups']}
    renderers=colliders=lods=0
    for node in prefab['nodes']:
        old=expected[node['node_path']]
        assert node['active_self']==bool(old['active'])
        path=node['node_path'];chain=[]
        while True:
            chain.append(path)
            if path=='root':break
            path=path.rsplit('/',1)[0]
        assert node['active_in_hierarchy']==all(bool(expected[p]['active']) for p in chain)
        assert node['missing_script_components']==0 and not node['lights']
        assert all(not t.endswith('MonoBehaviour') for t in node['component_types'])
        assert len(node['renderers'])==(1 if old['renderer'] else 0)
        for r in node['renderers']:
            e=old['renderer']
            assert r['identity']['guid']==guid and r['identity']['file_id']==e['source_renderer_file_id']
            assert r['identity']['type']=='UnityEngine.MeshRenderer'
            assert r['enabled']==bool(e['enabled']) and r['shadow_casting_mode']==e['cast_shadows']
            assert r['receive_shadows']==bool(e['receive_shadows'])
            assert type(r['force_rendering_off']) is bool
            renderers+=1
        assert len(node['colliders'])==len(old['colliders'])
        for c,e in zip(node['colliders'],old['colliders']):
            assert c['identity']['guid']==guid and c['identity']['file_id']==e['source_file_id']
            assert c['identity']['type']=='UnityEngine.BoxCollider'
            props={p['property_path']:p for p in c['serialized']}
            original=e['source']
            for field in ['m_Enabled','m_IsTrigger']:
                assert props[field]['boolean']==bool(original[field])
            for field in ['m_Center','m_Size']:
                vector(props[field]['vector'],[original[field][x] for x in 'xyz'])
            assert props['m_GameObject']['reference']['file_id']==str(old['game_object_file_id'])
            colliders+=1
        group=node.get('lod_group')
        if group:
            assert group['identity']['guid']==guid
            e=groups[group['identity']['file_id']]
            authored=objects[e['source_file_id']]
            assert group['enabled']==bool(authored['m_Enabled'])
            assert group['animate_cross_fading']==bool(authored['m_AnimateCrossFading'])
            assert group['fade_mode']==e['fade_mode']
            samefloat(group['size'],e['size'])
            vector(group['local_reference_point'],[e['reference_point'][x] for x in 'xyz'])
            assert len(group['levels'])==len(e['levels'])
            for actual,prior in zip(group['levels'],e['levels']):
                samefloat(actual['screen_relative_height'],prior['screen_relative_height'])
                samefloat(actual['fade_transition_width'],prior['fade_width'])
                assert [(r['guid'],r['file_id']) for r in actual['renderers']]==[(guid,r['source_renderer_file_id']) for r in prior['renderers']]
            lods+=1
    rows.append(dict(prefab_guid=guid,source_prefab=str(source),source_prefab_sha256=sha(source),
        nodes=len(prefab['nodes']),renderers=renderers,box_colliders=colliders,lod_groups=lods,
        source_float_bits_match=True,native_component_state=prefab))
assert len(rows)==5 and sum(r['renderers'] for r in rows)==17
assert sum(r['box_colliders'] for r in rows)==sum(r['lod_groups'] for r in rows)==5
O.mkdir()
result=dict(status='five_original_prefab_component_states_independently_reopened',
    observed_utc=datetime.now(timezone.utc).isoformat(),rows=rows,input_pins=pins,
    prefabs=5,renderers=17,box_colliders=5,lod_groups=5,
    source_and_private_prefabs_unchanged=True,source_component_snapshots_unchanged=True,
    camera_lod_decisions_measured=False,runtime_scene_admission_qualified=False,
    material_gpu_render_qualified=False,studio_verified=False,full_import=False,
    complete_asset_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — états natifs de composants réouverts, 10 octobre 2026

Unity : 5 prefabs, 17 renderers, 5 groupes LOD et 5 BoxColliders mesurés sans instanciation. Identités, visibilité, ombres et états actifs comparés aux sources. Seuils LOD, points de référence, dimensions et centres des colliders égaux au niveau des bits float32. États forceRenderingOff, activation des LOD et fondu capturés explicitement. Prefabs source et copies privées inchangés.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-native-component-reopen-v448\\RESULT.v1.json
Restent raccordement au contrat de scène du moteur, décisions LOD runtime, rendus comparés et Studio. Aucun nouvel asset complet qualifié.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes()
    (O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps({k:result[k] for k in ['status','prefabs','renderers','box_colliders','lod_groups']}))
