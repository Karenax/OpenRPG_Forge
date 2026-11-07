"""Seal completed checks, revalidate pins, and publish an honest lot update."""
from pathlib import Path
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
R = Path(__file__).resolve().parents[2]
O = W / 'placement-and-census-delivery-v890'

def sha(path):
    return hashlib.file_digest(Path(path).open('rb'), 'sha256').hexdigest()

def read(path):
    return json.loads(Path(path).read_text('utf-8'))

def main():
    O.mkdir(exist_ok=False)
    wasm = W / 'persistent-source-placement-wasm-v889'
    census = W / 'model-loading-routes-v881'
    compile_result = read(wasm / 'RESULT.v1.json')
    census_result = read(census / 'RESULT.v1.json')
    assert compile_result['exit_code'] == 0 and not compile_result['changed_inputs']
    assert census_result['status'] == 'current_glb_declaration_census_completed'
    assert not census_result['failures'] and not census_result['changed_metadata_inputs']
    pins = read(wasm / 'INPUT-PINS.v1.json')
    metadata_pins = read(census / 'INPUT-PINS.v1.json')['pins']
    changed = [p for p, expected in (pins | metadata_pins).items() if sha(p) != expected]
    assert not changed, changed
    routes, cards, paths, ids = Counter(), Counter(), set(), set()
    with (census / 'DECLARATIONS.v1.jsonl').open(encoding='utf-8') as stream:
        for line in stream:
            row = json.loads(line)
            assert row['status'] == 'declaration_read'
            assert row['path'] not in paths
            assert Path(row['path']).resolve().is_relative_to(Path('F:/OpenRPG_AssetVault').resolve())
            paths.add(row['path'])
            routes[row['route']] += 1
            for card in row['cards']:
                assert card['id'] not in ids
                ids.add(card['id'])
                cards[row['route']] += 1
    assert dict(routes) == census_result['unique_payload_counts']
    assert dict(cards) == census_result['card_counts']
    proof_paths = [wasm / name for name in ('RESULT.v1.json', 'INPUT-PINS.v1.json', 'cargo.log')]
    proof_paths += [census / name for name in ('RESULT.v1.json', 'INPUT-PINS.v1.json', 'DECLARATIONS.v1.jsonl')]
    result = dict(status='placement_compile_and_declaration_census_sealed',
                  utc=datetime.now(timezone.utc).isoformat(), changed_inputs=changed,
                  compile_input_count=len(pins), metadata_input_count=len(metadata_pins),
                  unique_payload_counts=dict(routes), card_counts=dict(cards),
                  proof_sha256={str(p): sha(p) for p in proof_paths},
                  full_import_increment=0, studio_visual_verified=False,
                  persistent_render_admission_verified=False, native_simulation_verified=False,
                  complete_vault_integrity_verified=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    note = ('## Montage persistant compile et census termine — v888 a v890\n\n'
            'Check WebGPU v889 PASS : 1128 entrees epinglees, reconfrontees sans changement. '
            'Montage source raccorde au root commun ManualPlacement ; paquet physique historique '
            'verifie par asset/modele/manifeste/paquet, ownership separe et reconstruction sur pose/contacts. '
            'Conversions monde explicitement declarees requises. Root masque : admission GPU, rendu, '
            'focus/picking, simulation et GUI non qualifies ; materiaux ordinaires mixtes restent a raccorder. '
            'Census v881 termine et observations relues : 13 673 GLB / 14 219 fiches sans declaration '
            'source graph. Cela ne prouve ni fidelite source ni admission du chargeur. '
            'Aucun nouvel asset complet ni image admis ; 562 images principales manquantes au dernier index. '
            'Mountain v875 et tests natifs v871 a suivre sans redemarrage. '
            'Preuve : F:/OpenRPG_ImportWork/placement-and-census-delivery-v890/RESULT.v1.json.\n\n')
    for name in ('AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md'):
        target = W / 'SUIVI_LOTS_HIVEMIND' / name
        before = target.read_bytes()
        (O / (name + '.before')).write_bytes(before)
        target.write_text(note + before.decode('utf-8-sig'), encoding='utf-8')
    print(json.dumps({k: result[k] for k in ('status', 'compile_input_count', 'unique_payload_counts', 'card_counts')}))

if __name__ == '__main__':
    main()
