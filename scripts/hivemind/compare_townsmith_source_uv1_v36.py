"""Compare source normal-buffer codes to actual native material output."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw

W = Path('F:/OpenRPG_ImportWork')
DRAW = W / 'townsmith-native-source-uv1-v36'
OUT = W / 'townsmith-source-uv1-comparison-v36'
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
carrier_path = W / 'townsmith-source-uv1-carrier-v36/RESULT.v1.json'
pins.update(read(carrier_path)['input_pins'])
depth_path = W / 'townsmith-door-depth-coverage-reopen-v22/RESULT.v1.json'
assert read(depth_path)['source_depth_mask_vs_native_fragment_coverage_byte_equal']
source_path = W / 'townsmith-door-source-fine-uv-v28/door-nopost-linear._GBufferTexture1.rgba32f'
coverage_path = W / 'townsmith-paint-door-native-diffuse-material-draw-v3/FORGE-LINEAR.rgba16f'
source_work=W/'townsmith-door-source-fine-uv-v28'
source_run=read(source_work/'RUN-RESULT.v1.json')
assert source_run['actual_exit_code']==0 and source_run['source_inputs_unchanged'] and source_run['neutral_reference_byte_equal']
pins.update(read(source_work/'PREPARED.v1.json')['input_pins'])
for path in [source_work/'RUN-RESULT.v1.json',source_work/'NATIVE-RESULT.v1.json',W/'townsmith-fine-uv-comparison-v28/RESULT.v1.json']:
    pins[str(path)]=sha(path)
# Original ForceEnable GBuffer normal/roughness observation unchanged by UV instrumentation.
assert sha(source_path)==sha(W/'townsmith-door-source-anisotropy-controls-v23/door-nopost-linear._GBufferTexture1.rgba32f')

for path in (carrier_path, depth_path, source_path, coverage_path, DRAW / 'RUN-RESULT.v1.json', DRAW / 'NATIVE-RESULT.v1.json', DRAW / 'FORGE-LINEAR.rgba16f'):
    pins[str(path)] = sha(path)
camera_proof=W/'townsmith-door-source-inpass-normal-reopen-v31/RESULT.v1.json'
camera_pixels=W/'townsmith-door-source-inpass-normal-v31/door-nopost-linear.inpass-normal-3.rgba32f'
assert sha(source_path)==sha(camera_pixels)
pins.update(read(camera_proof)['input_pins'])
pins[str(camera_proof)]=sha(camera_proof)
pins[str(camera_pixels)]=sha(camera_pixels)
for path, expected in pins.items():
    assert sha(Path(path)) == expected, path
native = np.fromfile(DRAW / 'FORGE-LINEAR.rgba16f', dtype='<f2').reshape(512, 512, 4).astype(np.float32)
source = np.flipud(np.fromfile(source_path, dtype='<f4').reshape(512, 640, 4)[:, :512])
coverage = np.fromfile(coverage_path, dtype='<f2').reshape(512, 512, 4)[:, :, 3] != 0
assert int(coverage.sum()) == 34445 and np.isfinite(native).all()
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
result = dict(diagnostic_sampling='Original implicit sampling with preserved authored UV1 for RGBMask',status='authored_uv1_material_control_compared_fidelity_open', finished_utc=now,
              lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.MATERIAUX.NORMALES', input_pins=pins,
              rehashed_pins=len(pins)-2, native_gpu_draw_executed=True, fixed_covered_pixels=int(coverage.sum()),
              half_transport_integer_codes_unambiguously_recovered=True,
              numpy_half_roundtrip_matches_gpu=bool(host_half_roundtrip_equal),
              maximum_transport_error_in_code_units=float(code_transport_error.max()),
              source_world_normal_mean_angle_degrees=float(angles.mean()),
              source_world_normal_max_angle_degrees=float(angles.max()), normal_code_equal_pixels=int(normal_codes_equal.sum()),
              roughness_code_equal_pixels=int((roughness_error == 0).sum()), mean_roughness_code_error=float(roughness_error.mean()),
              max_roughness_code_error=int(roughness_error.max()), shader_control_bypasses_lighting=True,
              native_receipt_ambient_and_reflection_flags_describe_installed_resources_only_for_this_control=True,
              source_normal_buffer_inpass_binding_qualified=True, source_global_buffer_lifetime_qualified=False, pixel_fit_or_threshold_relaxation=False,
              full_material_fidelity_qualified=False, source_render_parity_qualified=False,
              forge_editor_verified=False, full_import=False, complete_assets_delivered_increment=0,
              next_action='Recompare corrected full material lighting and graph intermediates before prefab and Studio qualification.')
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
summary = f'Normales/rugosité source et Forge comparées sur 34 445 pixels : angle moyen {float(angles.mean()):.6f}°, maximal {float(angles.max()):.6f}° ; {int((roughness_error == 0).sum())} codes de rugosité identiques. Correction du canal UV1 du masque uniquement ; fidélité originale encore ouverte.'
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **MATERIAUX.UV1 : contrôle causal comparé**. {summary} [Preuve](../{OUT.name}/RESULT.v1.json). Aucun nouvel asset complet livré.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = read(path)
    data.update(updated_utc=now, last_technical_batch=result['lot'], last_technical_proof=str(OUT / 'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.MATERIAUX.UV1', current_work_state=summary,
                next_action=result['next_action'], full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
print(json.dumps({key: value for key, value in result.items() if key != 'input_pins'}))
