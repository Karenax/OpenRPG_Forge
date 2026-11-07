"""Compare source normal-buffer codes to actual native material output."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw

W = Path('F:/OpenRPG_ImportWork')
DRAW = W / 'townsmith-quad-normal-buffer-draw-v71'
OUT = W / 'townsmith-quad-normal-buffer-comparison-v71'
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
carrier_path = W / 'townsmith-quad-normal-buffer-carrier-v71/RESULT.v1.json'
pins.update(read(carrier_path)['input_pins'])
depth_path = W / 'townsmith-door-depth-coverage-reopen-v22/RESULT.v1.json'
assert read(depth_path)['source_depth_mask_vs_native_fragment_coverage_byte_equal']
source_path = W / 'townsmith-door-source-inpass-normal-v31/door-nopost-linear.inpass-normal-3.rgba32f'
source_binding_proof=W/'townsmith-door-source-inpass-normal-reopen-v31/RESULT.v1.json'
source_binding=read(source_binding_proof);assert source_binding['actual_camera_normal_binding_qualified']
assert sha(source_path)==source_binding['input_pins'][str(source_path)]
repeat_path=source_path.with_name('door-nopost-linear.inpass-normal-2.rgba32f')
assert sha(repeat_path)==source_binding['input_pins'][str(repeat_path)]==sha(source_path)
pins[str(repeat_path)]=sha(repeat_path)
meta_path=source_path.with_suffix('.json');meta=read(meta_path)
assert sha(meta_path)==source_binding['input_pins'][str(meta_path)]
assert meta['actual_camera_normal_buffer'] and meta['injection']=='BeforePostProcess'
assert (meta['width'],meta['height'],meta['viewportWidth'],meta['viewportHeight'])==(640,512,512,512)
pins[str(meta_path)]=sha(meta_path)
pins[str(source_binding_proof)]=sha(source_binding_proof)

coverage_path = W / 'townsmith-paint-door-native-diffuse-material-draw-v3/FORGE-LINEAR.rgba16f'
for path in (carrier_path, depth_path, source_path, coverage_path, DRAW / 'RUN-RESULT.v1.json', DRAW / 'NATIVE-RESULT.v1.json', DRAW / 'FORGE-LINEAR.rgba16f'):
    pins[str(path)] = sha(path)
for path, expected in pins.items():
    assert sha(Path(path)) == expected, path
native = np.fromfile(DRAW / 'FORGE-LINEAR.rgba16f', dtype='<f2').reshape(512, 512, 4).astype(np.float32)
source = np.flipud(np.fromfile(source_path, dtype='<f4').reshape(512, 640, 4)[:, :512])
coverage = np.fromfile(coverage_path, dtype='<f2').reshape(512, 512, 4)[:, :, 3] != 0
assert int(coverage.sum()) == 34445 and np.isfinite(native).all()
current_depth=W/'townsmith-door-source-inpass-normal-v31/door-nopost-linear.depth-3.r32f'
assert sha(current_depth)==source_binding['input_pins'][str(current_depth)]
pins[str(current_depth)]=sha(current_depth)
source_depth_coverage=(np.fromfile(current_depth,dtype='<f4').reshape(512,512)>0)[::-1]
assert np.array_equal(coverage,source_depth_coverage)
assert np.all(source[:,:,3][coverage]>0)
assert np.array_equal(coverage,native[:,:,3]!=0), 'Current native normal-buffer coverage differs'

source_codes = (source*255.5).astype(np.uint32)
native_codes = np.round(native*255).astype(np.uint32)
assert np.all(native_codes <= 255)
# Native UNORM codes are transported through a float16 colour target. The GPU
# conversion need not use NumPy's rounding mode. A half ULP at [0,1] is at most
# 1/2048, or <0.125 of an 8-bit code, so integer recovery remains unambiguous.
host_half_roundtrip_equal = np.array_equal(native.astype('<f2'), (native_codes.astype(np.float32)/255).astype('<f2'))
code_transport_error = np.abs(native*255-native_codes.astype(np.float32))
assert float(code_transport_error.max()) <= 255/2048 + .0001


def decoded(codes):
    x = codes[:, :, 0] | ((codes[:, :, 2] & 15) << 8)
    y = codes[:, :, 1] | ((codes[:, :, 2] >> 4) << 8)
    f = np.stack((x, y), axis=-1).astype(np.float64)/4095*2-1
    n = np.concatenate((f, (1-np.abs(f).sum(axis=-1))[:, :, None]), axis=-1)
    t = np.maximum(-n[:, :, 2], 0)
    n[:, :, :2] += np.where(n[:, :, :2] >= 0, -t[:, :, None], t[:, :, None])
    return n/np.linalg.norm(n, axis=-1)[:, :, None]


sn, nn = decoded(source_codes), decoded(native_codes)
angles = np.degrees(np.arccos(np.clip(np.sum(sn*nn, axis=-1)[coverage], -1, 1)))
roughness_error = np.abs(source_codes[:, :, 3].astype(np.int32)-native_codes[:, :, 3].astype(np.int32))[coverage]
normal_codes_equal = np.all(source_codes[:, :, :3] == native_codes[:, :, :3], axis=-1)[coverage]
OUT.mkdir()
panel = Image.new('RGB', (1024, 1080), '#202020')
draw = ImageDraw.Draw(panel)
for row, (label, a, b) in enumerate((('Source-world normal', sn*.5+.5, nn*.5+.5), ('Perceptual roughness', np.repeat(source[:, :, 3:4], 3, axis=-1), np.repeat(native[:, :, 3:4], 3, axis=-1)))):
    for column, value in enumerate((a, b)):
        value = value.copy();value[~coverage] = 0
        panel.paste(Image.fromarray(np.round(np.clip(value, 0, 1)*255).astype('uint8')), (column*512, row*540+28))
        draw.text((column*512+8, row*540+5), label+(' / Unity' if column == 0 else ' / Forge'), fill='white')
panel.save(OUT / 'normal-roughness-controls.png')
pins[str(Path(__file__))] = sha(Path(__file__))
pins[str(OUT / 'normal-roughness-controls.png')] = sha(OUT / 'normal-roughness-controls.png')
now = datetime.now(timezone.utc).isoformat()
result = dict(status='native_source_normal_and_roughness_codes_compared_fidelity_open', finished_utc=now,
              lot='L03.TOWNSMITH.MATERIAUX.BUFFER.NORMAL.QUAD', input_pins=pins,
              rehashed_pins=len(pins)-2, native_gpu_draw_executed=True, current_native_alpha_coverage_vs_source_depth_byte_equal=True, installed_in_product=False, source_pixels_applied_to_native=False, fixed_covered_pixels=int(coverage.sum()),
              half_transport_integer_codes_unambiguously_recovered=True,
              numpy_half_roundtrip_matches_gpu=bool(host_half_roundtrip_equal),
              maximum_transport_error_in_code_units=float(code_transport_error.max()),
              source_world_normal_mean_angle_degrees=float(angles.mean()),
              source_world_normal_max_angle_degrees=float(angles.max()), normal_code_equal_pixels=int(normal_codes_equal.sum()),
              roughness_code_equal_pixels=int((roughness_error == 0).sum()), mean_roughness_code_error=float(roughness_error.mean()),
              max_roughness_code_error=int(roughness_error.max()), shader_control_bypasses_lighting=True,
              native_receipt_ambient_and_reflection_flags_describe_installed_resources_only_for_this_control=True,
              source_global_buffer_lifetime_qualified=False, actual_camera_normal_binding_qualified=True, pixel_fit_or_threshold_relaxation=False,
              full_material_fidelity_qualified=False, source_render_parity_qualified=False,
              forge_editor_verified=False, full_import=False, complete_assets_delivered_increment=0,
              next_action='Compare source/native implicit texture sampling and derivatives; capture native depth buffer values, preserving the exact coverage proof.')
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
print(json.dumps({key: value for key, value in result.items() if key != 'input_pins'}))
