"""Build the staged native transport, run actual GPU sampling, independently reopen it."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import shutil
import struct
import subprocess
import sys
import math

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-rgb8-native-transport-candidate-v3'
TARGET = W / 'source-graph-private-boundary-v1/target-rust198-explicit-domain-v8'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def save(name, value, new=False):
    with (ROOT / name).open('x' if new else 'w', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False)


def check(pins):
    for path, digest in pins.items():
        assert sha(path) == digest, 'Changed input: ' + path


def reopen():
    inputs = read(ROOT / 'INPUT.v1.json')
    check(inputs['source_proof_pins'])
    result = read(ROOT / 'gpu/RESULT.v1.json')
    assert result['input_sha256'] == sha(ROOT / 'INPUT.v1.json')
    assert result['gpu_sampled_textures'] == 3 and result['sampled_mip_levels'] == 37
    reference = W / 'unity-delta-foliage-rgb24-reference-v1'
    proof = read(reference / 'GPU-REFERENCE-REOPEN.v1.json')
    native = read(reference / 'export/RESULT.json')
    assert proof['source_reference_result_sha256'] == sha(reference / 'export/RESULT.json')
    proof_rows = {r['asset_id']: r for r in proof['rows']}
    target = {r['asset_id']: r for r in result['rows']}
    expected = {r['asset_id']: r for r in inputs['rows']}
    assert len(target) == len(expected) == len(native['cases']) == 3
    pins = {str(ROOT / 'INPUT.v1.json'): sha(ROOT / 'INPUT.v1.json'),
            str(ROOT / 'gpu/RESULT.v1.json'): sha(ROOT / 'gpu/RESULT.v1.json'),
            str(reference / 'export/RESULT.json'): sha(reference / 'export/RESULT.json'),
            str(reference / 'GPU-REFERENCE-REOPEN.v1.json'): sha(reference / 'GPU-REFERENCE-REOPEN.v1.json')}
    rows, levels = [], 0
    for ordinal, case in enumerate(native['cases']):
        asset = case['asset_id']
        output, row = target[asset], expected[asset]
        assert row['wire_descriptor']['format'] == 'rgb8_unorm'
        assert case['graphics_format'] == 'R8G8B8_UNorm'
        assert output['unity_guid'] == case['actual_guid']
        assert str(output['unity_file_id']) == str(case['actual_file_id'])
        assert output['storage_sha256'] == row['original_storage_sha256'] == sha(row['storage'])
        assert len(output['mips']) == case['mip_count'] == len(row['mips'])
        maximum = 0.0
        for level, mip in enumerate(output['mips']):
            source = reference / 'export' / f'{ordinal:03}' / f'mip-{level}-rgba.f32'
            sampled = Path(mip['rgba_f32'])
            assert mip['level'] == level and mip['grid'] == case['grid'] == 64
            assert sha(source) == proof_rows[asset]['raw_rgba_and_both_decode_buffers_sha256'][str(source)]
            assert sha(sampled) == mip['sha256']
            raw_a, raw_b = source.read_bytes(), sampled.read_bytes()
            assert len(raw_a) == len(raw_b) == 64*64*16
            a, b = struct.unpack('<16384f', raw_a), struct.unpack('<16384f', raw_b)
            assert all(math.isfinite(v) for v in a + b)
            error = max(abs(x-y) for x, y in zip(a, b))
            assert error <= 1e-6, f'RGB8 native transport differs: {asset}, mip {level}, {error}'
            maximum = max(maximum, error)
            pins[str(source)], pins[str(sampled)] = sha(source), sha(sampled)
            levels += 1
        rows.append(dict(asset_id=asset, mips=len(output['mips']), max_rgba_error=maximum,
                         original_rgb24_native_storage_sha256=sha(row['storage'])))
    assert levels == 37
    check(pins)
    check(inputs['source_proof_pins'])
    save('INDEPENDENT-GPU-REOPEN.v1.json', dict(native_rgb24_textures_verified=3, mips_verified=37,
         sampled_coordinates=37*64*64, coordinate_mapping='identity', fixed_rgba_tolerance=1e-6,
         wire_format='rgb8_unorm', actual_engine_candidate_expansion_used=True,
         every_original_rgb_byte_and_opaque_alpha_checked_by_native_probe=True,
         corruption_refusals_per_texture=6, rows=rows, input_pins=pins,
         canonical_renderer_installed=False, shadergraph_render_qualified=False,
         studio_verified=False, full_import=False), new=True)


def run():
    state = dict(status='building_native_rgb8_transport_candidate', native_rgb24_textures_verified=0,
                 canonical_renderer_installed=False, studio_verified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        check(prepared['input_pins'])
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), CARGO_TARGET_DIR=str(TARGET), CARGO_INCREMENTAL='0')
        with (ROOT / 'build.log').open('xb') as log:
            state['build_exit_code'] = subprocess.run(
                ['C:/Users/karen/.cargo/bin/cargo.exe', '+1.98.0', 'build', '--locked', '--offline', '-j1'],
                cwd=ROOT, env=env, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW).returncode
        assert state['build_exit_code'] == 0, 'Native candidate compilation refused; preserve log'
        check(prepared['input_pins'])
        executable = ROOT / 'rgb8-transport-probe.immutable.v1.exe'
        shutil.copyfile(TARGET / 'debug/openrpg-delta-rgb8-native-transport-candidate-v3.exe', executable)
        state.update(status='sampling_three_native_rgb24_textures', executable_sha256=sha(executable))
        save('RUN-RESULT.v1.json', state)
        with (ROOT / 'execution.stdout.log').open('xb') as out, (ROOT / 'execution.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(executable), str(ROOT / 'INPUT.v1.json'), str(ROOT / 'gpu')],
                cwd=ROOT, env=env, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state['gpu_pid'] = child.pid
            save('RUN-RESULT.v1.json', state)
            state['execution_exit_code'] = child.wait()
        assert state['execution_exit_code'] == 0, 'GPU or transport integrity refused'
        reopen()
        check(prepared['input_pins'])
        state.update(status='three_native_rgb24_transport_candidates_gpu_verified',
                     native_rgb24_textures_verified=3, mips_verified=37,
                     independent_receipt_sha256=sha(ROOT / 'INDEPENDENT-GPU-REOPEN.v1.json'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


def launch():
    assert not (ROOT / 'JOB.v1.json').exists() and not (ROOT / 'build.log').exists()
    check(read(ROOT / 'PREPARED.v1.json')['input_pins'])
    save('CONTROLLER-PIN.v1.json', dict(path=str(Path(__file__).resolve()), sha256=sha(Path(__file__))), new=True)
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid, created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()), new=True)
    print(json.dumps(dict(controller_pid=child.pid, native_rgb24_textures=3, mips=37, canonical_renderer_installed=False)))


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else launch()
