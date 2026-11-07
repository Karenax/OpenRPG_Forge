"""Reopen actual camera/BRDF/occlusion inputs and retain the reflected draw refusal."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
OUT = W / 'townsmith-door-source-scene-inputs-reopen-v11'
assert not OUT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


pins = {}
for version, directory in ((10, 'townsmith-door-source-camera-brdf-v10'), (11, 'townsmith-door-source-occlusion-v11')):
    root = W / directory
    run = read(root / 'RUN-RESULT.v1.json')
    assert run['actual_exit_code'] == 0 and run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
    assert sha(root / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
    prepared = read(root / 'PREPARED.v1.json')
    pins.update(prepared['input_pins'])
    pins.update(prepared['private_before_pins'])
    for path in root.iterdir():
        if path.is_file() and path.suffix in ('.json', '.rgba16f', '.rgba32f'):
            pins[str(path)] = sha(path)
    for case in ('door-neutral', 'door-linear', 'door-fixedbackground-linear'):
        assert sha(root / f'{case}.fgd.rgba32f') == 'd962fd1333474b433e12b951ee7f492d7f21307dc28f33d1b9ff6d4a51cd769c'
context = read(W / 'townsmith-door-source-occlusion-v11/door-linear.context.json')
assert context['actual_lit_shader_mode'] == 'Deferred'
assert context['actual_ssao_enabled'] and context['actual_ssao_intensity'] == .5
assert context['actual_ssao_specular_blend'] == .5
assert context['actual_indirect_diffuse_multiplier'] == context['actual_reflection_multiplier'] == 1
comparison_path = W / 'townsmith-paint-door-native-reflection-comparison-v1/RESULT.v1.json'
comparison = read(comparison_path)
assert comparison['measured_sky_reflection_applied'] and comparison['stable_native_readbacks'] == 3
assert not comparison['source_render_parity_qualified']
assert comparison['fixed_roi_after_mean_rgb_error'] > comparison['fixed_roi_before_mean_rgb_error']
for path in (comparison_path, Path(__file__)):
    pins[str(path)] = sha(path)
for path, expected in pins.items():
    assert sha(path) == expected, path
OUT.mkdir()
now = datetime.now(timezone.utc).isoformat()
result = dict(status='actual_deferred_camera_brdf_and_occlusion_inputs_reopened_reflection_parity_refused', finished_utc=now,
              lot='L03.TOWNSMITH.DESSIN.02.REFLETS', input_pins=pins, rehashed_pins=len(pins), source_context=context,
              source_camera_mode_measured='Deferred', source_camera_gpu_projection_captured=True,
              source_brdf_byte_equal_to_shared_table=True, source_ssao_enabled=True, source_ssao_intensity=.5,
              source_ssao_specular_blend=.5, source_indirect_multipliers=[1, 1],
              declared_ao_settings_are_not_gpu_pixel_capture=True,
              shader_get_global_vector_does_not_qualify_hdrp_cbuffer=True,
              gpu_reflection_draw_executed=True, stable_native_readbacks=3,
              fixed_roi_error_before=comparison['fixed_roi_before_mean_rgb_error'],
              fixed_roi_error_after=comparison['fixed_roi_after_mean_rgb_error'],
              source_render_parity_qualified=False, forge_editor_verified=False, full_import=False,
              complete_assets_delivered_increment=0,
              next_action='Capture native HDRP lighting components and occlusion, isolate the residual, then correct the common scene transport.')
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
summary = 'Mode HDRP Deferred, projection GPU et table BRDF source vérifiés. Reflets réellement dessinés, trois captures stables ; fidélité refusée, erreur région fixe 0,0313 -> 0,0367. Occlusion source active à 0,5, absente du contrôle Forge ; composantes natives à isoler.'
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **DESSIN.02.REFLETS : contrôle exécuté, fidélité refusée**. {summary} [Preuve](../townsmith-door-source-scene-inputs-reopen-v11/RESULT.v1.json). Aucun nouvel asset complet livré.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = read(path)
    data.update(updated_utc=now, last_technical_batch='L03.TOWNSMITH.DESSIN.02.REFLETS', last_technical_proof=str(OUT / 'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.OCCLUSION', current_work_state=summary,
                current_work_receipt=str(W / 'townsmith-door-source-lighting-controls-v12/RUN-RESULT.v1.json'),
                next_action=result['next_action'], full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print(json.dumps({'status': result['status'], 'rehashed_pins': len(pins), 'full_import': False}))
