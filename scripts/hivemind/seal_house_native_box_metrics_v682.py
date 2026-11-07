"""Pin native Unity box query controls to the preserved House collider identities."""
from pathlib import Path
import datetime
import hashlib
import json
import math

W=Path('F:/OpenRPG_ImportWork');O=W/'house-native-box-checkpoint-v682'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
run_path=W/'house-native-component-state-v681/RUN-RESULT.v1.json'
native_path=run_path.with_name('NATIVE-RESULT.v1.json')
plan_path=W/'house-collider-binding-plan-v678/RESULT.v1.json'
run,native,plan=map(read,[run_path,native_path,plan_path])
assert run['unity_exit_code']==0 and not run['source_changes'] and not run['private_prefab_changes']
assert sha(native_path)==run['native_sha256']
assert native['original_components_unchanged'] and not native['purchased_scripts_executed'] and not native['physical_simulation_stepped']
pins={str(p):sha(p) for p in [run_path,native_path,plan_path,Path(__file__)]}
for p,h in run['input_pins'].items():assert sha(p)==h,p;pins[p]=h
boxes={(r['source_component']['guid'],r['source_component']['file_id']):r for r in plan['rows'] if r['kind']=='box'}
assert len(native['boxes'])==len(boxes)==3
for b in native['boxes']:
    row=boxes[(b['source_guid'],b['source_file_id'])]
    assert b['node_path']==row['node_path']
    props={p['property_path']:p for p in row['source_serialized_properties']}
    assert b['local_center']==props['m_Center']['vector'] and b['local_size']==props['m_Size']['vector']
    assert b['enabled']==row['source_flags']['m_Enabled'] and b['is_trigger']==row['source_flags']['m_IsTrigger']
    assert len(b['points'])==21 and len(b['rays'])==6
    assert b['world_position']==[0,0,0] and b['world_rotation']==[0,0,0,1] and b['lossy_scale']==[1,1,1]
    for point in b['points']:
        assert point['axis'] in [0,1,2]
        for field in ['local_query','world_query','closest_world']:
            assert len(point[field])==3 and all(math.isfinite(v) for v in point[field])
    for ray in b['rays']:
        assert ray['hit'] and ray['distance']>=0 and math.isfinite(ray['distance'])
        for field in ['origin','direction','point','normal']:
            assert len(ray[field])==3 and all(math.isfinite(v) for v in ray[field])
    assert not b['disabled_raycast_hit'] and b['disabled_query']==b['disabled_closest']
    m=b['implicit_material'];assert m['original_reference_null']
    assert all(math.isfinite(m[field]) for field in ['dynamic_friction','static_friction','bounciness'])
assert all(sha(p)==h for p,h in pins.items())
O.mkdir();result=dict(schema='hivemind.house-native-box-checkpoint.v682',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),boxes=3,point_queries=63,ray_queries=18,disabled_controls=3,rows=native['boxes'],input_pins=pins,unity_native_query_controls_verified=True,rapier_query_parity=False,physics_world_simulation_qualified=False,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## House — références physiques Unity des boîtes vérifiées (v682)

- Trois BoxColliders source : 63 requêtes de point le plus proche, 18 rayons et 3 contrôles désactivés capturés sur clones temporaires puis réouverts contre les identités/propriétés source.
- Matériau implicite, contact offset et paramètres physiques Unity conservés. Sources et prefabs privés inchangés ; aucune simulation physique exécutée.
- Preuve : F:/OpenRPG_ImportWork/house-native-box-checkpoint-v682/RESULT.v1.json.
- Prochaine étape : comparaison aux requêtes de l'adaptateur Rapier commun ; les 19 meshes de collision demandent aussi leur admission. Essai GPU v679/v680 toujours à contrôler, puis Studio. Incrément complet : 0.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps(dict(boxes=3,point_queries=63,ray_queries=18,disabled_controls=3)))
