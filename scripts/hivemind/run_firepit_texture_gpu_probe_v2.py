"""Run the staged real RA texture on DX12, retaining a pinned frozen core.

This qualifies texture transport/readback only, not Unity sampling parity or
the legacy shader, particle runtime, Studio, or a delivered full prefab.
"""
from pathlib import Path
import datetime
import hashlib
import json
import math
import os
import shutil
import struct
import subprocess
import sys
import time

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'firepit-active-texture-gpu-run-v2'
STAGED = W / 'firepit-active-texture-gpu-probe-v1'
HOST = W / 'delta-rgb8-frozen-canonical-loader-v7'
MASK = W / 'source-graph-color-write-mask-native-check-v2/RUN-RESULT.v1.json'
CORE = W / 'delta-rgb8-frozen-canonical-loader-v6/snapshot/crates/forge_core'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TARGET = Path('F:/OpenRPG_Forge/relocated-work/Rust-target')
NAME = 'openrpg-firepit-active-texture-gpu-run-v2'

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def check(pins):
    for name, digest in pins.items():
        assert sha(Path(name)) == digest, 'Changed pinned input: ' + name

def save(name, value):
    p = ROOT / (name + '.tmp')
    p.write_text(json.dumps(value, indent=2), 'utf-8')
    p.replace(ROOT / name)

def samples(data):
    assert len(data) == 64 * 64 * 16, 'Wrong readback size'
    values = struct.unpack('<16384f', data)
    assert all(math.isfinite(v) and -0.000001 <= v <= 1.000001 for v in values), 'Invalid normalized readback'
    return values

def prepare():
    assert not ROOT.exists(), 'Preserve earlier run'
    staged = read(STAGED / 'PREPARED.v1.json')
    pins = dict(staged['input_pins']); pins.update(staged['output_pins'])
    host = read(HOST / 'RUN-RESULT.v1.json')
    assert host['actual_exit_code'] == 0 and host['actual_host_tests_passed'] == 17
    pins.update(read(HOST / 'PREPARED.v1.json')['input_pins'])
    for p in [STAGED / 'PREPARED.v1.json', HOST / 'RUN-RESULT.v1.json', Path(__file__).resolve(), BIN / 'cargo.exe', BIN / 'rustc.exe']:
        pins[str(p)] = sha(p)
    check(pins)
    ROOT.mkdir(); (ROOT / 'src').mkdir(); (ROOT / 'temp').mkdir()
    for p in (STAGED / 'src').iterdir():
        shutil.copyfile(p, ROOT / 'src' / p.name)
    manifest = (STAGED / 'Cargo.toml').read_text('utf-8')
    old_core = 'E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_core'
    assert manifest.count(old_core) == 1
    manifest = manifest.replace(old_core, CORE.as_posix()).replace('openrpg-firepit-active-texture-gpu-probe-v1', NAME)
    (ROOT / 'Cargo.toml').write_text(manifest, 'utf-8')
    lock = (STAGED / 'Cargo.lock').read_text('utf-8')
    assert lock.count('name = "openrpg-firepit-active-texture-gpu-probe-v1"') == 1
    (ROOT / 'Cargo.lock').write_text(lock.replace('name = "openrpg-firepit-active-texture-gpu-probe-v1"', 'name = "' + NAME + '"'), 'utf-8')
    input_data = read(STAGED / 'INPUT.v1.json')
    input_data['source_proof_pins'] = pins.copy()
    (ROOT / 'INPUT.v1.json').write_text(json.dumps(input_data, indent=2), 'utf-8')
    for p in list((ROOT / 'src').iterdir()) + [ROOT / 'Cargo.toml', ROOT / 'Cargo.lock', ROOT / 'INPUT.v1.json']:
        pins[str(p)] = sha(p)
    check(pins)
    save('PREPARED.v1.json', dict(input_pins=pins, expected_textures=1, expected_mips=11,
        frozen_core=str(CORE), historical_texture_sampler_modules=True, current_full_renderer_qualified=False,
        target=str(TARGET), full_import=False))
    with (ROOT / 'controller.log').open('xb') as out:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdout=out, stderr=out, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid))
    print(json.dumps(dict(controller_pid=child.pid, expected_textures=1, expected_mips=11, full_import=False)))

def run():
    state = dict(status='waiting_for_owned_mask_build_to_release_shared_target', textures_verified=0,
        mips_verified=0, unity_sampling_parity_qualified=False, source_shader_gpu_qualified=False,
        renderer_admitted=False, studio_verified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        # No second writer in the shared target while the existing build runs.
        while not read(MASK).get('finished_utc'):
            time.sleep(15)
        state['preceding_mask_terminal_sha256'] = sha(MASK)
        pins = read(ROOT / 'PREPARED.v1.json')['input_pins']; check(pins)
        env = os.environ.copy()
        env.update(RUSTC=str(BIN / 'rustc.exe'), TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'),
                   CARGO_TARGET_DIR=str(TARGET), CARGO_BUILD_JOBS='1', PATH=str(BIN) + os.pathsep + env.get('PATH', ''))
        state['status'] = 'compiling_real_ra_texture_dx12_probe'; save('RUN-RESULT.v1.json', state)
        with (ROOT / 'build.stdout.log').open('xb') as out, (ROOT / 'build.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(BIN / 'cargo.exe'), 'build', '--locked', '--offline', '-j', '1'],
                cwd=ROOT, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid'] = child.pid; save('RUN-RESULT.v1.json', state)
            state['actual_build_exit_code'] = child.wait()
        assert state['actual_build_exit_code'] == 0, 'Actual build refused; retain logs'
        check(pins)
        exe = TARGET / 'debug' / (NAME + '.exe')
        state['executable_sha256'] = sha(exe)
        state['status'] = 'sampling_real_ra_texture_on_gpu'; save('RUN-RESULT.v1.json', state)
        output = ROOT / 'gpu-output'
        with (ROOT / 'gpu.stdout.log').open('xb') as out, (ROOT / 'gpu.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(exe), str(ROOT / 'INPUT.v1.json'), str(output)],
                cwd=ROOT, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state['gpu_pid'] = child.pid; save('RUN-RESULT.v1.json', state)
            state['actual_gpu_exit_code'] = child.wait()
        assert state['actual_gpu_exit_code'] == 0, 'Actual GPU execution refused'
        check(pins)
        report = read(output / 'RESULT.v1.json'); original = read(ROOT / 'INPUT.v1.json')
        assert report['gpu_sampled_textures'] == 1 and report['sampled_mip_levels'] == 11
        assert report['backend'] == 'Dx12' and report['adapter_name']
        assert report['input_sha256'] == sha(ROOT / 'INPUT.v1.json')
        assert len(report['rows']) == len(original['rows']) == 1
        row = report['rows'][0]; source = original['rows'][0]
        for k in ['asset_id', 'unity_guid', 'unity_file_id', 'gpu_format', 'storage_sha256']:
            assert row[k] == source[k], 'Wrong texture identity: ' + k
        assert len(row['mips']) == 11
        output_pins = {str(output / 'RESULT.v1.json'): sha(output / 'RESULT.v1.json')}
        for level, mip in enumerate(row['mips']):
            assert mip['level'] == level and mip['grid'] == 64
            p = Path(mip['rgba_f32'])
            assert p.resolve().is_relative_to(output.resolve())
            assert sha(p) == mip['sha256']; samples(p.read_bytes()); output_pins[str(p)] = sha(p)
        for label, data in [('nonfinite_sample', struct.pack('<f', float('nan')) + bytes(65532)), ('truncated_readback', bytes(65535))]:
            try:
                samples(data)
            except AssertionError:
                continue
            raise AssertionError('Negative control accepted: ' + label)
        check(pins); check(output_pins)
        save('INDEPENDENT-REOPEN.v1.json', dict(input_pins=pins, output_pins=output_pins,
            textures_verified=1, mips_verified=11, sampled_texels=45056,
            negative_controls=['nonfinite_sample', 'truncated_readback'], actual_adapter=report['adapter_name'],
            backend=report['backend'], source_unity_sampling_parity_qualified=False, full_import=False))
        state.update(status='one_real_ra_texture_eleven_gpu_mips_reopened', textures_verified=1,
            mips_verified=11, sampled_texels=45056, input_pins_unchanged=True,
            independent_receipt_sha256=sha(ROOT / 'INDEPENDENT-REOPEN.v1.json'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error),
                     diagnostic_traceback=traceback.format_exc())
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)

if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
