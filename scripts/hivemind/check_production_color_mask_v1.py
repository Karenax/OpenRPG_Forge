"""Compile actual staged renderer integration on an immutable engine snapshot."""
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
ROOT = W / 'source-graph-production-color-mask-check-v1'
STAGED = W / 'source-graph-production-color-mask-v1'
HOST = W / 'delta-rgb8-frozen-canonical-loader-v7'
OLD_SNAPSHOT = W / 'delta-rgb8-frozen-canonical-loader-v6/snapshot'
SNAPSHOT = ROOT / 'snapshot'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TARGET = Path('F:/OpenRPG_Forge/relocated-work/Rust-target')
WAIT = W / 'firepit-active-texture-gpu-run-v2/RUN-RESULT.v1.json'

def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def check(pins):
    for p, digest in pins.items(): assert sha(Path(p)) == digest, 'Changed input: ' + p
def save(name, value):
    p = ROOT / (name + '.tmp'); p.write_text(json.dumps(value, indent=2), 'utf-8'); p.replace(ROOT / name)

def prepare():
    assert not ROOT.exists()
    stage = read(STAGED / 'PREPARED.v1.json')
    pins = dict(stage['input_pins']); pins.update(stage['output_pins'])
    pins.update(read(HOST / 'PREPARED.v1.json')['input_pins'])
    assert read(HOST / 'RUN-RESULT.v1.json')['actual_host_tests_passed'] == 17
    for p in [Path(__file__).resolve(), STAGED / 'PREPARED.v1.json', HOST / 'Cargo.toml', HOST / 'Cargo.lock', HOST / 'src/main.rs', BIN / 'cargo.exe', BIN / 'rustc.exe']:
        pins[str(p)] = sha(p)
    check(pins); ROOT.mkdir(); (ROOT / 'temp').mkdir(); (ROOT / 'src').mkdir()
    for name in pins:
        source = Path(name)
        if source.is_relative_to(OLD_SNAPSHOT):
            target = SNAPSHOT / source.relative_to(OLD_SNAPSHOT)
            target.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(source, target)
    for relative in stage['changed_paths']:
        target = SNAPSHOT / relative; target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(STAGED / 'candidate' / relative, target)
    # Reuse actual Studio host tests, changing only their snapshot path.
    for name in ['Cargo.toml', 'Cargo.lock', 'src/main.rs']:
        text = (HOST / name).read_text('utf-8')
        text = text.replace(OLD_SNAPSHOT.as_posix(), SNAPSHOT.as_posix())
        text = text.replace('openrpg-studio-source-frame-admission-check', 'openrpg-production-color-mask-host-check')
        (ROOT / name).write_text(text, 'utf-8')
    frozen = {str(p): sha(p) for p in SNAPSHOT.rglob('*') if p.is_file()}
    for p in [ROOT / 'Cargo.toml', ROOT / 'Cargo.lock', ROOT / 'src/main.rs']: frozen[str(p)] = sha(p)
    check(pins); check(frozen)
    save('PREPARED.v1.json', dict(input_pins=pins, frozen_pins=frozen, actual_engine_source_files=len(frozen)-3,
        expected_host_tests=17, renderer_unit_test_filter='source_graph', canonical_installed=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as out:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdout=out, stderr=out, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid)); print(json.dumps(dict(controller_pid=child.pid, source_files=len(frozen)-3)))

def command(state, label, arguments, env):
    state['status'] = label; save('RUN-RESULT.v1.json', state)
    with (ROOT / (label + '.stdout.log')).open('xb') as out, (ROOT / (label + '.stderr.log')).open('xb') as err:
        child = subprocess.Popen([str(BIN / 'cargo.exe')] + arguments, cwd=ROOT, env=env,
            stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
        state['cargo_pid'] = child.pid; save('RUN-RESULT.v1.json', state); code = child.wait()
    state[label + '_exit_code'] = code
    assert code == 0, 'Actual test/build refused: ' + label
    return (ROOT / (label + '.stdout.log')).read_text('utf-8-sig')

def run():
    state = dict(status='waiting_for_owned_texture_gpu_build', host_tests_passed=0, actual_renderer_tests_passed=0,
        canonical_installed=False, gpu_shader_parity_qualified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        while not read(WAIT).get('finished_utc'): time.sleep(15)
        prepared = read(ROOT / 'PREPARED.v1.json'); check(prepared['input_pins']); check(prepared['frozen_pins'])
        state['preceding_gpu_job_sha256'] = sha(WAIT)
        env = os.environ.copy(); env.update(RUSTC=str(BIN / 'rustc.exe'), TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'),
            CARGO_TARGET_DIR=str(TARGET), CARGO_BUILD_JOBS='1', PATH=str(BIN) + os.pathsep + env.get('PATH', ''))
        output = command(state, 'checking_seventeen_actual_studio_host_tests',
            ['test', '--locked', '--offline', '-j', '1', '--', '--test-threads=1'], env)
        assert 'test result: ok. 17 passed; 0 failed;' in output
        state['host_tests_passed'] = 17; check(prepared['frozen_pins']); check(prepared['input_pins'])
        output = command(state, 'checking_actual_renderer_source_graph_tests',
            ['test', '--manifest-path', str(SNAPSHOT / 'Cargo.toml'), '-p', 'forge_engine', '--lib', '--locked', '--offline', '-j', '1', 'source_graph', '--', '--test-threads=1'], env)
        match = re.search(r'test result: ok\. (\d+) passed; 0 failed;', output); assert match
        for name in ['schema_four_rgb_reaches_actual_material_program_and_legacy_keeps_all',
                     'unknown_mask_missing_mask_or_mask_in_legacy_contract_refuse',
                     'original_rgb_mask_preserves_secondary_attachment_and_changes_specialization_key',
                     'invalid_channels_or_attachments_refuse_without_partial_state']:
            assert re.search(r'::' + name + r' \.\.\. ok', output), 'Missing real renderer test: ' + name
        check(prepared['input_pins']); check(prepared['frozen_pins'])
        state.update(status='actual_host_and_renderer_mask_integration_tests_passed', actual_renderer_tests_passed=int(match.group(1)),
            source_pins_unchanged=True, frozen_pins_unchanged=True)
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error), diagnostic_traceback=traceback.format_exc())
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat(); save('RUN-RESULT.v1.json', state)

if __name__ == '__main__': run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
