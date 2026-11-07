"""Compare independent camera depth values under each real projection."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np

W = Path('F:/OpenRPG_ImportWork')
DRAW = W / 'townsmith-paint-door-native-depth-draw-v5'
SRC = W / 'townsmith-door-source-depth-capture-v22'
OUT = W / 'townsmith-door-native-depth-comparison-v5'
assert not OUT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


run = read(DRAW / 'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0 and run['gpu_draw_executed']
receipt = read(DRAW / 'NATIVE-RESULT.v1.json')
assert receipt['native_camera_depth_buffer_captured'] and receipt['native_depth_consecutive_equal_readbacks'] >= 3
assert sha(DRAW / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
assert sha(DRAW / 'FORGE-DEPTH.r32f') == receipt['native_depth_sha256']
pins = dict(read(DRAW / 'PREPARED.v1.json')['input_pins'])
source_proof = SRC.parent / 'townsmith-door-depth-coverage-reopen-v22/RESULT.v1.json'
pins.update(read(source_proof)['input_pins'])
paths = sorted(SRC.glob('door-nopost-linear.depth-*.r32f'))
source_path = paths[-1]
metadata_path = source_path.with_suffix('.json')
context_path = Path(read(DRAW / 'INPUT.v1.json')['source_context'])
old_colour = W / 'townsmith-paint-door-native-diffuse-material-draw-v3/FORGE-LINEAR.rgba16f'
assert sha(DRAW / 'FORGE-LINEAR.rgba16f') == sha(old_colour)
for path in (source_proof, source_path, metadata_path, context_path, old_colour, DRAW / 'FORGE-DEPTH.r32f', DRAW / 'FORGE-LINEAR.rgba16f', DRAW / 'NATIVE-RESULT.v1.json', DRAW / 'RUN-RESULT.v1.json'):
    pins[str(path)] = sha(path)
for path, expected in pins.items():
    assert sha(Path(path)) == expected, path
source = np.flipud(np.fromfile(source_path, dtype='<f4').reshape(512, 512)).astype(np.float64)
native = np.fromfile(DRAW / 'FORGE-DEPTH.r32f', dtype='<f4').reshape(512, 512).astype(np.float64)
assert np.isfinite(native).all() and native.min() == 0 and native.max() < 1
mask = source > 0
assert np.array_equal(mask, native > 0) and int(mask.sum()) == 34445
projection = read(metadata_path)['projection']
source_distance = projection['e23']/(source[mask]+projection['e22'])
near = read(context_path)['near']
# Bevy PerspectiveProjection uses an infinite reversed-Z projection. The source
# camera uses finite reversed-Z. Compare camera-space distance, never raw Z.
native_distance = near/native[mask]
delta = np.abs(source_distance-native_distance)
pins[str(Path(__file__))] = sha(Path(__file__))
OUT.mkdir()
now = datetime.now(timezone.utc).isoformat()
result = dict(status='independent_source_and_native_gpu_depth_compared', finished_utc=now,
              lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.PROFONDEUR.VALEURS', input_pins=pins,
              rehashed_pins=len(pins)-1, source_depth_captured_inside_actual_pass=True,
              native_depth_copied_from_actual_Depth32Float_texture=True, native_stable_depth_readbacks=receipt['native_depth_consecutive_equal_readbacks'],
              original_native_material_colour_capture_byte_equal=True, coverage_mask_different_pixels=0,
              covered_pixels=int(mask.sum()), mean_camera_distance_error_metres=float(delta.mean()),
              max_camera_distance_error_metres=float(delta.max()), source_finite_reverse_z_projection=[projection['e22'],projection['e23']],
              native_infinite_reverse_z_near=near, raw_depth_equality_is_not_a_projection_equivalence_test=True,
              source_depth_positions_bit_equal=False, full_material_fidelity_qualified=False,
              source_render_parity_qualified=False, forge_editor_verified=False,
              full_import=False, complete_assets_delivered_increment=0,
              next_action='Isolate source/native texture sampling and derivatives; preserve exact coverage and measured camera-space depth evidence before correcting normal/material discrepancies.')
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
summary = f'Vrai Depth32Float Bevy copié, trois lectures stables ; couverture exactement identique à Unity sur 34 445 pixels. Après les projections respectives : erreur de distance moyenne {float(delta.mean()):.9f} m, maximale {float(delta.max()):.9f} m. Capture couleur antérieure inchangée ; fidélité matériau ouverte.'
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **PROFONDEUR.VALEURS : comparaison indépendante vérifiée**. {summary} [Preuve](../{OUT.name}/RESULT.v1.json). Aucun nouvel asset complet livré.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = read(path)
    data.update(updated_utc=now, last_technical_batch=result['lot'], last_technical_proof=str(OUT / 'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.MATERIAUX.SAMPLING', current_work_state=summary,
                next_action=result['next_action'], full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
print(json.dumps({key:value for key,value in result.items() if key != 'input_pins'}))
