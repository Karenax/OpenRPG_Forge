"""Close the isolated emission/bloom cause with original references preserved."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np

W = Path('F:/OpenRPG_ImportWork')
OUT = W / 'townsmith-door-postprocess-cause-reopen-v17'
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
for directory, count in (('townsmith-door-source-postprocess-controls-v16', 14), ('townsmith-door-source-bloom-controls-v17', 17)):
    root = W / directory
    run = read(root / 'RUN-RESULT.v1.json')
    assert run['actual_exit_code'] == 0 and run['cases_captured'] == count
    assert run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
    assert sha(root / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
    prepared = read(root / 'PREPARED.v1.json')
    pins.update(prepared['input_pins'])
    pins.update(prepared['private_before_pins'])
    for path in root.iterdir():
        if path.is_file() and path.suffix in ('.json', '.rgba16f'):
            pins[str(path)] = sha(path)
for path, expected in pins.items():
    assert sha(path) == expected, path
native_path = W / 'townsmith-paint-door-native-emission-draw-v1/FORGE-LINEAR.rgba16f'
native_run_path = native_path.parent / 'RUN-RESULT.v1.json'
native_run = read(native_run_path)
assert native_run['actual_exit_code'] == 0 and native_run['gpu_draw_executed']
assert sha(native_path) == native_run['forge_image_sha256']
baseline_path = W / 'townsmith-paint-door-native-ambient-draw-v1/FORGE-LINEAR.rgba16f'
baseline = pixels(baseline_path)
roi = np.max(np.abs(baseline - baseline[0, 0]), axis=2) > .001
native = pixels(native_path)
rows = []
exposure = None
for version, case in ((16, 'door-linear'), (16, 'door-debug-EmissiveLighting-linear'), (16, 'door-emission-dark-nopost-linear'), (16, 'door-debug-EmissiveLighting-nopost-linear'), (17, 'door-debug-EmissiveLighting-nobloom-linear'), (17, 'door-emission-dark-nobloom-linear')):
    directory = 'townsmith-door-source-postprocess-controls-v16' if version == 16 else 'townsmith-door-source-bloom-controls-v17'
    root = W / directory
    context = read(root / f'{case}.context.json')
    assert context['actual_color_buffer_format'] == 'R11G11B10'
    assert context['exposure_texture_present'] == [False, True, True]
    measured = context['exposure_texture_rgba'][4]
    assert measured == context['exposure_texture_rgba'][8]
    exposure = measured if exposure is None else exposure
    assert measured == exposure
    source = np.flipud(pixels(root / f'{case}.rgba16f'))
    delta = np.abs(source - native)[roi]
    bloom = read(root / f'{case}.active-Bloom.json')['MonoBehaviour']
    rows.append(dict(case=case, postprocess_enabled=context['actual_postprocess_enabled'],
                     bloom_intensity=bloom['intensity']['m_Value'], bloom_threshold=bloom['threshold']['m_Value'],
                     exposure_gpu_multiplier=measured, fixed_roi_source_mean=float(source[roi].mean()),
                     fixed_roi_source_max=float(source[roi].max()), fixed_roi_native_mean=float(native[roi].mean()),
                     fixed_roi_mean_error=float(delta.mean()), fixed_roi_max_error=float(delta.max()),
                     fixed_roi_pixels_over_001=int((delta.max(axis=1) > .001).sum())))
assert rows[1]['fixed_roi_source_mean'] > .02
for row in rows[2:4]:
    assert row['fixed_roi_max_error'] == 0 and row['fixed_roi_pixels_over_001'] == 0
for row in rows[4:]:
    assert row['postprocess_enabled'] and row['bloom_intensity'] == 0
    assert row['fixed_roi_pixels_over_001'] == 0
for path in (native_path, native_run_path, baseline_path, Path(__file__)):
    pins[str(path)] = sha(path)
# Preserve the compile refusal through its exact saved helper epoch.
epoch_path = W / 'townsmith-door-source-postprocess-controls-v15/HELPER-EPOCH.v1.json'
epoch = read(epoch_path)
assert sha(Path(epoch['saved_path'])) == epoch['sha256']
pins[str(epoch_path)] = sha(epoch_path)
pins[epoch['saved_path']] = epoch['sha256']
OUT.mkdir()
now = datetime.now(timezone.utc).isoformat()
result = dict(status='bloom_causes_emissive_debug_residual_isolated_dark_emission_control_matches', finished_utc=now,
              lot='L03.TOWNSMITH.DESSIN.02.EMISSION', input_pins=pins, rehashed_pins=len(pins),
              original_sources_and_references_unchanged=True, source_color_buffer_format='R11G11B10',
              source_gpu_exposure_multiplier=exposure, source_exposure_ev100=8,
              source_bloom_intensity=.2, source_bloom_threshold=0,
              fixed_forge_roi_pixels=int(roi.sum()), fixed_roi_is_geometry_evidence=False, rows=rows,
              isolated_dark_emission_control_zero_byte_values_match=True,
              isolated_emission_control_scope='Original LOD0 material fixture, fixed prior Forge ROI, no illumination, no post-processing.',
              raw_emissive_debug_image_is_not_intrinsic_material_emission=True,
              pixel_fit_or_threshold_relaxation=False, bloom_application_in_forge_qualified=False,
              full_material_fidelity_qualified=False, source_render_parity_qualified=False,
              forge_editor_verified=False, full_import=False, complete_assets_delivered_increment=0,
              next_action='Capture original lighting components without bloom/post-processing, compare the direct transport and port the measured HDRP scene bloom separately.')
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
summary = 'Émission du témoin isolé vérifiée : Unity et Forge donnent zéro sans lumière ni post-traitement. Le bloom HDRP à 0,2 / seuil 0 explique le témoin Emissive lumineux ; retirer uniquement le bloom ramène l’écart sous 0,001. Exposition GPU inchangée : 0,0032552082557231188 à EV100 8. Rendu complet, bloom Forge et Studio ouverts.'
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **DESSIN.02.EMISSION : témoin technique clos**. {summary} [Preuve](../townsmith-door-postprocess-cause-reopen-v17/RESULT.v1.json). Aucun nouvel asset complet livré.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = read(path)
    data.update(updated_utc=now, last_technical_batch='L03.TOWNSMITH.DESSIN.02.EMISSION', last_technical_proof=str(OUT / 'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT', current_work_state=summary,
                next_action=result['next_action'], full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print(json.dumps({'status': result['status'], 'pins': len(pins), 'exposure': exposure, 'full_import': False}))
