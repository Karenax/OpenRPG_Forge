"""Reopen native lighting controls and document raw component witnesses."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw

W = Path('F:/OpenRPG_ImportWork')
SOURCE = W / 'townsmith-door-source-lighting-controls-v12'
OUT = W / 'townsmith-door-source-lighting-controls-reopen-v12'
assert not OUT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


run = read(SOURCE / 'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0 and run['cases_captured'] == 9
assert run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
assert sha(SOURCE / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
prepared = read(SOURCE / 'PREPARED.v1.json')
pins = dict(prepared['input_pins'])
pins.update(prepared['private_before_pins'])
for path, expected in pins.items():
    assert sha(path) == expected, path
baseline_path = W / 'townsmith-paint-door-native-ambient-draw-v1/FORGE-LINEAR.rgba16f'
baseline = np.fromfile(baseline_path, dtype='<f2').reshape(512, 512, 4).astype(np.float32)
roi = np.max(np.abs(baseline[:, :, :3] - baseline[0, 0, :3]), axis=2) > .001
assert roi.any()
pins[str(baseline_path)] = sha(baseline_path)
rows = []
images = []
for mode in ('DirectDiffuseLighting', 'DirectSpecularLighting', 'IndirectDiffuseLighting', 'ReflectionLighting', 'IndirectDiffuseOcclusion', 'IndirectSpecularOcclusion'):
    case = 'door-debug-' + mode + '-linear'
    context_path = SOURCE / f'{case}.context.json'
    context = read(context_path)
    assert context['actual_debug_lighting_mode'] == mode
    assert context['actual_camera_history'] == 'CustomUserHistory0-existing'
    path = SOURCE / f'{case}.rgba16f'
    assert path.stat().st_size == 512 * 512 * 8
    pixels = np.flipud(np.fromfile(path, dtype='<f2').reshape(512, 512, 4)).astype(np.float32)
    assert np.isfinite(pixels).all()
    rows.append(dict(mode=mode, raw_capture=str(path), sha256=sha(path),
                     rgb_min=float(pixels[:, :, :3].min()), rgb_max=float(pixels[:, :, :3].max()),
                     fixed_forge_roi_rgb_min=float(pixels[:, :, :3][roi].min()), fixed_forge_roi_rgb_max=float(pixels[:, :, :3][roi].max()),
                     fixed_forge_roi_mean_rgb=float(pixels[:, :, :3][roi].mean()),
                     fixed_roi_is_geometry_evidence=False, debug_display_exposure_equivalence_qualified=False))
    value = np.maximum(pixels[:, :, :3], 0)
    value = value / (1 + value)
    value = np.where(value <= .0031308, 12.92 * value, 1.055 * value ** (1 / 2.4) - .055)
    images.append((mode, Image.fromarray(np.round(np.clip(value, 0, 1) * 255).astype('uint8'))))
    for item in (path, context_path, SOURCE / f'{case}.png'):
        pins[str(item)] = sha(item)
assert len({row['sha256'] for row in rows}) == 6
for case in ('door-neutral', 'door-linear', 'door-fixedbackground-linear'):
    assert read(SOURCE / f'{case}.context.json')['actual_debug_lighting_mode'] == 'None'
assert sha(SOURCE / 'door-linear.rgba16f') == sha(W / 'townsmith-door-source-occlusion-v11/door-linear.rgba16f')
for name in ('PREPARED.v1.json', 'RUN-RESULT.v1.json', 'NATIVE-RESULT.v1.json'):
    pins[str(SOURCE / name)] = sha(SOURCE / name)
pins[str(Path(__file__))] = sha(Path(__file__))
OUT.mkdir()
panel = Image.new('RGB', (768, 568), '#202020')
draw = ImageDraw.Draw(panel)
for index, (mode, image) in enumerate(images):
    x = index % 3 * 256
    y = index // 3 * 284
    draw.text((x + 5, y + 7), mode, fill='white')
    panel.paste(image.resize((256, 256), Image.Resampling.LANCZOS), (x, y + 28))
panel.save(OUT / 'lighting-controls.png')
pins[str(OUT / 'lighting-controls.png')] = sha(OUT / 'lighting-controls.png')
now = datetime.now(timezone.utc).isoformat()
result = dict(status='six_distinct_native_hdrp_lighting_controls_captured_and_reopened', finished_utc=now,
              lot='L03.TOWNSMITH.DESSIN.02.OCCLUSION.CONTROLES', input_pins=pins, rehashed_source_pins=len(prepared['input_pins']) + len(prepared['private_before_pins']),
              original_neutral_and_linear_references_byte_equal=True, source_inputs_unchanged=True,
              original_graph_and_textures_preserved=True, native_requested_debug_modes_observed=True,
              debug_state_restored=True, raw_rows=rows, display_images_are_diagnostic_only=True,
              source_ssao_texture_pixels_used_as_target_input=False,
              debug_display_exposure_equivalence_qualified=False,
              source_render_parity_qualified=False, forge_editor_verified=False, full_import=False,
              complete_assets_delivered_increment=0,
              next_action='Separate native diffuse/specular/occlusion witnesses and source intermediate formats, compare matching Forge components, then correct the common transport.')
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
summary = 'Six diagnostics HDRP natifs distincts capturés : diffuse directe, spéculaire directe, diffuse indirecte, reflets, occlusion diffuse et spéculaire. Références et sources inchangées ; état debug restauré. Comparaison des composantes et formats intermédiaires restante.'
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **DESSIN.02.OCCLUSION.CONTROLES : capture technique vérifiée**. {summary} [Preuve](../townsmith-door-source-lighting-controls-reopen-v12/RESULT.v1.json) · [Vues diagnostiques](../townsmith-door-source-lighting-controls-reopen-v12/lighting-controls.png). Aucun nouvel asset complet livré.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = read(path)
    data.update(updated_utc=now, last_technical_batch='L03.TOWNSMITH.DESSIN.02.OCCLUSION.CONTROLES', last_technical_proof=str(OUT / 'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.COMPOSANTES', current_work_state=summary,
                next_action=result['next_action'], full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print(json.dumps({'status': result['status'], 'rows': rows, 'full_import': False}))
