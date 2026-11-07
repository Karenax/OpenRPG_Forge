"""Separate measured geometry and query residuals; no tolerance adjustment or runtime promotion."""
from pathlib import Path
import collections,datetime,hashlib,json
import numpy as np
W=Path('F:/OpenRPG_ImportWork');O=W/'house-convex-query-residual-v707'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
run_path=W/'house-rapier-native-plane-query-v706/RUN-RESULT.v1.json'
planes_path=W/'house-native-convex-candidate-v704/RESULT.v1.json'
run,planes=map(read,[run_path,planes_path])
assert run['status']=='mesh_candidate_mismatches_retained' and len(run['rows'])==19
pins={str(p):sha(p) for p in [Path(__file__),run_path,planes_path]}
for p,h in run['input_pins'].items():assert sha(p)==h,p;pins[p]=h
by_guid={r['source_guid']:r for r in planes['rows']}
rows=[];distances=[];positions=[];normals=[];outside=[]
for row in run['rows']:
    counts=collections.Counter(f['kind'] for f in row['failures'])
    old_rays=sum(f['kind'].startswith('ray_') and f['sample']<6 for f in row['failures'])
    supplemental_rays=sum(f['kind'].startswith('ray_') and f['sample']>=6 for f in row['failures'])
    violations=[]
    for failure in row['failures']:
        if failure['kind']=='ray_values':
            distances.append(abs(failure['actual_distance']-failure['expected_distance']))
            positions.append(float(np.max(np.abs(np.array(failure['actual_point'])-failure['expected_point']))))
            normals.append(float(np.max(np.abs(np.array(failure['actual_normal'])-failure['expected_normal']))))
        elif failure['kind']=='closest_point':
            p=by_guid[row['guid']];n=np.array(p['source_plane_normals']);n[:,2]*=-1
            violation=float(np.max(n@np.array(failure['expected'])-np.array(p['source_plane_distances'])))
            violations.append(violation)
            if violation>max(p['maximum_offset_spread'],1e-6):outside.append(dict(guid=row['guid'],sample=failure['sample'],expected_point_outside_observed_planes=violation))
    rows.append(dict(guid=row['guid'],convex=row['convex'],failure_kinds=dict(counts),
                     original_ray_failures=old_rays,independent_ray_failures=supplemental_rays,
                     native_closest_point_expected_plane_violations=violations))
def metrics(values):
    return dict(count=len(values),maximum=float(np.max(values)),percentiles={str(p):float(np.percentile(values,p)) for p in [50,90,99]}) if values else dict(count=0)
result=dict(schema='hivemind.house-convex-query-residual.v707',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
            status='native_convex_query_residuals_measured_not_qualified',rows=rows,
            closest_point_failures=sum(r['failure_kinds'].get('closest_point',0) for r in rows),
            original_ray_failures=sum(r['original_ray_failures'] for r in rows),
            independent_ray_failures=sum(r['independent_ray_failures'] for r in rows),
            ray_distance_error=metrics(distances),ray_position_error=metrics(positions),ray_normal_error=metrics(normals),
            source_closest_point_outputs_outside_observed_planes=outside,input_pins=pins,
            tolerance_changed=False,global_shape_equivalence_proven=False,physics_world_qualified=False,full_import=False)
assert all(sha(p)==h for p,h in pins.items())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note=f'''## House — surfaces natives acquises et résidu convexe mesuré (v707)

- 17 candidats issus des plans natifs Unity ; acquisition dense séparée de 17 408 rayons de vérification indépendants. Les sources et composants restent inchangés.
- Vérification Rapier de 19 cas : 357 points et 17 522 rayons exécutés. Résidu : {result['closest_point_failures']} points, {result['original_ray_failures']} rayons originaux, {result['independent_ray_failures']} rayons indépendants hors tolérance inchangée.
- Distances, positions et normales analysées séparément. {len(outside)} résultats Unity de point proche dépassent aussi les plans natifs observés au-delà de leur dispersion mesurée ; la précision des requêtes doit être distinguée de la forme de collision. Aucune équivalence globale n'est déduite.
- Preuve : F:/OpenRPG_ImportWork/house-convex-query-residual-v707/RESULT.v1.json. Les contrôles d'intégrité et les refus v702/v705 restent conservés.
- Restent adaptateur commun des meshes, qualification de pose et collision dans le runtime, rendu et bibliothèque Studio. Incrément complet : 0.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes()
    (O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps({k:result[k] for k in ['closest_point_failures','original_ray_failures','independent_ray_failures','ray_distance_error','ray_normal_error']}))
