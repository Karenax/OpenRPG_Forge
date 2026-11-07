"""Reopen and seal the actual common projection/context replay."""
from pathlib import Path
import hashlib, json
from datetime import datetime, timezone

W = Path('F:/OpenRPG_ImportWork')
O = W / 'source-collider-projection-delivery-v484'
assert not O.exists()
parent = W / 'source-collider-projection-query-v483/RUN-RESULT.v1.json'

def sha(p):
    with Path(p).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

run = json.loads(parent.read_text('utf-8-sig'))
assert run['status'] == 'five_actual_source_projection_contexts_verified'
assert run['cargo_exit_code'] == 0
for path, expected in run['input_pins'].items():
    assert sha(path) == expected, path
assert sha(parent.parent / 'rapier-box-checker.exe') == run['checker_sha256']
rows = run['rows']
assert len(rows) == 5 and len({r['prefab_guid'] for r in rows}) == 5
for row in rows:
    assert row['actual_exit_code'] == 0
    assert row['composed_context_queries_verified']
    assert row['projection_ownership_and_removal_verified']
    assert not row['full_import'] and not row['studio_gui_verified']
    stdout = parent.parent / row['prefab_guid'] / 'stdout.json'
    assert sha(stdout) == row['stdout_sha256']
counts = {key: sum(r[key] for r in rows) for key in ['boxes', 'points', 'rays', 'disabled_controls', 'context_points', 'context_rays']}
assert counts == dict(boxes=3, points=63, rays=18, disabled_controls=3, context_points=189, context_rays=54)
O.mkdir()
result = dict(
    status='five_actual_common_collider_projection_contexts_sealed',
    checked_utc=datetime.now(timezone.utc).isoformat(),
    parent=str(parent), parent_sha256=sha(parent), input_pins=run['input_pins'],
    checker_sha256=run['checker_sha256'], rows=rows, counts=counts,
    placement_poses=['identity', 'translation_and_y_rotation', 'translation_y_rotation_and_positive_nonuniform_scale'],
    native_reference_query_tolerance='1e-6 * max(1, abs(expected))',
    world_point_query_max_distance=1000,
    actual_common_projection_native_context_queries=True,
    projection_removal_preserves_placement=True,
    studio_host_connected=False,
    material_contact_simulation_qualified=False,
    source_layer_interaction_mapping_qualified=False,
    gpu_render_qualified=False, studio_gui_verified=False,
    vault_mutated=False, complete_asset_conversion_increment=0, full_import=False,
)
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
note = '''## Collisions Gladiator — contexte Rapier commun vérifié

Cinq scènes passent avec l'adaptateur source_collider_projection courant et le backend Rapier du produit. Trois formes : 63 points et 18 rayons locaux conformes aux références Unity ; puis 189 projections de points et 54 rayons dans le contexte physique réel, sous identité, translation/rotation et échelle positive non uniforme. Identités et observations originales conservées. Refus d'un lot incomplet, de nœuds absents et d'une seconde installation sans mutation partielle ; suppression des colliders et purge du backend en conservant la racine de placement.

Preuve réouverte : F:\\OpenRPG_ImportWork\\source-collider-projection-delivery-v484\\RESULT.v1.json
Le banc v483 est terminé, ne pas le relancer. Les refus précédents sont conservés. L'adaptateur est testé via ses sources réelles mais reste à exporter dans le moteur et raccorder au host Studio ; cela exige un nouveau contrôle WebGPU. Friction explicitement fournie dans le banc, contact skin nul pour les requêtes : aucune équivalence des contacts Unity/Rapier revendiquée. Simulation, mapping des couches, rendu et parcours Studio restent ouverts. Aucun nouvel asset complet qualifié ; aucun fichier du Vault modifié.

'''
for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
    dashboard = W / 'SUIVI_LOTS_HIVEMIND' / name
    old = dashboard.read_bytes()
    (O / (name + '.before')).write_bytes(old)
    dashboard.write_bytes(note.encode('utf-8') + old)
print(json.dumps(dict(status=result['status'], counts=counts, proof=str(O / 'RESULT.v1.json'))))
