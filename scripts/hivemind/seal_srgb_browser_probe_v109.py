"""Reopen the real browser receipt without promoting material or Studio proof."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import math
import struct

W = Path('F:/OpenRPG_ImportWork')
D = W / 'srgb-browser-producer-v108'
O = W / 'srgb-browser-producer-reopen-v109'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


assert not O.exists()
run = read(D / 'RUN-RESULT.v1.json')
receipt = read(D / 'BROWSER-RESULT.v1.json')
assert run['status'] == 'actual_common_rust_producer_browser_gpu_probe_passed'
assert run['build_exit_code'] == run['bindgen_exit_code'] == 0
assert run['browser_gpu_executed'] and run['source_inputs_unchanged']
assert sha(D / 'BROWSER-RESULT.v1.json') == run['browser_receipt_sha256']
assert sha(D / 'BROWSER-CURVE.r32f') == run['browser_curve_sha256']
assert receipt['status'] == 'actual_common_rust_producer_calibrated_on_browser_webgpu'
assert receipt['backend'] == 'BrowserWebGpu' and receipt['gpu_errors'] == []
assert receipt['pending_frame_polls'] > 0 and receipt['page_animation_frames'] > 0
assert receipt['cloned_device_identity_equal'] and receipt['second_device_identity_differs']
assert receipt['source_asset_inputs'] == receipt['calibration_file_inputs'] == 0
assert not receipt['studio_material_drawn'] and not receipt['full_import']
raw = (D / 'BROWSER-CURVE.r32f').read_bytes()
assert raw == bytes(receipt['curve_bytes']) and len(raw) == 1024
floats = struct.unpack('<256f', raw)
assert floats[0] == 0.0
assert all(math.isfinite(x) and 0.0 < x <= 1.0 for x in floats[1:])
assert all(left < right for left, right in zip(floats, floats[1:]))
pins = dict(read(D / 'PREPARED.v1.json')['input_pins'])
for path, expected in pins.items():
    assert sha(Path(path)) == expected, path
for path in [Path(__file__), D / 'PREPARED.v1.json', D / 'RUN-RESULT.v1.json',
             D / 'BROWSER-RESULT.v1.json', D / 'BROWSER-CURVE.r32f', D / 'Cargo.lock',
             *sorted((D / 'site/pkg').glob('*'))]:
    if path.is_file():
        pins[str(path)] = sha(path)
assert sha(D / 'site/pkg/openrpg_srgb_browser_probe_bg.wasm') == run['linked_wasm_sha256']
O.mkdir()
result = dict(
    status='common_async_producer_browser_gpu_and_reopened_curve_verified',
    finished_utc=datetime.now(timezone.utc).isoformat(),
    lot='L01.SRGB.WEB.ASYNC.GPU.NAVIGATEUR', browser_gpu_executed=True,
    backend=receipt['backend'], reported_adapter=receipt['adapter'],
    reported_driver=receipt['driver'], hardware_adapter_identity_independently_verified=False,
    curve_bytes=1024, ordered_float32_thresholds=255,
    adjacent_witnesses_validated_by_actual_common_producer=True,
    pending_frame_polls=receipt['pending_frame_polls'],
    page_animation_frames=receipt['page_animation_frames'],
    elapsed_ms=receipt['elapsed_ms'], longest_poll_ms=receipt['longest_poll_ms'],
    gpu_errors=[], actual_device_identity_comparison_verified=True,
    runtime_device_invalidation_exercised=False, browser_material_draw_verified=False,
    editor_visual_verified=False, full_material_fidelity_qualified=False,
    native_browser_table_equality_required=False,
    calibration_file_inputs=0, purchased_asset_inputs=0,
    full_import=False, new_fully_qualified_assets=0, input_pins=pins,
)
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps({key: value for key, value in result.items() if key != 'input_pins'}))
