"""Compare the dark source control; do not infer intrinsic emission from debug images."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np

W = Path('F:/OpenRPG_ImportWork')
SOURCE = W / 'townsmith-door-source-dark-control-v14'
NATIVE = W / 'townsmith-paint-door-native-emission-draw-v1'
OUT = W / 'townsmith-door-dark-emission-comparison-reopen-v14'
assert not OUT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def pixels(path):
    assert path.stat().st_size == 512 * 512 * 8
    return np.fromfile(path, dtype='<f2').reshape(512, 512, 4).astype(np.float32)[:, :, :3]


pins = {}
for root, count in ((SOURCE, 11), (NATIVE, None)):
    run = read(root / 'RUN-RESULT.v1.json')
    assert run['actual_exit_code'] == 0
    assert sha(root / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
    if count:
        assert run['cases_captured'] == count and run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
    else:
        native_receipt = read(root / 'NATIVE-RESULT.v1.json')
        assert native_receipt['emission_only_diagnostic'] and native_receipt['consecutive_equal_readbacks'] >= 3
        assert native_receipt['directional_illuminance'] == 0
    prepared = read(root / 'PREPARED.v1.json')
    pins.update(prepared['input_pins'])
    pins.update(prepared.get('private_before_pins', {}))
    for name in ('PREPARED.v1.json', 'RUN-RESULT.v1.json', 'NATIVE-RESULT.v1.json'):
        pins[str(root / name)] = sha(root / name)
for path, expected in pins.items():
    assert sha(path) == expected, path
context_path = SOURCE / 'door-emission-dark-linear.context.json'
context = read(context_path)
assert context['dark_source_control'] and context['actual_debug_lighting_mode'] == 'None'
assert not context['actual_fog_enabled'] and context['actual_ssao_intensity'] == 0
assert context['actual_indirect_diffuse_multiplier'] == context['actual_reflection_multiplier'] == context['light_intensity'] == 0
baseline_path = W / 'townsmith-paint-door-native-ambient-draw-v1/FORGE-LINEAR.rgba16f'
baseline = pixels(baseline_path)
roi = np.max(np.abs(baseline - baseline[0, 0]), axis=2) > .001
source_path = SOURCE / 'door-emission-dark-linear.rgba16f'
native_path = NATIVE / 'FORGE-LINEAR.rgba16f'
source = np.flipud(pixels(source_path))
native = pixels(native_path)
delta = np.abs(source - native)[roi]
assert np.isfinite(source).all() and np.isfinite(native).all()
debug_path = W / 'townsmith-door-source-emission-control-v13/door-debug-EmissiveLighting-linear.rgba16f'
debug = np.flipud(pixels(debug_path))
for path in (source_path, native_path, context_path, baseline_path, debug_path, Path(__file__)):
    pins[str(path)] = sha(path)
OUT.mkdir()
now = datetime.now(timezone.utc).isoformat()
result = dict(status='source_dark_control_and_native_emission_draw_compared', lot='L03.TOWNSMITH.DESSIN.02.EMISSION.TEMOIN', finished_utc=now,
              input_pins=pins, rehashed_pins=len(pins), source_settings=context,
              original_source_and_references_preserved=True, stable_native_readbacks=3,
              fixed_forge_roi_pixels=int(roi.sum()), fixed_roi_is_geometry_evidence=False,
              source_dark_roi_min=float(source[roi].min()), source_dark_roi_max=float(source[roi].max()), source_dark_roi_mean=float(source[roi].mean()),
              native_dark_roi_min=float(native[roi].min()), native_dark_roi_max=float(native[roi].max()), native_dark_roi_mean=float(native[roi].mean()),
              dark_comparison_mean_error=float(delta.mean()), dark_comparison_max_error=float(delta.max()),
              dark_comparison_pixels_over_001=int((delta.max(axis=1) > .001).sum()), rgb_threshold=.001,
              source_emissive_debug_roi_mean=float(debug[roi].mean()),
              raw_emissive_debug_image_does_not_qualify_intrinsic_material_emission=True,
              prior_v13_source_emission_nonzero_field_scope='Nonzero Emissive debug image only; intrinsic emission was not qualified.',
              pixel_fit_or_threshold_relaxation=False, intrinsic_emission_parity_qualified=False,
              source_render_parity_qualified=False, forge_editor_verified=False, full_import=False,
              complete_assets_delivered_increment=0,
              next_action='Use the original dark control and exact source G-buffer semantics to qualify intrinsic emission and diagnose lighting/debug composition independently.')
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
summary = f"Témoin Unity sans lumière/brouillard comparé au dessin Forge isolé : moyenne source {source[roi].mean():.6f}, Forge {native[roi].mean():.6f}, erreur {delta.mean():.6f}, {result['dark_comparison_pixels_over_001']} pixels hors seuil. Le mode Emissive non nul ne qualifie pas à lui seul l'émission intrinsèque du matériau ; les preuves précédentes sont conservées avec cette limitation."
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **DESSIN.02.EMISSION.TEMOIN : contrôle exécuté**. {summary} [Preuve](../townsmith-door-dark-emission-comparison-reopen-v14/RESULT.v1.json). Fidélité et Studio ouverts ; aucun nouvel asset complet livré.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = read(path)
    data.update(updated_utc=now, last_technical_batch='L03.TOWNSMITH.DESSIN.02.EMISSION.TEMOIN', last_technical_proof=str(OUT / 'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT', current_work_state=summary,
                next_action=result['next_action'], full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print(json.dumps({key: value for key, value in result.items() if key not in ('input_pins', 'source_settings')}))
