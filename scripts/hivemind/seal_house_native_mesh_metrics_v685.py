"""Reopen mesh query controls against retained source identities and policies."""
from pathlib import Path
import datetime
import hashlib
import json
import math

W = Path('F:/OpenRPG_ImportWork')
O = W / 'house-native-mesh-checkpoint-v685'
assert not O.exists()

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

run_path = W / 'house-native-component-state-v684/RUN-RESULT.v1.json'
native_path = run_path.with_name('NATIVE-RESULT.v1.json')
plan_path = W / 'house-collider-binding-plan-v678/RESULT.v1.json'
run, native, plan = map(read, [run_path, native_path, plan_path])
assert run['unity_exit_code'] == 0
assert not run['source_changes'] and not run['private_prefab_changes']
assert sha(native_path) == run['native_sha256']
assert native['original_components_unchanged']
assert not native['purchased_scripts_executed'] and not native['physical_simulation_stepped']
pins = {str(p): sha(p) for p in [run_path, native_path, plan_path, Path(__file__)]}
for p, h in run['input_pins'].items():
    assert sha(p) == h, p
    pins[p] = h
meshes = {(r['source_component']['guid'], r['source_component']['file_id']): r
          for r in plan['rows'] if r['kind'] == 'assigned_mesh'}
assert len(native['boxes']) == len(meshes) == 19
seen = set()
hits = 0
for b in native['boxes']:
    key = (b['source_guid'], b['source_file_id'])
    assert key not in seen
    seen.add(key)
    row = meshes[key]
    assert b['node_path'] == row['node_path']
    ref = row['source_mesh_reference']
    assert (b['mesh_guid'], b['mesh_file_id']) == (ref['guid'], ref['file_id'])
    assert b['convex'] == row['source_convex']
    assert b['cooking_options'] == row['source_cooking_options']
    assert b['enabled'] == row['source_flags']['m_Enabled']
    assert b['is_trigger'] == row['source_flags']['m_IsTrigger']
    assert len(b['points']) == (21 if b['convex'] else 0)
    assert len(b['rays']) == 6
    for field in ['local_center', 'local_size', 'world_position', 'lossy_scale']:
        assert len(b[field]) == 3 and all(math.isfinite(v) for v in b[field])
    assert min(b['local_size']) > 0
    for point in b['points']:
        assert point['axis'] in [0, 1, 2]
        for field in ['local_query', 'world_query', 'closest_world']:
            assert len(point[field]) == 3 and all(math.isfinite(v) for v in point[field])
    for ray in b['rays']:
        assert ray['axis'] in [0, 1, 2] and ray['sign'] in [-1, 1]
        assert math.isfinite(ray['max_distance']) and ray['max_distance'] > 0
        for field in ['origin', 'direction']:
            assert len(ray[field]) == 3 and all(math.isfinite(v) for v in ray[field])
        if ray['hit']:
            hits += 1
            assert 0 <= ray['distance'] <= ray['max_distance']
            for field in ['point', 'normal']:
                assert len(ray[field]) == 3 and all(math.isfinite(v) for v in ray[field])
        else:
            assert ray['distance'] == 0 and not ray['point'] and not ray['normal']
    assert not b['disabled_raycast_hit']
    assert b['disabled_closest'] == (b['disabled_query'] if b['convex'] else [])
assert all(sha(p) == h for p, h in pins.items())
result = dict(schema='hivemind.house-native-mesh-checkpoint.v685',
              utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
              meshes=19, convex=17, concave=2, point_queries=357,
              ray_queries=114, ray_hits=hits, rows=native['boxes'], input_pins=pins,
              unity_native_query_controls_verified=True, rapier_query_parity=False,
              physics_world_simulation_qualified=False, full_import=False)
O.mkdir()
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
note = '''## House — références Unity des collisions mesh vérifiées (v685)

- 19 colliders mesh : 17 convexes et 2 non convexes, identités et options de cuisson comparées aux observations source.
- 357 points les plus proches pour les convexes et 114 rayons capturés puis réouverts ; les rayons manqués restent des résultats source valides. Contrôles désactivés conservés.
- Unity terminé avec code 0, sources et prefabs privés inchangés, aucune simulation exécutée.
- Preuve : F:/OpenRPG_ImportWork/house-native-mesh-checkpoint-v685/RESULT.v1.json.
- Restent comparaison Rapier, intégration physique complète, rendu comparé et Studio. Incrément complet : 0.

'''
(O / 'LOT.md').write_text(note, 'utf-8')
for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
    p = W / 'SUIVI_LOTS_HIVEMIND' / name
    before = p.read_bytes()
    (O / (name + '.before')).write_bytes(before)
    p.write_bytes(note.encode('utf-8') + before)
print(json.dumps({k: result[k] for k in ['meshes', 'convex', 'concave', 'point_queries', 'ray_queries', 'ray_hits']}))
