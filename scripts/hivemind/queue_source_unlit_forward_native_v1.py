"""Test the genuine forward-only material after the owned engine mask job.

Reuse its frozen engine; retain all seventeen existing Studio checks.
No installation, shader/particle GPU or full-import claim from these tests.
"""
from pathlib import Path
import datetime
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'source-unlit-forward-native-check-v1'
BASE = W / 'source-graph-production-color-mask-check-v3'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TARGET = Path('F:/OpenRPG_Forge/relocated-work/Rust-target')
CANDIDATE = Path(__file__).resolve().parent / 'candidates/source_unlit_forward.rs'
TESTS = [
    'source_unlit_forward_disables_native_non_forward_material_routes',
    'source_unlit_forward_requires_raw_uv_color_without_fabricating_tangents',
    'source_unlit_forward_specializes_original_rgb_blend_and_rejects_prepass',
    'source_unlit_forward_preserves_mirrored_view_culling_and_secondary_target',
]

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return json.loads(p.read_text('utf-8-sig'))
def check(pins):
    for name, digest in pins.items():
        assert sha(Path(name)) == digest, 'Changed pinned input: ' + name
def save(name, data):
    temp = ROOT / (name + '.tmp')
    temp.write_text(json.dumps(data, indent=2), 'utf-8'); temp.replace(ROOT / name)

def prepare():
    assert not ROOT.exists(), 'Keep earlier evidence'
    pins = {str(p): sha(p) for p in [CANDIDATE, Path(__file__).resolve(),
        BIN / 'cargo.exe', BIN / 'rustc.exe',
        W / 'delta-firepit-single-pass-requirements-v1/RESULT.v1.json',
        W / 'delta-firepit-geometry-buffers-v2/RESULT.v1.json']}
    ROOT.mkdir(); (ROOT / 'src').mkdir(); (ROOT / 'temp').mkdir()
    shutil.copyfile(CANDIDATE, ROOT / 'src/source_unlit_forward.rs')
    pins[str(ROOT / 'src/source_unlit_forward.rs')] = sha(ROOT / 'src/source_unlit_forward.rs')
    check(pins)
    save('QUEUED.v1.json', dict(input_pins=pins, expected_source_material_tests=4,
        existing_studio_tests_preserved=17, installed=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as out:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdin=subprocess.DEVNULL, stdout=out, stderr=out,
            creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid))
    print(json.dumps(dict(controller_pid=child.pid, expected_tests=21, installed=False)))

def run():
    state = dict(status='waiting_for_owned_production_mask_v3', actual_material_tests_passed=0,
        actual_studio_tests_passed=0, installed=False, gpu_render_qualified=False,
        whole_effect_qualified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        while True:
            result_path = BASE / 'RUN-RESULT.v1.json'
            if result_path.is_file():
                baseline = read(result_path)
                if baseline.get('finished_utc'): break
            time.sleep(15)
        assert baseline.get('host_tests_passed') == 17 and baseline.get('actual_renderer_tests_passed', 0) > 0, 'Prerequisite renderer/host tests did not pass'
        queued = read(ROOT / 'QUEUED.v1.json'); check(queued['input_pins'])
        parent = read(BASE / 'PREPARED.v1.json'); check(parent['frozen_pins'])
        pins = dict(queued['input_pins']); pins.update(parent['frozen_pins'])
        for p in [result_path, BASE / 'PREPARED.v1.json']: pins[str(p)] = sha(p)
        for name in ['Cargo.toml', 'Cargo.lock', 'src/main.rs']:
            text = (BASE / name).read_text('utf-8')
            text = text.replace('openrpg-production-color-mask-host-check', 'openrpg-source-unlit-forward-host-check')
            if name == 'src/main.rs': text += '\nmod source_unlit_forward;\n'
            (ROOT / name).write_text(text, 'utf-8')
            pins[str(ROOT / name)] = sha(ROOT / name)
        check(pins)
        save('PREPARED.v1.json', dict(input_pins=pins, frozen_renderer_snapshot=True,
            expected_tests=21, tests=TESTS, installed=False, full_import=False))
        env = os.environ.copy()
        env.update(RUSTC=str(BIN / 'rustc.exe'), CARGO_TARGET_DIR=str(TARGET),
            CARGO_BUILD_JOBS='1', TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'),
            PATH=str(BIN) + os.pathsep + env.get('PATH', ''))
        state['status'] = 'testing_actual_forward_only_material_and_seventeen_studio_tests'
        save('RUN-RESULT.v1.json', state)
        with (ROOT / 'testing.stdout.log').open('xb') as out, (ROOT / 'testing.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(BIN / 'cargo.exe'), 'test', '--locked', '--offline',
                '-j', '1', '--', '--test-threads=1'], cwd=ROOT, env=env,
                stdin=subprocess.DEVNULL, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid'] = child.pid; save('RUN-RESULT.v1.json', state)
            state['testing_exit_code'] = child.wait()
        assert state['testing_exit_code'] == 0, 'Actual forward-only material build/test refused'
        output = (ROOT / 'testing.stdout.log').read_text('utf-8-sig')
        assert 'test result: ok. 21 passed; 0 failed;' in output
        for name in TESTS: assert re.search(r'::' + name + r' \.\.\. ok', output), 'Missing material test: ' + name
        check(pins)
        state.update(status='actual_forward_only_material_and_studio_tests_passed',
            actual_material_tests_passed=4, actual_studio_tests_passed=17, source_pins_unchanged=True)
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error),
            diagnostic_traceback=traceback.format_exc())
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)

if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
