"""Seal current finite-ray regression and actual Studio WebGPU compile evidence."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json
W = Path('F:/OpenRPG_ImportWork')
O = W/'source-ray-numeric-and-web-delivery-v512'
def sha(p):
    with Path(p).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()
assert not O.exists()
np = W/'source-box-ray-numeric-range-v503/RUN-RESULT.v1.json'
wp = W/'source-ray-and-projection-studio-web-check-v505/RUN-RESULT.v1.json'
n, w = [json.loads(p.read_text('utf-8-sig')) for p in [np, wp]]
assert n['status'] == 'three_source_boxes_native_queries_and_54_extreme_ray_magnitudes_verified'
assert n['cargo_exit_code'] == 0 and len(n['rows']) == 5
assert sum(r['finite_ray_magnitude_extremes_checked'] for r in n['rows']) == 54
assert sum(r['points'] for r in n['rows']) == 63
assert sum(r['rays'] for r in n['rows']) == 18
assert all(r['actual_exit_code'] == 0 for r in n['rows'])
assert w['status'] == 'actual_studio_webgpu_lod_check_passed'
assert w['cargo_exit_code'] == 0 and w['inputs_unchanged']
pins = {}
for evidence in [n, w]:
    for p, h in evidence['input_pins'].items():
        if p in pins:
            assert pins[p] == h, p
        pins[p] = h
for p, h in pins.items():
    assert sha(p) == h, p
exe = np.parent/'rapier-box-checker.exe'
assert sha(exe) == n['checker_sha256']
for p in [np, wp, exe, Path(__file__), np.parent/'cargo.log', wp.parent/'cargo.log']:
    pins[str(p)] = sha(p)
O.mkdir()
result = dict(status='finite_float32_ray_range_and_actual_studio_webgpu_check_verified', native_boxes=3, native_points=63, native_rays=18, extreme_direction_checks=54, input_pins=pins, actual_studio_webgpu_compile=True, studio_gui_verified=False, lod_with_live_colliders_verified=False, dynamics_contact_material_equivalence_qualified=False, full_import=False, complete_conversion_increment=0, finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
header = '## Requetes collider et compilation WebGPU - v503, v505, v512\n\nTrois BoxCollider sources : 63 points, 18 rayons et 54 directions de magnitude extreme verifies. Compilation du Studio WebGPU reussie sur ces sources. Maintien physique pendant LOD en cours dans v504 ; dynamique, contacts, rendu et GUI non qualifies. Preuve : F:/OpenRPG_ImportWork/source-ray-numeric-and-web-delivery-v512/RESULT.v1.json.\n\n'
for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
    p = W/'SUIVI_LOTS_HIVEMIND'/name
    raw = p.read_bytes()
    (O/(name+'.before')).write_bytes(raw)
    p.write_text(header+raw.decode('utf-8-sig'), 'utf-8')
print(json.dumps({k:v for k,v in result.items() if k != 'input_pins'}))
