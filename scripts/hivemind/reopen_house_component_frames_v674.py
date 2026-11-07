"""Reopen native House identities/state and derive target matrices from captured Unity values."""
from pathlib import Path
import datetime
import hashlib
import json
import math

W=Path('F:/OpenRPG_ImportWork'); O=W/'house-native-component-frame-checkpoint-v674'
assert not O.exists()
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as stream: return hashlib.file_digest(stream,'sha256').hexdigest()
run_path=W/'house-native-component-state-v673/RUN-RESULT.v1.json'
native_path=run_path.with_name('NATIVE-RESULT.v1.json')
scene_path=W/'house-scene-geometry-package-v667/RESULT.v1.json'
run,native,scenes=[read(p) for p in [run_path,native_path,scene_path]]
assert run['unity_exit_code']==0 and not run['source_changes'] and not run['private_prefab_changes']
assert sha(native_path)==run['native_sha256']
pins={str(p):sha(p) for p in [run_path,native_path,scene_path,Path(__file__).resolve()]}
for p,h in run['input_pins'].items():
    assert sha(p)==h,p
    pins[p]=h
by_guid={r['prefab_guid']:r for r in scenes['rows']}
signs=[1,1,-1,1]
rows=[]; renderers=colliders=nodes=groups=0; max_math_difference=0
for prefab in native['prefabs']:
    guid=prefab['identity']['guid']; scene=by_guid[guid]
    expected={n['source_node']['path']:n for n in scene['nodes']}
    assert {n['node_path'] for n in prefab['nodes']}==set(expected)
    mapped=[]
    for n in prefab['nodes']:
        path=n['node_path']; s=expected[path]
        assert n['missing_script_components']==0
        assert n['component_types']==s['source_node']['component_types']
        identity=n['gameobject'];assert identity['guid']==guid and identity['file_id'] and identity['type']=='UnityEngine.GameObject'
        assert type(n['active_self']) is bool
        matrices={}
        for lane in ['local','world']:
            original=n[lane+'_matrix']; assert len(original)==16 and all(math.isfinite(v) for v in original)
            target=[original[c*4+r]*signs[r]*signs[c] for c in range(4) for r in range(4)]
            assert [target[c*4+r]*signs[r]*signs[c] for c in range(4) for r in range(4)]==original
            assert target[3]==target[7]==target[11]==0 and target[15]==1
            a,b,c=target[0:3],target[4:7],target[8:11]
            determinant=a[0]*(b[1]*c[2]-b[2]*c[1])-b[0]*(a[1]*c[2]-a[2]*c[1])+c[0]*(a[1]*b[2]-a[2]*b[1])
            assert determinant!=0 and math.isfinite(determinant)
            matrices['target_'+lane+'_matrix_column_major']=target
            old=s['target_'+lane+'_matrix_column_major']
            max_math_difference=max(max_math_difference,max(abs(x-y) for x,y in zip(target,old)))
        assert len(n['renderers'])==(1 if 'geometry' in s else 0)
        for r in n['renderers']:
            assert r['identity']['guid']==guid and r['identity']['file_id']
            assert r['identity']['type']=='UnityEngine.MeshRenderer'
            assert type(r['enabled']) is bool and type(r['force_rendering_off']) is bool
            assert 0<=r['shadow_casting_mode']<=3 and type(r['receive_shadows']) is bool
        for c in n['colliders']:
            assert c['identity']['guid']==guid and c['identity']['file_id']
            assert c['identity']['type'] in ['UnityEngine.MeshCollider','UnityEngine.BoxCollider']
        if n['lod_group']['identity']['guid']: groups+=1
        renderers+=len(n['renderers']);colliders+=len(n['colliders']);nodes+=1
        mapped.append(dict(native_node=n,**matrices))
    rows.append(dict(prefab_guid=guid,card_ids=scene['cards'],nodes=mapped))
assert len(rows)==24 and renderers==24 and colliders==24
O.mkdir()
result=dict(schema='hivemind.house-native-component-frame-checkpoint.v674',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),prefabs=24,nodes=nodes,renderers=renderers,colliders=colliders,lod_groups=groups,rows=rows,input_pins=pins,max_previous_float64_matrix_difference=max_math_difference,actual_native_matrix_basis_change_reversible=True,previous_computed_matrices_replaced_in_glb=False,renderer_state_applied=False,gpu_rendered=False,studio_verified=False,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2,allow_nan=False),'utf-8')
note=f'''## House — composants et matrices Unity réels vérifiés (v674)

- 24 prefabs : {nodes} identités GameObject, 24 renderers et 24 colliders capturés ; activation, états d'ombres et données de collision conservés. {groups} groupes LOD présents.
- Matrices locales et monde natives conservées et changement de repère réversible vérifié. Écart maximal avec les matrices précédemment recomposées : {max_math_difference:.9g}. Les matrices natives doivent être utilisées pour le raccordement final.
- Sources et prefabs privés inchangés. Preuve : F:/OpenRPG_ImportWork/house-native-component-frame-checkpoint-v674/RESULT.v1.json.
- Prochaine tranche : lier ces états au host commun et aux GLB, puis rendu GPU/collisions et Studio. Incrément complet : 0.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    path=W/'SUIVI_LOTS_HIVEMIND'/name; before=path.read_bytes()
    (O/(name+'.before')).write_bytes(before);path.write_bytes(note.encode('utf-8')+before)
print(json.dumps({k:result[k] for k in ['prefabs','nodes','renderers','colliders','lod_groups','max_previous_float64_matrix_difference']}))
