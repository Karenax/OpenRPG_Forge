"""Reopen actual Unity samples; normalize documented enum representations only.

The capture uses enum names, while the original storage receipt uses integers.
Retain the first verifier and its outcome rather than modifying live pinned code.
"""
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
CAP = W / 'unity-delta-firepit-ra-sampling-v1'
OLD = W / 'unity-delta-firepit-active-texture-storage-v1'
ROOT = W / 'delta-firepit-native-sampling-reopen-v2'

def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def check(pins):
    for p, digest in pins.items(): assert sha(Path(p)) == digest, 'Changed input: ' + p
def save(name, value):
    p = ROOT / (name + '.tmp'); p.write_text(json.dumps(value, indent=2), 'utf-8'); p.replace(ROOT / name)

def run():
    state = dict(status='waiting_for_actual_native_capture', mips_verified=0, sampling_parity_qualified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        while not read(CAP / 'RUN-RESULT.v1.json').get('finished_utc'): time.sleep(15)
        previous = read(CAP / 'RUN-RESULT.v1.json')
        assert previous['unity_exit_code'] == 0, 'Capture itself failed'
        native = read(CAP / 'export/RESULT.json'); old = read(OLD / 'export/RESULT.json')['cases'][0]
        pins = dict(read(CAP / 'PREPARED.v1.json')['input_pins'])
        for p in [Path(__file__).resolve(), CAP / 'PREPARED.v1.json', CAP / 'RUN-RESULT.v1.json', CAP / 'export/RESULT.json', OLD / 'export/RESULT.json']:
            pins[str(p)] = sha(p)
        check(pins)
        assert native['unity_version'] == '6000.5.8f1' and native['color_space'] == 'Linear'
        assert native['graphics_device_type'] == 'Direct3D11' and native['graphics_device_name']
        assert native['unity_guid'] == old['actual_guid'] and native['unity_file_id'] == old['actual_file_id']
        for key in ['graphics_format', 'width', 'height', 'mip_count', 'anisotropy', 'mip_bias']: assert native[key] == old[key], key
        enums = dict(filter_mode={'Point': 0, 'Bilinear': 1, 'Trilinear': 2})
        for key in ['wrap_u', 'wrap_v', 'wrap_w']: enums[key] = {'Repeat': 0, 'Clamp': 1, 'Mirror': 2, 'MirrorOnce': 3}
        for key, table in enums.items(): assert table[native[key]] == old[key], 'Actual sampler differs: ' + key
        assert len(native['mips']) == native['mip_count'] == 11
        outputs = {}
        for level, mip in enumerate(native['mips']):
            assert mip['level'] == level and mip['grid'] == 64
            p = Path(mip['rgba_f32']); assert p.resolve().is_relative_to((CAP / 'export').resolve())
            data = p.read_bytes(); assert len(data) == 65536
            assert all(math.isfinite(v) and -0.000001 <= v <= 1.000001 for v in struct.unpack('<16384f', data))
            outputs[str(p)] = sha(p)
        check(pins); check(outputs)
        save('INDEPENDENT-REOPEN.v1.json', dict(input_pins=pins, output_pins=outputs,
            mips_verified=11, sampled_texels=45056, enum_representation_normalized=True,
            prior_verifier_status=previous['status'], prior_verifier_receipt_sha256=sha(CAP / 'RUN-RESULT.v1.json'),
            original_texture_and_importer_unchanged=True, sampling_parity_qualified=False, full_import=False))
        state.update(status='eleven_actual_native_sampling_mips_reopened', mips_verified=11, sampled_texels=45056,
            independent_receipt_sha256=sha(ROOT / 'INDEPENDENT-REOPEN.v1.json'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error), diagnostic_traceback=traceback.format_exc())
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat(); save('RUN-RESULT.v1.json', state)

def prepare():
    assert not ROOT.exists(); ROOT.mkdir()
    with (ROOT / 'controller.log').open('xb') as out:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdout=out, stderr=out, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid)); print(json.dumps(dict(controller_pid=child.pid)))

if __name__ == '__main__': run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
