"""Reopen source buffer observations without promoting lifetime to parity."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-door-source-gbuffer-arrays-v21'
OUT = W / 'townsmith-door-source-gbuffer-arrays-reopen-v21'
assert not OUT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


run = read(ROOT / 'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0 and run['cases_captured'] == 5
assert run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
assert sha(ROOT / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
prep = read(ROOT / 'PREPARED.v1.json')
pins = dict(prep['input_pins'])
pins.update(prep['private_before_pins'])
for path in ROOT.iterdir():
    if path.is_file() and path.suffix in ('.json', '.rgba32f', '.rgba16f'):
        pins[str(path)] = sha(path)
epoch_path = W / 'townsmith-door-source-gbuffer-capture-v19/HELPER-EPOCH.v1.json'
epoch = read(epoch_path)
assert sha(Path(epoch['saved_path'])) == epoch['sha256']
pins[str(epoch_path)] = sha(epoch_path)
pins[epoch['saved_path']] = epoch['sha256']
rows = []
cases = ['door-neutral', 'door-linear', 'door-nopost-linear', 'door-debug-DirectDiffuseLighting-nopost-linear', 'door-debug-DirectSpecularLighting-nopost-linear']
for case in cases:
    inventory = read(ROOT / (case + '.global-buffers.json'))
    assert inventory['actual_viewport_width'] == inventory['actual_viewport_height'] == 512
    light = read(ROOT / (case + '.source-light.json'))['Light']
    hdlight = read(ROOT / (case + '.source-hdlight.json'))['MonoBehaviour']
    assert light['m_Intensity'] == 15000 and light['m_Shadows']['m_Type'] == 0
    assert hdlight['m_LightDimmer'] == 1 and hdlight['m_AffectDiffuse'] and hdlight['m_AffectSpecular']
    captured = []
    for record in inventory['buffers']:
        if record['shader_property'] == '_CameraDepthTexture':
            assert not record['present'] and not record['gpu_loaded']
            continue
        assert record['present'] and record['gpu_loaded'] and not record['error']
        assert record['texture_dimension'] == 'Tex2DArray' and record['layer_count'] == 1
        assert record['width'] == 640 and record['height'] == 512
        path = ROOT / (case + '.' + record['shader_property'] + '.rgba32f')
        assert path.stat().st_size == record['float_count'] * 4
        values = np.fromfile(path, dtype='<f4')
        assert np.isfinite(values).all()
        captured.append(dict(property=record['shader_property'], graphics_format=record['graphics_format'],
                             sha256=sha(path), min=float(values.min()), max=float(values.max()),
                             nonzero_floats=int(np.count_nonzero(values))))
    assert len(captured) == 5
    diameter = hdlight['m_AngularDiameter']
    max_smoothness = min(1, max(0, 1.35 / (1 + (1.15 * (.0315 * diameter + .4)) ** 2) - .11))
    rows.append(dict(case=case, observed_buffers=captured, viewport=[512, 512], physical_layout=[640, 512, 1],
                     light_intensity=15000, source_shadows_enabled=False, angular_diameter_degrees=diameter,
                     source_sdk_derived_min_roughness=(1-max_smoothness)**2, gpu_light_buffer_value_independently_qualified=False))
for path, expected in pins.items():
    assert sha(path) == expected, path
OUT.mkdir()
panel = Image.new('RGB', (1280, 300), '#202020')
draw = ImageDraw.Draw(panel)
for index, record in enumerate(rows[2]['observed_buffers']):
    values = np.fromfile(ROOT / ('door-nopost-linear.' + record['property'] + '.rgba32f'), dtype='<f4').reshape(512, 640, 4)
    rgb = np.maximum(values[:, :, :3], 0)
    rgb = rgb / (1 + rgb)
    image = Image.fromarray(np.round(np.clip(rgb, 0, 1) * 255).astype('uint8')).resize((256, 205))
    draw.text((index*256+4, 4), record['property'], fill='white')
    panel.paste(image, (index*256, 28))
panel.save(OUT / 'physical-buffer-observations.png')
pins[str(Path(__file__))] = sha(Path(__file__))
pins[str(OUT / 'physical-buffer-observations.png')] = sha(OUT / 'physical-buffer-observations.png')
now = datetime.now(timezone.utc).isoformat()
result = dict(status='source_global_array_buffers_and_light_observations_reopened', finished_utc=now,
              lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.GBUFFER.CAPTURE', input_pins=pins,
              rehashed_pins=len(pins)-2, rows=rows, original_sources_and_reference_unchanged=True,
              loaded_buffer_observations=25, source_gbuffer0_format='R8G8B8A8_SRGB',
              source_gbuffer3_format='B10G11R11_UFloatPack32', source_depth_global_unavailable=True,
              global_resource_lifetime_and_actual_lighting_binding_qualified=False,
              viewport_origin_and_row_mapping_qualified=False, independent_geometry_masks_qualified=False,
              full_material_fidelity_qualified=False, source_render_parity_qualified=False,
              forge_editor_verified=False, full_import=False, complete_assets_delivered_increment=0,
              next_action='Apply and compare source sRGB GBuffer diffuse projection, preserving authored graph values; verify global lifetime/viewport and independent source/native depth masks before geometry parity.')
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
summary = '25 lectures de buffers GPU source réouvertes : tableaux physiques 640×512×1, viewport 512×512, couleur diffuse sRGB8, indirect R11G11B10. Soleil 15000 lux, diamètre 0,5°, ombres désactivées. Profondeur globale absente ; durée de vie/binding et masques indépendants restent ouverts.'
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **GBUFFER.CAPTURE : observations GPU vérifiées**. {summary} [Preuve](../{OUT.name}/RESULT.v1.json). Aucun nouvel asset complet livré.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = read(path)
    data.update(updated_utc=now, last_technical_batch=result['lot'], last_technical_proof=str(OUT / 'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.GBUFFER.APPLICATION', current_work_state=summary,
                next_action=result['next_action'], full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print(json.dumps(dict(status=result['status'], pins=result['rehashed_pins'], buffer_observations=25)))
