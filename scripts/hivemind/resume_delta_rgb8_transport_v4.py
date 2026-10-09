"""Reuse the successfully built candidate under its actual Cargo package name."""
from pathlib import Path
import datetime
import importlib.util
import json
import os
import shutil
import subprocess
import sys
import time
import tomllib

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('rgb8_check', HERE / 'check_delta_rgb8_transport_v3.py')
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)
OLD = probe.ROOT
ROOT = OLD.parent / 'delta-rgb8-native-transport-candidate-v4'
probe.ROOT = ROOT


def run():
    state = dict(status='waiting_for_original_candidate_build', native_rgb24_textures_verified=0,
                 canonical_renderer_installed=False, studio_verified=False, full_import=False)
    probe.save('RUN-RESULT.v1.json', state)
    try:
        while True:
            previous = probe.read(OLD / 'RUN-RESULT.v1.json')
            if previous.get('finished_utc'):
                break
            time.sleep(5)
        assert previous['build_exit_code'] == 0
        assert previous['status'] == 'refused_or_unverified' and previous['error'].startswith('FileNotFoundError:')
        assert not (OLD / 'gpu').exists(), 'Never repeat an executed GPU job'
        prepared = probe.read(OLD / 'PREPARED.v1.json')
        probe.check(prepared['input_pins'])
        manifest = tomllib.loads((OLD / 'Cargo.toml').read_text('utf-8-sig'))
        name = manifest['package']['name']
        assert name == 'openrpg-delta-rgb8-transport-candidate-v1'
        built = probe.TARGET / 'debug' / (name + '.exe')
        assert built.stat().st_mtime >= datetime.datetime.fromisoformat(probe.read(OLD / 'JOB.v1.json')['created_utc']).timestamp()
        assert 'Finished' in (OLD / 'build.log').read_text('utf-8-sig')
        executable = ROOT / 'rgb8-transport-probe.immutable.v1.exe'
        shutil.copyfile(built, executable)
        shutil.copyfile(OLD / 'INPUT.v1.json', ROOT / 'INPUT.v1.json')
        pins = dict(prepared['input_pins'])
        for path in [Path(__file__).resolve(), HERE / 'check_delta_rgb8_transport_v3.py',
                     OLD / 'RUN-RESULT.v1.json', OLD / 'build.log', OLD / 'Cargo.toml',
                     executable, ROOT / 'INPUT.v1.json']:
            pins[str(path)] = probe.sha(path)
        probe.save('BUILD-REOPEN.v1.json', dict(actual_manifest_package_name=name,
             build_exit_code=0, original_build_receipt_sha256=probe.sha(OLD / 'RUN-RESULT.v1.json'),
             original_build_log_sha256=probe.sha(OLD / 'build.log'),
             dispatch_error='controller expected v3 executable; Cargo manifest still names package v1',
             previous_gpu_executed=False, original_candidate_sources_unchanged=True,
             no_rebuild=True, executable_sha256=probe.sha(executable), input_pins=pins), new=True)
        probe.check(pins)
        state.update(status='sampling_three_native_rgb24_textures', build_exit_code=0,
                     compiled_candidate_reused=True, executable_sha256=probe.sha(executable))
        probe.save('RUN-RESULT.v1.json', state)
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT))
        with (ROOT / 'execution.stdout.log').open('xb') as out, (ROOT / 'execution.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(executable), str(ROOT / 'INPUT.v1.json'), str(ROOT / 'gpu')],
                cwd=ROOT, env=env, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state['gpu_pid'] = child.pid
            probe.save('RUN-RESULT.v1.json', state)
            state['execution_exit_code'] = child.wait()
        assert state['execution_exit_code'] == 0, 'Native transport/GPU refused; preserve stderr'
        probe.reopen()
        probe.check(pins)
        state.update(status='three_native_rgb24_transport_candidates_gpu_verified',
                     native_rgb24_textures_verified=3, mips_verified=37,
                     independent_receipt_sha256=probe.sha(ROOT / 'INDEPENDENT-GPU-REOPEN.v1.json'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    probe.save('RUN-RESULT.v1.json', state)


def launch():
    assert not ROOT.exists()
    probe.check(probe.read(OLD / 'PREPARED.v1.json')['input_pins'])
    ROOT.mkdir()
    probe.save('CONTROLLER-PIN.v1.json', dict(path=str(Path(__file__).resolve()), sha256=probe.sha(Path(__file__))), new=True)
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    probe.save('JOB.v1.json', dict(controller_pid=child.pid, created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()), new=True)
    print(json.dumps(dict(controller_pid=child.pid, build_reused=True, canonical_renderer_installed=False)))


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else launch()
