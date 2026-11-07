"""Reopen in-pass source depth and compare it to native fragment coverage."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-door-source-depth-capture-v22'
OUT = W / 'townsmith-door-depth-coverage-reopen-v22'
assert not OUT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


run = read(ROOT / 'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0 and run['cases_captured'] == 5
assert run['source_inputs_unchanged'] and run['neutral_reference_byte_equal']
assert sha(ROOT / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
prep = read(ROOT / 'PREPARED.v1.json')
pins = dict(prep['input_pins'])
pins.update(prep['private_before_pins'])
for path in ROOT.iterdir():
    if path.is_file() and path.suffix in ('.json', '.rgba16f', '.rgba32f', '.r32f'):
        pins[str(path)] = sha(path)
native_path = W / 'townsmith-paint-door-native-diffuse-material-draw-v3/FORGE-LINEAR.rgba16f'
native_run_path = native_path.parent / 'RUN-RESULT.v1.json'
assert sha(native_path) == read(native_run_path)['forge_image_sha256']
pins[str(native_path)] = sha(native_path)
pins[str(native_run_path)] = sha(native_run_path)
native = np.fromfile(native_path, dtype='<f2').reshape(512, 512, 4)
coverage = native[:, :, 3] != 0
assert int(coverage.sum()) == 34445
rows = []
for case in read(ROOT / 'REQUEST.v1.json')['cases']:
    paths = sorted(ROOT.glob(case['id'] + '.depth-*.r32f'))
    assert len(paths) >= 3
    values = []
    for path in paths:
        metadata = read(path.with_suffix('.json'))
        assert metadata['captured_inside_actual_camera_pass']
        assert metadata['projection']['e11'] < 0 and metadata['projection']['e00'] > 0
        assert metadata['width'] == metadata['height'] == 512 and metadata['layers'] == 1
        assert path.stat().st_size == 512*512*4
        depth = np.fromfile(path, dtype='<f4').reshape(512, 512)
        assert np.isfinite(depth).all() and depth.min() == 0 and depth.max() < 1
        mask = np.flipud(depth) > 0
        assert np.array_equal(mask, coverage)
        values.append(depth)
    assert np.array_equal(values[-1], values[-2])
    rows.append(dict(case=case['id'], captures=len(paths), last_two_depth_arrays_byte_equal=True,
                     source_depth_covered_pixels=int((values[-1] > 0).sum()),
                     native_fragment_covered_pixels=int(coverage.sum()), mask_differing_pixels=0,
                     minimum_nonzero_depth=float(values[-1][values[-1] > 0].min()), maximum_depth=float(values[-1].max())))
for path, expected in pins.items():
    assert sha(path) == expected, path
pins[str(Path(__file__))] = sha(Path(__file__))
OUT.mkdir()
now = datetime.now(timezone.utc).isoformat()
result = dict(status='in_pass_source_depth_mask_matches_native_fragment_coverage', finished_utc=now,
              lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.PROFONDEUR.COUVERTURE', input_pins=pins,
              rehashed_pins=len(pins)-1, rows=rows,
              captured_inside_actual_camera_depth_and_normal_pass=True,
              source_depth_mask_vs_native_fragment_coverage_byte_equal=True,
              source_depth_row_normalization='Vertical inversion of source GPU camera projection with negative m11; no pixel fit.',
              native_independent_depth_buffer_values_captured=False,
              scope='LOD0 isolated original mesh/material camera fixture: source independent depth visibility compared to native original clipped fragment alpha coverage.',
              global_gbuffer_binding_lifetime_qualified=False,
              full_geometry_depth_positions_qualified=False, full_material_fidelity_qualified=False,
              source_render_parity_qualified=False, forge_editor_verified=False,
              full_import=False, complete_assets_delivered_increment=0,
              next_action='Capture native GPU depth values and native/source material normal, roughness and texture samples; preserve the now-proven LOD0 coverage and separate material/lighting discrepancies.')
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
summary = f'Profondeur source capturée pendant le vrai passage HDRP : {sum(row["captures"] for row in rows)} captures, dernières répétitions byte-equal. Masque de profondeur source et couverture native correspondent exactement sur 34 445 pixels, dans cinq contrôles. Valeurs de profondeur native et fidélité complète encore ouvertes.'
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **PROFONDEUR.COUVERTURE : contrôle technique vérifié**. {summary} [Preuve](../{OUT.name}/RESULT.v1.json). Aucun nouvel asset complet livré.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = read(path)
    data.update(updated_utc=now, last_technical_batch=result['lot'], last_technical_proof=str(OUT / 'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.MATERIAUX.DEPTH_VALUES', current_work_state=summary,
                next_action=result['next_action'], full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
print(json.dumps(dict(status=result['status'], pins=result['rehashed_pins'], rows=rows)))
