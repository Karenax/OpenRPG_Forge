"""Record complete box query success and convex mesh discrepancies without promoting runtime admission."""
from pathlib import Path
import datetime, hashlib, json, collections
W=Path('F:/OpenRPG_ImportWork');O=W/'house-physics-query-checkpoint-v695'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
box_path=W/'house-rapier-box-query-v693/RUN-RESULT.v1.json'
mesh_path=W/'house-rapier-mesh-query-v686/RUN-RESULT.v1.json'
box,mesh=map(read,[box_path,mesh_path])
assert box['status']=='three_common_rapier_boxes_match_63_unity_points_and_18_rays'
assert len(box['rows'])==3 and all(r['unmodified_complete_glb_document'] for r in box['rows'])
assert sum(r['points'] for r in box['rows'])==63 and sum(r['rays'] for r in box['rows'])==18
assert sum(r['disabled_controls'] for r in box['rows'])==3
assert mesh['status']=='mesh_candidate_mismatches_retained' and len(mesh['rows'])==19
assert sum(len(r['failures']) for r in mesh['rows'])==mesh['failures']==93
assert all(not r['failures'] for r in mesh['rows'] if not r['convex'])
pins={str(p):sha(p) for p in [Path(__file__),box_path,mesh_path]}
for source in [box,mesh]:
    for p,h in source['input_pins'].items():assert sha(p)==h,p;pins[p]=h
failure_kinds=dict(collections.Counter(f['kind'] for r in mesh['rows'] for f in r['failures']))
O.mkdir()
result=dict(schema='hivemind.house-physics-query-checkpoint.v695',
            utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
            complete_box_documents=3,box_points=63,box_rays=18,box_disabled_controls=3,
            concave_candidate_query_cases_without_mismatch=2,convex_candidate_cases=17,
            convex_candidate_failures=93,failure_kinds=failure_kinds,
            box_rows=box['rows'],mesh_rows=mesh['rows'],input_pins=pins,
            source_cooking_parity=False,complete_physics_world_qualified=False,
            studio_verified=False,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## House — requêtes physiques réelles et écarts convexes (v695)

- Trois prefabs à boîtes testés dans leurs documents GLB complets par l'adaptateur commun : 63 points, 18 rayons, 3 contrôles désactivés et entrées invalides vérifiés contre Unity.
- Candidat mesh Rapier : 19 cas exécutés. Les 2 meshes non convexes passent leurs rayons mesurés ; les 17 convexes produisent 27 écarts de point proche et 66 écarts de rayon, tous conservés. Aucune tolérance modifiée.
- Preuve : F:/OpenRPG_ImportWork/house-physics-query-checkpoint-v695/RESULT.v1.json.
- Les constructeurs Rapier seuls ne qualifient ni la cuisson native Unity, ni l'intégration commune des meshes, ni les contacts et la simulation. Restent ces étapes, rendu fidèle et Studio. Incrément complet : 0.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes()
    (O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps(dict(boxes_verified=3,concave_candidate_cases=2,convex_failures=93,failure_kinds=failure_kinds)))
