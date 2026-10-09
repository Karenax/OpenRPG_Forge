"""Compare real Unity/Forge GPU texels; keep orientation hypotheses explicit."""
from pathlib import Path
import datetime
import hashlib
import json
import math
import struct
import subprocess
import sys
import time

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-firepit-ra-sampling-parity-v1'
UNITY = W / 'delta-firepit-native-sampling-reopen-v2'
CAPTURE = W / 'unity-delta-firepit-ra-sampling-v1'
GPU = W / 'firepit-active-texture-gpu-run-v2'
TOLERANCE = 0.00001

def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def check(pins):
    for p, digest in pins.items(): assert sha(Path(p)) == digest, 'Changed reference: ' + p
def save(name, value):
    p = ROOT / (name + '.tmp'); p.write_text(json.dumps(value, indent=2), 'utf-8'); p.replace(ROOT / name)
def values(p):
    data = p.read_bytes(); assert len(data) == 65536
    result = struct.unpack('<16384f', data); assert all(math.isfinite(v) for v in result)
    return result

def run():
    state = dict(status='waiting_for_actual_gpu_receipts', sampled_texels_verified=0,
        source_sampling_qualified=False, source_orientation_qualified=False,
        current_full_renderer_qualified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        while not all(read(root / 'RUN-RESULT.v1.json').get('finished_utc') for root in [UNITY, GPU]): time.sleep(15)
        native_state = read(UNITY / 'RUN-RESULT.v1.json'); gpu_state = read(GPU / 'RUN-RESULT.v1.json')
        assert native_state['mips_verified'] == gpu_state['mips_verified'] == 11, 'An actual capture is unqualified'
        pins = {str(Path(__file__).resolve()): sha(Path(__file__).resolve()), str(ROOT / 'PREDECLARED.v1.json'): sha(ROOT / 'PREDECLARED.v1.json')}
        for root, state_row in [(UNITY, native_state), (GPU, gpu_state)]:
            receipt = root / 'INDEPENDENT-REOPEN.v1.json'
            assert sha(receipt) == state_row['independent_receipt_sha256']
            proof = read(receipt); check(proof['input_pins']); check(proof['output_pins'])
            pins.update(proof['input_pins']); pins.update(proof['output_pins'])
            for p in [receipt, root / 'RUN-RESULT.v1.json']: pins[str(p)] = sha(p)
        native = read(CAPTURE / 'export/RESULT.json'); gpu = read(GPU / 'gpu-output/RESULT.v1.json')
        row = gpu['rows'][0]; source = read(GPU / 'INPUT.v1.json')['rows'][0]
        assert row['unity_guid'] == native['unity_guid'] and str(row['unity_file_id']) == str(native['unity_file_id'])
        assert row['gpu_format'] == 'Bc3RgbaUnormSrgb' and native['graphics_format'] == 'RGBA_DXT5_SRGB'
        assert native['mip_bias'] == source['resolved_sampler']['mip_bias'] == 0
        assert native['anisotropy'] == source['resolved_sampler']['anisotropy'] == 1
        errors = []; distinct_native_values = set(); distinct_gpu_values = set()
        for level, (umip, gmip) in enumerate(zip(native['mips'], row['mips'], strict=True)):
            assert umip['level'] == gmip['level'] == level
            u = values(Path(umip['rgba_f32'])); g = values(Path(gmip['rgba_f32']))
            distinct_native_values.update(u); distinct_gpu_values.update(g)
            direct = max(abs(a-b) for a,b in zip(u,g,strict=True))
            flipped = max(abs(u[(x+y*64)*4+c] - g[(x+(63-y)*64)*4+c]) for y in range(64) for x in range(64) for c in range(4))
            errors.append(dict(level=level, direct_maximum_error=direct, vertical_flip_maximum_error=flipped))
        assert len(distinct_native_values) > 1 and len(distinct_gpu_values) > 1, 'Flat uninformative GPU captures'
        direct_max = max(row['direct_maximum_error'] for row in errors)
        flipped_max = max(row['vertical_flip_maximum_error'] for row in errors)
        matching = [name for name,error in [('direct',direct_max),('vertical_flip',flipped_max)] if error <= TOLERANCE]
        # Deliberately do not flip or replace source samples to obtain a pass.
        check(pins)
        save('COMPARISON.v1.json', dict(input_pins=pins, mip_errors=errors,
            tolerance_predeclared=TOLERANCE, direct_maximum_error=direct_max,
            vertical_flip_maximum_error=flipped_max, matching_coordinate_hypotheses=matching,
            sampled_texels=45056, unique_coordinate_hypothesis=len(matching)==1,
            historical_texture_sampler_modules=True, source_shader_parity_qualified=False,
            full_prefab_qualified=False, current_full_renderer_qualified=False, full_import=False))
        assert 'direct' in matching, 'Direct source sampling differs; comparison retained, no automatic flip'
        state.update(status='eleven_native_and_forge_sampling_mips_compared', sampled_texels_verified=45056,
            source_sampling_qualified=True, source_orientation_qualified=matching==['direct'],
            direct_maximum_error=direct_max, matching_coordinate_hypotheses=matching,
            comparison_receipt_sha256=sha(ROOT / 'COMPARISON.v1.json'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error), diagnostic_traceback=traceback.format_exc())
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat(); save('RUN-RESULT.v1.json', state)

def prepare():
    assert not ROOT.exists(); ROOT.mkdir()
    save('PREDECLARED.v1.json', dict(tolerance=TOLERANCE, comparison='all 45056 texels / 180224 channels across eleven mips',
        coordinate_hypotheses=['direct','vertical_flip'], silent_flip_allowed=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as out:
        child = subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdout=out,stderr=out,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid)); print(json.dumps(dict(controller_pid=child.pid)))

if __name__ == '__main__': run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
