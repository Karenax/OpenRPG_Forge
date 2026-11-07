"""Seal completed command checks and current source-thumbnail routing decisions."""
from pathlib import Path
import datetime, hashlib, json, re
W = Path('F:/OpenRPG_ImportWork')
O = W/'reviewed-replacement-and-frontier-seal-v816'
assert not O.exists()
def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()
pins = {str(Path(__file__)): sha(Path(__file__))}
for name in ['placement-reviewed-physics-tests-v812', 'placement-reviewed-physics-clippy-v813', 'placement-reviewed-physics-wasm-v814']:
    root = W/name
    result = json.loads((root/'RESULT.v1.json').read_text('utf-8-sig'))
    assert result['exit_code'] == 0 and not result['changed_inputs']
    for p, h in json.loads((root/'INPUT-PINS.v1.json').read_text('utf-8-sig')).items():
        assert sha(p) == h, p
        if p in pins:
            assert pins[p] == h
        pins[p] = h
    for n in ['RESULT.v1.json', 'INPUT-PINS.v1.json', 'JOB.v1.json', 'cargo.log']:
        p = root/n
        pins[str(p)] = sha(p)
log = (W/'placement-reviewed-physics-tests-v812/cargo.log').read_text('utf-8')
passed = sum(map(int, re.findall(r'test result: ok\. (\d+) passed;', log)))
assert passed == 25
frontier = W/'pirate-image-frontier-v815/RESULT.v1.json'
f = json.loads(frontier.read_text('utf-8-sig'))
assert not f['eligible_groups'] and len(f['held_groups']) == 3
pins[str(frontier)] = sha(frontier)
for p, h in f['input_pins'].items():
    assert sha(p) == h, p
    pins[p] = h
O.mkdir()
result = dict(status='reviewed_replacement_checks_sealed_material_frontier_routed', utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), targeted_tests_passed=passed, domain_and_physics_test_clippy=True, studio_wasm_compile=True, remaining_uncaptured_inline_lod_groups=3, groups_eligible_without_material_qualification=0, source_primary_images_missing=f['source_primary_images_missing'], input_pins=pins, images_installed=False, studio_visual_verified=False, persistent_physics_mount_verified=False, full_import_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
header = '## Remplacement physique : tests/Clippy/WebGPU PASS, frontier images retriée (v812-v816)\n\n- Dernières sources : 25 tests ciblés PASS ; Clippy domaine/test PASS ; Studio WebGPU PASS. Entrées rehashées sans changement, résultats/logs scellés sous reviewed-replacement-and-frontier-seal-v816. Le montage persistant et le parcours UI restent à réaliser.\n- Frontier Pirate v815 : trois groupes LOD inline non capturés restants (VolcanoSkull, WindowGlass, WoodLog_Stump), tous retenus par les refus de matériaux existants ; zéro groupe éligible à une capture sans qualification préalable. Les huit groupes déjà capturés v791 sont exclus ; aucune relance identique ni admission forcée. Ce classement ne couvre pas toutes les 570 images manquantes, ni les cas déjà capturés mais refusés.\n- Prochaine qualification native du Studio : v817. Les audits globaux v761/v778 continuent sans mutation du Vault. Autres conversions, matériaux/effets, images et preuves produit restent dans l’objectif complet actif ; incrément full-import 0.\n\n'
for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
    p = W/'SUIVI_LOTS_HIVEMIND'/name
    raw = p.read_bytes()
    (O/(name+'.before')).write_bytes(raw)
    p.write_text(header+raw.decode('utf-8-sig'), 'utf-8')
print(json.dumps({k:v for k,v in result.items() if k != 'input_pins'}))
