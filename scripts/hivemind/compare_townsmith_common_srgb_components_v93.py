"""Measure unfitted source/native lighting deltas in the fixed prior Forge ROI."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw

W = Path('F:/OpenRPG_ImportWork')
SRC = W / 'townsmith-door-source-direct-controls-v18'
OUT = W / 'townsmith-common-srgb-components-comparison-v93'
assert not OUT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def pixels(path, source=False):
    assert path.stat().st_size == 512 * 512 * 8
    value = np.fromfile(path, dtype='<f2').reshape(512, 512, 4).astype(np.float32)[:, :, :3]
    assert np.isfinite(value).all()
    return np.flipud(value) if source else value


def packed_pixels(path):
    assert path.stat().st_size==512*512*4
    packed=np.fromfile(path,dtype='<u4').reshape(512,512)
    channels=[]
    for shift,bits in [(0,6),(11,6),(22,5)]:
        value=(packed>>shift)&((1<<(bits+5))-1)
        mant=value&((1<<bits)-1);exponent=value>>bits
        assert not np.any(exponent==31)
        scale=np.where(exponent==0,-14,exponent.astype('int32')-15)
        significand=mant.astype('float64')/(1<<bits)+np.where(exponent==0,0.,1.)
        channels.append(np.ldexp(significand,scale).astype('<f4'))
    return np.stack(channels,-1)

receipt_path = W / 'townsmith-door-source-direct-controls-reopen-v18/RESULT.v1.json'
receipt = read(receipt_path)
assert receipt['status'] == 'four_native_lighting_components_without_postprocess_reopened'
pins = dict(receipt['input_pins'])
pins[str(receipt_path)] = sha(receipt_path)
native = []
for layer, directory in enumerate(tuple(f'townsmith-common-native-srgb-layer-{i}-v91' for i in range(4))):
    root = W / directory
    run = read(root / 'RUN-RESULT.v1.json')
    assert run['actual_exit_code'] == 0 and run['gpu_draw_executed']
    assert sha(root / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
    assert sha(root / 'FORGE-PACKED.rg11b10') == run['forge_packed_image_sha256']
    for path in (root / 'RUN-RESULT.v1.json', root / 'NATIVE-RESULT.v1.json', root / 'FORGE-PACKED.rg11b10'):
        pins[str(path)] = sha(path)
    inp=read(root/'INPUT.v1.json')
    assert inp['diagnostic_layer']==layer and inp['glb_sha256']=='38b11ac971d46552b9cd00141246c4c644191f0d5239a42203f7d099373df865'
    assert sha(root/'MATERIAL-CURVE.r32f') == sha(W/'townsmith-common-native-srgb-layer-0-v91/MATERIAL-CURVE.r32f')
    prepared=read(root/'PREPARED.v1.json')
    pins.update(prepared['input_pins'])
    native.append(packed_pixels(root / 'FORGE-PACKED.rg11b10'))
for path, expected in pins.items():
    assert sha(path) == expected, path
components = [pixels(SRC / ('door-debug-' + mode + '-nopost-linear.rgba16f'), True)
              for mode in ('DirectDiffuseLighting', 'DirectSpecularLighting', 'IndirectDiffuseLighting', 'ReflectionLighting')]
full = pixels(SRC / 'door-nopost-linear.rgba16f', True)
depth=W/'townsmith-door-source-material-values-v41/door-nopost-linear.depth-3.r32f'
prior=read(W/'townsmith-common-native-srgb-comparison-v91/RESULT.v1.json')
assert sha(depth)==prior['input_pins'][str(depth)]
pins[str(depth)]=sha(depth)
roi=(np.fromfile(depth,dtype='<f4').reshape(512,512)>0)[::-1]
assert int(roi.sum()) == 34445
comparisons = [
    ('direct diffuse + specular', components[0] + components[1], native[1]),
    ('indirect diffuse', components[2], native[2]),
    ('reflection', components[3], native[3]),
    ('full without postprocessing', full, native[0]),
    ('source component reconstruction', full, sum(components)),
]
rows = []
for name, source, target in comparisons:
    delta = np.abs(source - target)[roi]
    rows.append(dict(component=name, source_mean=float(source[roi].mean()),
                     target_mean=float(target[roi].mean()), mean_rgb_error=float(delta.mean()),
                     max_rgb_error=float(delta.max()), pixels_over_001=int((delta.max(axis=1) > .001).sum())))
OUT.mkdir()
panel = Image.new('RGB', (1024, len(comparisons) * 280), '#202020')
draw = ImageDraw.Draw(panel)
for row, (name, source, target) in enumerate(comparisons):
    for column, value in enumerate((source, target)):
        rgb = np.maximum(value, 0)
        rgb = rgb / (1 + rgb)
        rgb = np.where(rgb <= .0031308, rgb * 12.92, 1.055 * rgb ** (1 / 2.4) - .055)
        image = Image.fromarray(np.round(np.clip(rgb, 0, 1) * 255).astype('uint8')).resize((256, 256))
        draw.text((column * 512 + 8, row * 280 + 4), name + (' / source' if column == 0 else ' / target'), fill='white')
        panel.paste(image, (column * 512, row * 280 + 24))
panel.save(OUT / 'components.png')
pins[str(Path(__file__))] = sha(Path(__file__))
pins[str(OUT / 'components.png')] = sha(OUT / 'components.png')
now = datetime.now(timezone.utc).isoformat()
result = dict(status='unfitted_lighting_components_compared_fidelity_open', finished_utc=now,
              lot='L03.TOWNSMITH.MATERIAUX.DIFFUS.INDIRECT.DIAGNOSTIC', input_pins=pins,
              rehashed_pins=len(pins) - 2, rows=rows, fixed_roi_pixels=int(roi.sum()),
              fixed_roi_is_geometry_evidence=True, threshold=.001, pixel_fit_or_threshold_relaxation=False,
              source_debug_component_sum_is_exact_scene_reconstruction=rows[-1]['max_rgb_error'] == 0,
              native_components_drawn_separately=True, native_target_format='Rg11b10Ufloat', installed_in_product=False, source_pixels_applied_to_native=False,
              diagnostic_display_clamps_negative_values=True, metrics_use_unmodified_linear_values=True,
              full_material_fidelity_qualified=False, source_render_parity_qualified=False,
              forge_editor_verified=False, full_import=False, complete_assets_delivered_increment=0,
              next_action='Use component diagnostics to locate the residual; component-sum discrepancy forbids assigning the full error exclusively to a debug component.')
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
print(json.dumps(dict(status=result['status'], rows=rows, rehashed_pins=result['rehashed_pins'])))
