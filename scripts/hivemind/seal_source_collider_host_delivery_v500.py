"""Seal actual host collider calls and scheduled LOD replay with the WebGPU check."""
from pathlib import Path
import hashlib, json
from datetime import datetime, timezone

W = Path('F:/OpenRPG_ImportWork')
O = W / 'source-collider-host-delivery-v500'
assert not O.exists()

def sha(p):
    with Path(p).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

native_path = W / 'gladiator-source-collider-host-cycle-v496/RUN-RESULT.v1.json'
web_path = W / 'source-collider-host-web-delivery-v489/RESULT.v1.json'
native = json.loads(native_path.read_text('utf-8-sig'))
web = json.loads(web_path.read_text('utf-8-sig'))
assert native['status'] == 'five_actual_hosts_install_remove_colliders_and_pass_207_lod_cycles'
assert native['cargo_exit_code'] == 0 and len(native['rows']) == 5
assert sum(row['cases'] for row in native['rows']) == 207
assert web['actual_exit_code'] == 0 and web['studio_webgpu_compile']
assert sha(native_path.parent / 'viewport-cycle-checker.exe') == native['checker_sha256']
pins = native['input_pins'] | web['input_pins']
for path, expected in pins.items():
    assert sha(path) == expected, path
for row in native['rows']:
    assert row['actual_exit_code'] == 0 and row['actual_host_collision_install_remove_verified']
    assert row['material_binding_count'] == 0 and not row['gpu_rendered']
    assert not row['studio_gui_verified'] and not row['full_import']
    stdout = native_path.parent / row['prefab_guid'] / 'stdout.json'
    assert sha(stdout) == row['stdout_sha256']
    pins[str(stdout)] = row['stdout_sha256']
for path in [Path(__file__), native_path, web_path]:
    pins[str(path)] = sha(path)
O.mkdir()
result = dict(
    status='five_actual_hosts_collider_install_remove_and_207_lod_cycles_sealed',
    checked_utc=datetime.now(timezone.utc).isoformat(), input_pins=pins,
    rows=native['rows'], source_scenes=5, source_boxes=3,
    actual_host_install_remove_verified=True,
    query_fixture_scope=dict(placement_poses=3, native_point_samples_per_box=21, native_rays_per_box=6),
    scheduled_lod_cases=207, studio_webgpu_compile=True,
    collider_queries_and_lod_verified_in_separate_phases=True,
    concurrent_lod_collider_lifecycle_qualified=False,
    automatic_placement_contact_admission_qualified=False,
    material_contact_simulation_qualified=False,
    layer_interaction_mapping_qualified=False,
    material_binding_count=0, gpu_render_qualified=False,
    studio_gui_verified=False, full_import=False,
    complete_asset_conversion_increment=0, vault_mutated=False,
)
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
note = '''## Collisions Gladiator — host courant et LOD vérifiés

Cinq scènes passent par les méthodes install_colliders/remove_colliders du host Studio actuel. Les trois formes sont interrogées dans le contexte Rapier sous trois poses composées ; suppression des colliders, conservation des nœuds source et racines. Le même banc rejoue ensuite 207 cycles LOD et les refus/récupérations de caméra. Le contrôle WebGPU des mêmes sources passe. Les hashes, exécutables et sorties sont réouverts.

Preuve : F:\\OpenRPG_ImportWork\\source-collider-host-delivery-v500\\RESULT.v1.json
Banc v496 terminé, ne pas relancer. Les contrôles physiques et LOD sont exécutés en phases distinctes ; maintien simultané des colliders pendant les transitions LOD encore à tester. Admission automatique au placement, simulation des contacts, mapping des couches, rendu et parcours GUI restent ouverts. Zéro binding matériau dans ce banc, aucune qualification du rendu déduite. Aucun nouvel asset complet qualifié.

Images Pirate LOD04 : 24 captures et revue des 32 vues candidates terminées ; transaction d'indexation v498 en cours, lecteur courant v499 à exécuter après son verdict. Ne pas relancer la capture v491.

'''
for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
    dashboard = W / 'SUIVI_LOTS_HIVEMIND' / name
    old = dashboard.read_bytes()
    (O / (name + '.before')).write_bytes(old)
    dashboard.write_bytes(note.encode('utf-8') + old)
print(json.dumps(dict(status=result['status'], scenes=5, lod_cases=207)))
