"""Compile staged mask controls against the already qualified frozen engine."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import sys

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'source-graph-color-write-mask-native-check-v1'
STAGED = W / 'source-graph-color-write-mask-candidate-v1'
HOST = W / 'delta-rgb8-frozen-canonical-loader-v7'
CARGO = Path('C:/Users/karen/.cargo/bin/cargo.exe')
TARGET = Path('F:/OpenRPG_Forge/relocated-work/Rust-target')

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def check(pins):
    for p, expected in pins.items():
        assert sha(Path(p)) == expected, 'Changed input: ' + p

def save(name, value):
    temporary = ROOT / (name + '.tmp')
    temporary.write_text(json.dumps(value, indent=2), 'utf-8')
    temporary.replace(ROOT / name)

def prepare():
    assert not ROOT.exists()
    staged = read(STAGED / 'PREPARED.v1.json')
    check(staged['input_pins']); check(staged['output_pins'])
    host = read(HOST / 'RUN-RESULT.v1.json')
    assert host['actual_exit_code'] == 0 and host['actual_host_tests_passed'] == 17
    # Keep precisely the tested engine, features, dependency lock and profiles.
    prepared_host = read(HOST / 'PREPARED.v1.json')
    pins = dict(staged['input_pins'])
    pins.update(staged['output_pins'])
    pins.update(prepared_host['input_pins'])
    for p in (Path(__file__).resolve(), STAGED / 'PREPARED.v1.json', HOST / 'RUN-RESULT.v1.json', HOST / 'Cargo.toml', HOST / 'Cargo.lock'):
        pins[str(p)] = sha(p)
    check(pins)
    ROOT.mkdir(); (ROOT / 'src').mkdir(); (ROOT / 'temp').mkdir()
    for name in ['source_graph_color_write_mask.rs', 'source_graph_render_state.rs']:
        shutil.copyfile(STAGED / name, ROOT / 'src' / name)
        pins[str(ROOT / 'src' / name)] = sha(ROOT / 'src' / name)
    (ROOT / 'src/lib.rs').write_text('pub mod source_graph_render_state;\npub mod source_graph_color_write_mask;\n', 'utf-8')
    manifest = (HOST / 'Cargo.toml').read_text('utf-8')
    assert manifest.count('openrpg-studio-source-frame-admission-check') == 2
    manifest = manifest.replace('openrpg-studio-source-frame-admission-check', 'openrpg-source-color-write-mask-check-v1')
    (ROOT / 'Cargo.toml').write_text(manifest, 'utf-8')
    lock = (HOST / 'Cargo.lock').read_text('utf-8')
    assert lock.count('name = "openrpg-studio-source-frame-admission-check"') == 1
    (ROOT / 'Cargo.lock').write_text(lock.replace('name = "openrpg-studio-source-frame-admission-check"', 'name = "openrpg-source-color-write-mask-check-v1"'), 'utf-8')
    for p in [ROOT / 'src/lib.rs', ROOT / 'Cargo.toml', ROOT / 'Cargo.lock']:
        pins[str(p)] = sha(p)
    check(pins)
    save('PREPARED.v1.json', dict(input_pins=pins, source_engine_snapshot_reused=True,
        expected_candidate_tests=2, expected_original_forward_tests=4,
        canonical_renderer_installed=False, gpu_parity_qualified=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid))
    print(json.dumps(dict(controller_pid=child.pid, frozen_actual_engine_reused=True, full_import=False)))

def run():
    state = dict(status='compiling_mask_candidate_with_frozen_actual_engine', tests_passed=0,
        canonical_renderer_installed=False, gpu_parity_qualified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json'); check(prepared['input_pins'])
        env = os.environ.copy()
        env.update(TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'), CARGO_TARGET_DIR=str(TARGET))
        with (ROOT / 'native.stdout.log').open('xb') as out, (ROOT / 'native.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(CARGO), 'test', '-j', '1', '--locked', '--offline', '--lib', '--', '--test-threads=1'],
                cwd=ROOT, env=env, stdin=subprocess.DEVNULL, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid'] = child.pid; save('RUN-RESULT.v1.json', state)
            state['actual_exit_code'] = child.wait()
        assert state['actual_exit_code'] == 0, 'Actual mask candidate compilation/test refused'
        check(prepared['input_pins'])
        output = (ROOT / 'native.stdout.log').read_text('utf-8-sig')
        assert 'test result: ok. 6 passed; 0 failed;' in output
        assert 'original_rgb_mask_preserves_secondary_attachment_and_changes_specialization_key ... ok' in output
        assert 'invalid_channels_or_attachments_refuse_without_partial_state ... ok' in output
        state.update(status='six_original_forward_and_rgb_mask_candidate_tests_passed', tests_passed=6,
            input_pins_unchanged=True, stdout_sha256=sha(ROOT / 'native.stdout.log'), stderr_sha256=sha(ROOT / 'native.stderr.log'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error), diagnostic_traceback=traceback.format_exc())
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)

if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
