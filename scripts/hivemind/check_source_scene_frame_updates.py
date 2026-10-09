"""Compile the actual Studio frame updater and run its scoped native tests."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

REPO = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
ROOT = Path('F:/OpenRPG_ImportWork/studio-source-scene-frame-native-v1')
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TARGET = Path('F:/OpenRPG_Forge/relocated-work/Rust-target')
MODULES = REPO / 'apps/forge_studio/src/authoring_assets'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(name, value):
    (ROOT / name).write_text(json.dumps(value, indent=2) + '\n', 'utf-8')


def prepare():
    assert not ROOT.exists(), 'Existing native checks preserved'
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    (ROOT / 'temp').mkdir()
    manifest = '''[package]
name="openrpg-studio-source-scene-frame-check"
version="0.1.0"
edition="2024"
[workspace]
[dependencies]
forge_engine={path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine"}
forge_assets={path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_assets"}
bevy={version="=0.19.1",default-features=false,features=["3d","ui"]}
'''
    (ROOT / 'Cargo.toml').write_text(manifest, 'utf-8')
    modules = ['source_graph_scene', 'source_graph_resources', 'source_graph_frame']
    (ROOT / 'src/main.rs').write_text('\n'.join(
        f'#[allow(dead_code)]\n#[path="{(MODULES / (name + ".rs")).as_posix()}"]\nmod {name};'
        for name in modules) + '\nfn main(){}\n', 'utf-8')
    paths = [Path(__file__).resolve(), ROOT / 'Cargo.toml', ROOT / 'src/main.rs',
             *[MODULES / (name + '.rs') for name in modules]]
    paths += list((REPO / 'crates/forge_engine/src').rglob('*.rs'))
    paths += list((REPO / 'crates/forge_assets/src').rglob('*.rs'))
    save('PREPARED.v1.json', {'input_pins': {str(path): sha(path) for path in paths},
        'expected_tests': 12, 'new_frame_tests': 3, 'gpu_rendered': False, 'studio_verified': False})
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdin=subprocess.DEVNULL, stdout=log, stderr=log,
            creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': child.pid,
         'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({'pid': child.pid, 'root': str(ROOT), 'tests_not_yet_run': True}))


def run():
    prepared = json.loads((ROOT / 'PREPARED.v1.json').read_text('utf-8'))
    state = {'status': 'running_actual_source_frame_module_tests', 'tests_passed': 0,
             'gpu_rendered': False, 'studio_verified': False, 'full_import': False}
    save('RUN-RESULT.v1.json', state)
    try:
        for path, expected in prepared['input_pins'].items():
            assert sha(path) == expected, 'Prepared native source changed'
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
            assert child.wait() == 0, 'Actual module compile/tests failed; logs retained'
        output = (ROOT / 'native.stdout.log').read_text('utf-8')
        assert 'test result: ok. 12 passed; 0 failed;' in output, 'Native test scope/count mismatch'
        for path, expected in prepared['input_pins'].items():
            assert sha(path) == expected, 'Source changed during native module tests'
        state.update(status='12_actual_attachment_resource_and_frame_tests_verified',
            tests_passed=12, new_frame_tests_passed=3,
            all_input_pins_unchanged=True, input_pins=prepared['input_pins'],
            stdout_sha256=sha(ROOT / 'native.stdout.log'), stderr_sha256=sha(ROOT / 'native.stderr.log'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)
    assert state['status'] != 'refused_or_unverified', state.get('error')


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
