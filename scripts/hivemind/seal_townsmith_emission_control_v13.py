"""Reopen emission and measure the unfitted native five-component reconstruction."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np

W = Path('F:/OpenRPG_ImportWork')
SOURCE = W / 'townsmith-door-source-emission-control-v13'
OUT = W / 'townsmith-door-source-emission-control-reopen-v13'
assert not OUT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def pixels(path):
    assert path.stat().st_size == 512 * 512 * 8
    return np.flipud(np.fromfile(path, dtype='<f2').reshape(512, 512, 4)).astype(np.float32)[:, :, :3]


run = read(SOURCE / 'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0 and run['cases_captured'] == 10
assert run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
assert sha(SOURCE / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
prepared = read(SOURCE / 'PREPARED.v1.json')
pins = dict(prepared['input_pins'])
pins.update(prepared['private_before_pins'])
for path, expected in pins.items():
    assert sha(path) == expected, path
native_path = W / 'townsmith-paint-door-native-ambient-draw-v1/FORGE-LINEAR.rgba16f'
native = np.fromfile(native_path, dtype='<f2').reshape(512, 512, 4).astype(np.float32)[:, :, :3]
roi = np.max(np.abs(native - native[0, 0]), axis=2) > .001
pieces = []
rows = []
for mode in ('DirectDiffuseLighting', 'DirectSpecularLighting', 'IndirectDiffuseLighting', 'ReflectionLighting', 'EmissiveLighting'):
    case = 'door-debug-' + mode + '-linear'
    assert read(SOURCE / f'{case}.context.json')['actual_debug_lighting_mode'] == mode
    path = SOURCE / f'{case}.rgba16f'
    values = pixels(path)
    assert np.isfinite(values).all()
    pieces.append(values)
    rows.append(dict(mode=mode, fixed_roi_mean_rgb=float(values[roi].mean()), raw_sha256=sha(path)))
    if mode != 'EmissiveLighting':
        assert sha(path) == sha(W / f'townsmith-door-source-lighting-controls-v12/{case}.rgba16f')
total_path = SOURCE / 'door-linear.rgba16f'
total = pixels(total_path)
four = sum(pieces[:4])
five = four + pieces[4]
before = np.abs(four - total)[roi]
after = np.abs(five - total)[roi]
for path in SOURCE.iterdir():
    if path.is_file() and path.suffix in ('.json', '.rgba16f', '.rgba32f'):
        pins[str(path)] = sha(path)
pins[str(native_path)] = sha(native_path)
pins[str(Path(__file__))] = sha(Path(__file__))
OUT.mkdir()
now = datetime.now(timezone.utc).isoformat()
result = dict(status='native_emission_captured_five_component_reconstruction_measured', lot='L03.TOWNSMITH.DESSIN.02.COMPOSANTES.EMISSION', finished_utc=now,
              input_pins=pins, rehashed_source_pins=len(prepared['input_pins']) + len(prepared['private_before_pins']),
              original_reference_byte_equal=True, previous_four_lighting_controls_byte_equal=True,
              source_inputs_unchanged=True, native_mode_observed='EmissiveLighting',
              source_emission_nonzero=True, fixed_forge_roi_pixels=int(roi.sum()), fixed_roi_is_geometry_evidence=False,
              source_rows=rows, raw_four_component_mean_error=float(before.mean()),
              raw_five_component_mean_error=float(after.mean()), raw_five_component_max_error=float(after.max()),
              raw_five_component_pixels_over_001=int((after.max(axis=1) > .001).sum()),
              pixel_fit_or_threshold_relaxation=False,
              exact_component_reconstruction_qualified=False,
              forge_emission_equivalence_qualified=False, source_render_parity_qualified=False,
              forge_editor_verified=False, full_import=False, complete_assets_delivered_increment=0)
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
summary = f"Émission source non nulle capturée ; la somme brute des cinq composantes donne une erreur moyenne {after.mean():.6f}, contre {before.mean():.6f} sans émission, sur la région fixe. Pas de fit ni de seuil ajusté. Contrôle d'émission Forge lancé ; équivalence et fidélité ouvertes."
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **DESSIN.02.COMPOSANTES.EMISSION : source vérifiée**. {summary} [Preuve](../townsmith-door-source-emission-control-reopen-v13/RESULT.v1.json). Aucun nouvel asset complet livré.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = read(path)
    data.update(updated_utc=now, last_technical_batch='L03.TOWNSMITH.DESSIN.02.COMPOSANTES.EMISSION', last_technical_proof=str(OUT / 'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.EMISSION.FORGE', current_work_state=summary,
                current_work_receipt=str(W / 'townsmith-paint-door-native-emission-draw-v1/RUN-RESULT.v1.json'),
                next_action='Comparer le dessin Forge isolant l’émission au témoin natif HDRP ; qualifier les unités et traitements intermédiaires.', full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print(json.dumps({key: value for key, value in result.items() if key not in ('input_pins', 'source_rows')}))
