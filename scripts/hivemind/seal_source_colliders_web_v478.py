"""Seal the actual Studio compile without claiming runtime admission."""
from pathlib import Path
import hashlib, json
from datetime import datetime, timezone

W = Path('F:/OpenRPG_ImportWork')
O = W / 'source-colliders-web-delivery-v478'
assert not O.exists()

def sha(p):
    with Path(p).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

parent = W / 'source-colliders-studio-web-check-v476/RUN-RESULT.v1.json'
checked = json.loads(parent.read_text('utf-8-sig'))
assert checked['status'] == 'actual_studio_webgpu_lod_check_passed'
assert checked['cargo_exit_code'] == 0 and checked['inputs_unchanged']
for path, expected in checked['input_pins'].items():
    assert sha(path) == expected, path
log = parent.parent / 'cargo.log'
assert sha(log) == checked['log_sha256']
assert 'Finished' in log.read_text('utf-8', errors='strict')
O.mkdir()
result = dict(
    status='actual_studio_webgpu_source_box_adapter_compile_sealed',
    checked_utc=datetime.now(timezone.utc).isoformat(),
    parent=str(parent), parent_sha256=sha(parent),
    log=str(log), log_sha256=sha(log), command=checked['command'],
    input_pins=checked['input_pins'], actual_exit_code=0,
    studio_webgpu_compile=True, native_rapier_comparison_qualified=False,
    physics_world_integration_qualified=False,
    material_contact_simulation_qualified=False,
    gpu_render_qualified=False, studio_gui_verified=False,
    vault_mutated=False, complete_asset_conversion_increment=0,
    full_import=False,
)
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
note = '''## Collisions source — compilation Studio WebGPU vérifiée, 10 octobre 2026

Le cargo check réel de forge_studio, cible wasm32-unknown-unknown et feature web, termine avec code 0. Le nouveau module commun source_colliders et son export sont épinglés ; tous les hashes sont réouverts et concordent. Cette preuve qualifie la compilation, pas les contacts physiques, les requêtes Rapier, le rendu GPU ou la fenêtre de bibliothèque.

Preuve : F:\\OpenRPG_ImportWork\\source-colliders-web-delivery-v478\\RESULT.v1.json
La comparaison native Rapier reste suivie dans gladiator-rapier-box-query-v475/RUN-RESULT.v1.json ; reprendre son exécution existante, sans la redémarrer sur timeout. Aucun fichier du Vault modifié et aucun nouvel asset complet qualifié.

'''
for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
    dashboard = W / 'SUIVI_LOTS_HIVEMIND' / name
    old = dashboard.read_bytes()
    (O / (name + '.before')).write_bytes(old)
    dashboard.write_bytes(note.encode('utf-8') + old)
print(json.dumps(dict(status=result['status'], proof=str(O / 'RESULT.v1.json'))))
