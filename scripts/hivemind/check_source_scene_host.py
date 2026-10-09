"""Check the shared Studio scene host after the exact frame-test controller."""
import datetime
import json
import os
from pathlib import Path
import subprocess
import sys

from check_source_scene_frame_updates import REPO, BIN, TARGET, MODULES, sha
from recover_native_scene_check_queue import wait_exact

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK / 'studio-source-scene-host-native-v1'
PRIOR = WORK / 'studio-source-scene-frame-native-v1'
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'


def save(name, value):
    (ROOT / name).write_text(json.dumps(value, indent=2) + '\n', 'utf-8')


def prepare():
    assert not ROOT.exists(), 'Existing host validation preserved'
    job = json.loads((PRIOR / 'JOB.v1.json').read_text('utf-8'))
    birth = subprocess.check_output([PS, '-NoProfile', '-Command',
        f'(Get-Process -Id {job["pid"]} -ErrorAction Stop).StartTime.ToUniversalTime().Ticks'],
        text=True).strip()
    assert birth.isdigit(), 'Expected frame controller must be currently live'
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    (ROOT / 'temp').mkdir()
    manifest = (PRIOR / 'Cargo.toml').read_text('utf-8').replace(
        'openrpg-studio-source-scene-frame-check', 'openrpg-studio-source-scene-host-check')
    (ROOT / 'Cargo.toml').write_text(manifest, 'utf-8')
    modules = ['source_graph_scene', 'source_graph_resources', 'source_graph_frame',
               'source_graph_intake', 'source_graph_host']
    (ROOT / 'src/main.rs').write_text('\n'.join(
        f'#[allow(dead_code)]\n#[path="{(MODULES / (name + ".rs")).as_posix()}"]\nmod {name};'
        for name in modules) + '\nfn main(){}\n', 'utf-8')
    paths = [Path(__file__).resolve(), ROOT / 'Cargo.toml', ROOT / 'src/main.rs',
             Path(__file__).with_name('check_source_scene_frame_updates.py'),
             Path(__file__).with_name('recover_native_scene_check_queue.py'),
             *[MODULES / (name + '.rs') for name in modules]]
    paths += list((REPO / 'crates/forge_engine/src').rglob('*.rs'))
    paths += list((REPO / 'crates/forge_assets/src').rglob('*.rs'))
    save('PREPARED.v1.json', {'wait_pid': job['pid'], 'wait_birth_ticks': int(birth),
        'input_pins': {str(path): sha(path) for path in paths},
        'expected_tests': 14, 'new_host_tests': 2,
        'gpu_rendered': False, 'studio_verified': False})
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdin=subprocess.DEVNULL, stdout=log, stderr=log,
            creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': child.pid,
         'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({'pid': child.pid, 'root': str(ROOT), 'host_tests_not_yet_run': True}))


def run():
    prepared = json.loads((ROOT / 'PREPARED.v1.json').read_text('utf-8'))
    state = {'status': 'waiting_for_exact_frame_module_controller', 'tests_passed': 0,
             'gpu_rendered': False, 'studio_verified': False, 'full_import': False}
    save('RUN-RESULT.v1.json', state)
    try:
        wait_exact(prepared['wait_pid'], prepared['wait_birth_ticks'])
        prior_path = PRIOR / 'RUN-RESULT.v1.json'
        prior = json.loads(prior_path.read_text('utf-8'))
        assert prior['status'] == '12_actual_attachment_resource_and_frame_tests_verified'
        assert prior['tests_passed'] == 12 and prior['all_input_pins_unchanged']
        pins = {**prepared['input_pins'], str(prior_path): sha(prior_path)}
        for path, expected in pins.items():
            assert sha(path) == expected, 'Prepared scene host input changed'
        state['status'] = 'checking_actual_shared_scene_host_modules'
        save('RUN-RESULT.v1.json', state)
        environment = os.environ.copy()
        environment['PATH'] = str(BIN) + os.pathsep + environment.get('PATH', '')
        environment['RUSTC'] = str(BIN / 'rustc.exe')
        environment['CARGO_BUILD_JOBS'] = '2'
        environment['TEMP'] = environment['TMP'] = str(ROOT / 'temp')
        with (ROOT / 'native.stdout.log').open('xb') as out, (ROOT / 'native.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(BIN / 'cargo.exe'), 'test', '--offline', '--manifest-path',
                str(ROOT / 'Cargo.toml'), '--target-dir', str(TARGET), '--', '--nocapture'],
                cwd=ROOT, env=environment, stdout=out, stderr=err,
                creationflags=subprocess.CREATE_NO_WINDOW)
            save('CARGO-JOB.v1.json', {'pid': child.pid})
            assert child.wait() == 0, 'Actual scene host compile/tests refused; logs retained'
        output = (ROOT / 'native.stdout.log').read_text('utf-8')
        assert 'test result: ok. 14 passed; 0 failed;' in output, 'Host test scope/count mismatch'
        for path, expected in pins.items():
            assert sha(path) == expected, 'Scene host source changed during validation'
        state.update(status='14_actual_shared_scene_host_module_tests_verified', tests_passed=14,
            all_input_pins_unchanged=True, input_pins=pins,
            stdout_sha256=sha(ROOT / 'native.stdout.log'), stderr_sha256=sha(ROOT / 'native.stderr.log'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)
    assert state['status'] != 'refused_or_unverified', state.get('error')


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
