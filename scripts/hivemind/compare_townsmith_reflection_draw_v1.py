"""Compare actual linear readbacks before and after measured scene irradiance."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-paint-door-native-reflection-comparison-v1'
DRAW = W / 'townsmith-paint-door-native-reflection-draw-v1'
assert not ROOT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


run = read(DRAW / 'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0 and run['gpu_draw_executed']
assert sha(DRAW / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
native = read(DRAW / 'NATIVE-RESULT.v1.json')
assert native['measured_sky_reflection_applied']
assert native['consecutive_equal_readbacks'] >= 3 and native['measured_gpu_ambient_applied']
prepared = read(DRAW / 'PREPARED.v1.json')
pins = dict(prepared['input_pins'])
for path, expected in pins.items():
    assert sha(path) == expected, path
source_path = W / 'townsmith-door-source-camera-brdf-v10/door-linear.rgba16f'
before_path = W / 'townsmith-paint-door-native-ambient-draw-v1/FORGE-LINEAR.rgba16f'
after_path = DRAW / 'FORGE-LINEAR.rgba16f'
for path in (source_path, before_path, after_path):
    assert path.stat().st_size == 512 * 512 * 8
source = np.flipud(np.fromfile(source_path, dtype='<f2').reshape(512, 512, 4)).astype(np.float32)
before = np.fromfile(before_path, dtype='<f2').reshape(512, 512, 4).astype(np.float32)
after = np.fromfile(after_path, dtype='<f2').reshape(512, 512, 4).astype(np.float32)
assert np.isfinite(after).all()
threshold = .001
old_error = np.abs(source[:, :, :3] - before[:, :, :3])
new_error = np.abs(source[:, :, :3] - after[:, :, :3])
changed = np.max(np.abs(after[:, :, :3] - before[:, :, :3]), axis=2) > threshold
# A fixed region from the old Forge render is only a diagnostic ROI. It is not
# a source silhouette, depth mask, fit, or geometry equivalence qualification.
roi = np.max(np.abs(before[:, :, :3] - before[0, 0, :3]), axis=2) > threshold
assert roi.any()
ROOT.mkdir()


def display(linear):
    # Display-only Reinhard and sRGB; all reported metrics use untouched linear data.
    value = np.maximum(linear[:, :, :3], 0)
    value = value / (1 + value)
    value = np.where(value <= .0031308, 12.92 * value, 1.055 * value ** (1 / 2.4) - .055)
    return Image.fromarray(np.round(np.clip(value, 0, 1) * 255).astype('uint8'))


panel = Image.new('RGB', (1536, 540), '#202020')
draw = ImageDraw.Draw(panel)
for index, (label, image) in enumerate((('Unity source', source), ('Forge before reflections', before), ('Forge measured reflections', after))):
    draw.text((index * 512 + 8, 7), label, fill='white')
    panel.paste(display(image), (index * 512, 28))
panel.save(ROOT / 'comparison.png')
for path in (source_path, before_path, after_path, DRAW / 'RUN-RESULT.v1.json', DRAW / 'NATIVE-RESULT.v1.json', Path(__file__), ROOT / 'comparison.png'):
    pins[str(path)] = sha(path)
result = dict(status='measured_reflection_actual_draw_compared_fidelity_open',
              lot='L03.TOWNSMITH.DESSIN.02.REFLETS.APPLICATION', finished_utc=datetime.now(timezone.utc).isoformat(),
              input_pins=pins, rehashed_pins=len(prepared['input_pins']),
              gpu_draw_executed=True, stable_native_readbacks=native['consecutive_equal_readbacks'],
              measured_sky_reflection_applied=True, measured_gpu_ambient_applied=True, rgb_threshold=threshold,
              source_row_convention='Unity bottom-to-top normalized once',
              pixel_fit_or_threshold_relaxation=False,
              changed_pixels_after_reflection=int(changed.sum()),
              before_full_image_mean_rgb_error=float(old_error.mean()),
              after_full_image_mean_rgb_error=float(new_error.mean()),
              after_full_image_max_rgb_error=float(new_error.max()),
              after_pixels_over_threshold=int((new_error.max(axis=2) > threshold).sum()),
              fixed_previous_forge_roi_pixels=int(roi.sum()),
              fixed_roi_before_mean_rgb_error=float(old_error[roi].mean()),
              fixed_roi_after_mean_rgb_error=float(new_error[roi].mean()),
              fixed_roi_is_geometry_evidence=False,
              display_images_are_diagnostic_only=True,
              source_diffuse_brdf_variant_qualified=False,
              source_specular_reflection_applied=True, source_background_sky_applied=False, source_ssao_qualified=False,
              source_render_parity_qualified=False, forge_editor_verified=False,
              full_import=False, complete_assets_delivered_increment=0)
(ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print(json.dumps({key: value for key, value in result.items() if key != 'input_pins'}))
