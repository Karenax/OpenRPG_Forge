"""Reconcile only Cargo's root-package rename; preserve and reuse the built probe."""
from pathlib import Path
import copy
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tomllib

W = Path('F:/OpenRPG_ImportWork')
PRIOR = W / 'delta-foliage-texture-gpu-v2'
ROOT = W / 'delta-foliage-texture-gpu-v3'
BASE = W / 'source-graph-imported-texture-gpu-probe-v2'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(name, value):
    with (ROOT / name).open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False)


def check(pins):
    assert all(sha(path) == digest for path, digest in pins.items()), 'Unreconciled input changed'


def prepare():
    run, prepared, inputs = [read(PRIOR / name) for name in ['RUN-RESULT.v1.json', 'PREPARED.v1.json', 'INPUT.v1.json']]
    assert run['status'] == 'refused_or_unverified' and run['build_exit_code'] == 0 and run.get('finished_utc')
    assert run['error'] == 'AssertionError: Changed input: ' + str(PRIOR / 'Cargo.lock')
    assert not (PRIOR / 'samples').exists() and not ROOT.exists()
    old_lock, new_lock = BASE / 'Cargo.lock', PRIOR / 'Cargo.lock'
    assert sha(old_lock) == prepared['input_pins'][str(new_lock)]
    expected = tomllib.loads(old_lock.read_text('utf-8'))
    actual = tomllib.loads(new_lock.read_text('utf-8'))
    renamed = [p for p in expected['package'] if p['name'] == 'openrpg-imported-texture-gpu-probe-v2']
    assert len(renamed) == 1
    renamed[0]['name'] = 'openrpg-delta-foliage-texture-gpu-v1'
    for document in [expected, actual]:
        document['package'].sort(key=lambda p: (p['name'], p['version'], p.get('source', '')))
    assert expected == actual, 'Dependency graph changed beyond the root package name'
    pins = dict(prepared['input_pins'])
    pins[str(new_lock)] = sha(new_lock)
    check(pins)
    executable = Path(prepared['target']) / 'debug/openrpg-delta-foliage-texture-gpu-v1.exe'
    birth = datetime.datetime.fromisoformat(read(PRIOR / 'JOB.v1.json')['created_utc']).timestamp()
    assert executable.stat().st_mtime >= birth and executable.stat().st_size > 1000000
    assert 'Finished' in (PRIOR / 'build.log').read_text('utf-8-sig')
    ROOT.mkdir()
    save('LOCK-RECONCILIATION.v1.json', dict(original_lock_sha256=sha(old_lock), current_lock_sha256=sha(new_lock),
        only_root_package_renamed=True, all_dependency_versions_sources_checksums_unchanged=True,
        previous_terminal_receipt_sha256=sha(PRIOR / 'RUN-RESULT.v1.json'),
        actual_previous_build_exit_code=0, build_log_sha256=sha(PRIOR / 'build.log'),
        gpu_sampling_previously_executed=False, expanded_rgb24_files_reused=True))
    for path in [Path(__file__).resolve(), PRIOR / 'RUN-RESULT.v1.json', PRIOR / 'build.log',
                 PRIOR / 'PREPARED.v1.json', PRIOR / 'INPUT.v1.json', executable, ROOT / 'LOCK-RECONCILIATION.v1.json']:
        pins[str(path)] = sha(path)
    shutil.copyfile(executable, ROOT / 'gpu-texture-probe.immutable.v1.exe')
    shutil.copyfile(PRIOR / 'verify_gpu.py', ROOT / 'verify_gpu.py')
    pins[str(ROOT / 'verify_gpu.py')] = sha(ROOT / 'verify_gpu.py')
    pins[str(ROOT / 'gpu-texture-probe.immutable.v1.exe')] = sha(ROOT / 'gpu-texture-probe.immutable.v1.exe')
    inputs = copy.deepcopy(inputs)
    inputs['source_proof_pins'] = pins
    check(pins)
    save('INPUT.v1.json', inputs)
    pins[str(ROOT / 'INPUT.v1.json')] = sha(ROOT / 'INPUT.v1.json')
    save('PREPARED.v1.json', dict(input_pins=pins, expected_textures=18, expected_mips=225,
        previous_build_reused=True, rgb24_derivatives_reused=True, no_capture_repeated=True))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid, created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(controller_pid=child.pid, expected_textures=18, expected_mips=225, compiled_binary_reused=True)))


def run():
    state = dict(status='sampling_eighteen_textures_on_native_gpu', gpu_textures_verified=0,
        previous_build_reused=True, source_unity_sampling_parity_qualified=False,
        normal_decode_qualified=False, studio_verified=False, full_import=False)
    try:
        pins = read(ROOT / 'PREPARED.v1.json')['input_pins']
        check(pins)
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT))
        executable = ROOT / 'gpu-texture-probe.immutable.v1.exe'
        state['executable_sha256'] = sha(executable)
        (ROOT / 'RUN-RESULT.v1.json').write_text(json.dumps(state, indent=2), encoding='utf-8')
        with (ROOT / 'execution.stdout.log').open('xb') as out, (ROOT / 'execution.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(executable), str(ROOT / 'INPUT.v1.json'), str(ROOT / 'samples')],
                cwd=ROOT, env=env, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state['gpu_process_pid'] = child.pid
            (ROOT / 'RUN-RESULT.v1.json').write_text(json.dumps(state, indent=2), encoding='utf-8')
            state['execution_exit_code'] = child.wait()
        assert state['execution_exit_code'] == 0, 'GPU sampling refused; retain partial readbacks'
        with (ROOT / 'reopening.log').open('xb') as log:
            result = subprocess.run([sys.executable, '-B', str(ROOT / 'verify_gpu.py')], cwd=ROOT,
                env=env, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
        state['verifier_exit_code'] = result.returncode
        assert result.returncode == 0
        check(pins)
        report = read(ROOT / 'GPU-REOPEN.v1.json')
        assert report['verified_gpu_textures'] == 18 and report['verified_mip_levels'] == 225
        state.update(status='eighteen_textures_and_225_mips_gpu_sampled_and_reopened', gpu_textures_verified=18,
            gpu_mips_verified=225, adapter_name=report['adapter_name'], backend=report['backend'],
            gpu_reopen_sha256=sha(ROOT / 'GPU-REOPEN.v1.json'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    temp = ROOT / 'RUN-RESULT.tmp'
    temp.write_text(json.dumps(state, indent=2), encoding='utf-8')
    temp.replace(ROOT / 'RUN-RESULT.v1.json')


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
