"""Compare material outputs while preserving unresolved buffer orientation."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw

W = Path('F:/OpenRPG_ImportWork')
DRAW = W / 'townsmith-paint-door-native-diffuse-material-draw-v3'
SRC = W / 'townsmith-door-source-gbuffer-arrays-v21'
OUT = W / 'townsmith-door-diffuse-material-comparison-v3'
assert not OUT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


run = read(DRAW / 'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0 and run['gpu_draw_executed']
assert sha(DRAW / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
assert sha(DRAW / 'FORGE-LINEAR.rgba16f') == run['forge_image_sha256']
pins = dict(read(DRAW / 'PREPARED.v1.json')['input_pins'])
carrier = W / 'townsmith-paint-door-diffuse-material-carrier-v3/RESULT.v1.json'
pins.update(read(carrier)['input_pins'])
for path in (carrier, DRAW / 'RUN-RESULT.v1.json', DRAW / 'NATIVE-RESULT.v1.json', DRAW / 'FORGE-LINEAR.rgba16f', SRC / 'door-nopost-linear.global-buffers.json', SRC / 'door-nopost-linear._GBufferTexture0.rgba32f'):
    pins[str(path)] = sha(path)
for path, expected in pins.items():
    assert sha(Path(path)) == expected, path
native = np.fromfile(DRAW / 'FORGE-LINEAR.rgba16f', dtype='<f2').reshape(512, 512, 4).astype(np.float32)
source = np.fromfile(SRC / 'door-nopost-linear._GBufferTexture0.rgba32f', dtype='<f4').reshape(512, 640, 4)
assert np.isfinite(native).all() and np.isfinite(source).all()
coverage = native[:, :, 3] != 0
assert coverage.any() and (~coverage).any()
assert np.all(native[:, :, 3][coverage] == 1) and np.all(native[~coverage] == 0)
rows = []
OUT.mkdir()
panel = Image.new('RGB', (1536, 540), '#202020')
draw = ImageDraw.Draw(panel)
images = [native[:, :, :3]]
for flip in (False, True):
    # Report both conventions explicitly. No selecting a best fit or geometry verdict.
    candidate = source[:, :512, :3]
    if flip:
        candidate = np.flipud(candidate)
    delta = np.abs(candidate - native[:, :, :3])[coverage]
    source_coverage = np.max(candidate, axis=2) > 0
    rows.append(dict(source_vertical_flip=flip, assumed_viewport_origin=[0, 0],
                     native_fragment_coverage_pixels=int(coverage.sum()),
                     source_nonzero_colour_pixels=int(source_coverage.sum()),
                     coverage_overlap_pixels=int((coverage & source_coverage).sum()),
                     native_coverage_mean_rgb_error=float(delta.mean()), max_rgb_error=float(delta.max()),
                     pixels_over_001=int((delta.max(axis=1) > .001).sum())))
    images.append(candidate)
for index, image in enumerate(images):
    srgb = np.where(image <= .0031308, image*12.92, 1.055*np.maximum(image, 0)**(1/2.4)-.055)
    panel.paste(Image.fromarray(np.round(np.clip(srgb, 0, 1)*255).astype('uint8')), (index*512, 28))
    draw.text((index*512+8, 6), ('Forge material', 'Source physical crop, unflipped', 'Source physical crop, vertically flipped')[index], fill='white')
panel.save(OUT / 'material-controls.png')
pins[str(Path(__file__))] = sha(Path(__file__))
pins[str(OUT / 'material-controls.png')] = sha(OUT / 'material-controls.png')
now = datetime.now(timezone.utc).isoformat()
result = dict(status='native_diffuse_material_actual_draw_compared_source_mapping_open', finished_utc=now,
              lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.MATERIAUX', input_pins=pins,
              rehashed_pins=len(pins)-2, rows=rows, native_fragment_coverage_pixels=int(coverage.sum()),
              native_background_rgba_zero=True, source_row_and_viewport_mapping_qualified=False,
              shader_control_bypasses_lighting=True, native_receipt_ambient_and_reflection_flags_describe_installed_resources_only_for_this_control=True,
              source_nonzero_colour_is_not_independent_depth_evidence=True,
              pixel_fit_or_threshold_relaxation=False, full_material_fidelity_qualified=False,
              full_import=False, complete_assets_delivered_increment=0, forge_editor_verified=False,
              next_action='Capture source depth inside the real camera pass; establish buffer row/viewport mapping and compare native depth/coverage independently.')
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
summary = f'Matériau seul dessiné dans Forge ; trois captures stables, fond RGBA nul et {int(coverage.sum())} pixels couverts. Deux conventions du buffer source mesurées sans choisir un fit ; orientation, viewport et profondeur restent ouverts.'
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **MATERIAUX : dessin isolé vérifié**. {summary} [Preuve](../{OUT.name}/RESULT.v1.json). Aucun nouvel asset complet livré.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = read(path)
    data.update(updated_utc=now, last_technical_batch=result['lot'], last_technical_proof=str(OUT / 'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.PROFONDEUR', current_work_state=summary,
                next_action=result['next_action'], full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
print(json.dumps(dict(status=result['status'], pins=result['rehashed_pins'], rows=rows)))
