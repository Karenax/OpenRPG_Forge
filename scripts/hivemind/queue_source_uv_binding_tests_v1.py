"""Test the actual source UV bindings after the current pinned position job."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path('F:/OpenRPG_ImportWork/source-graph-uv-bindings-native-v1')
PREVIOUS = Path('F:/OpenRPG_ImportWork/source-graph-position-frame-native-v1')
SOURCE = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/src/source_graph_uv.rs')
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def save(name, value):
    path = ROOT / name
    temp = path.with_suffix('.tmp')
    temp.write_text(json.dumps(value, indent=2), encoding='utf-8')
    temp.replace(path)


def check(pins):
    for path, expected in pins.items():
        assert sha(path) == expected, 'Pinned input changed: ' + path


def process_identity(pid):
    script = f"$p=Get-Process -Id {int(pid)} -ErrorAction SilentlyContinue; if($p){{$p.StartTime.ToUniversalTime().Ticks}}"
    completed = subprocess.run([PS, '-NoProfile', '-Command', script],
                               capture_output=True, text=True, check=True,
                               creationflags=subprocess.CREATE_NO_WINDOW)
    return completed.stdout.strip()


def prepare():
    pid = read(PREVIOUS / 'JOB.v1.json')['controller_pid']
    ticks = process_identity(pid)
    assert ticks or read(PREVIOUS / 'RUN-RESULT.v1.json').get('finished_utc')
    ROOT.mkdir(exist_ok=False)
    (ROOT / 'src').mkdir()
    (ROOT / 'temp').mkdir()
    (ROOT / 'Cargo.toml').write_text(
        '[package]\nname="openrpg-source-uv-binding-check"\nversion="0.1.0"\nedition="2024"\n'
        '[workspace]\n[dependencies]\nbevy={version="=0.19.1",default-features=false,features=["3d","ui"]}\n',
        encoding='utf-8')
    (ROOT / 'Cargo.lock').write_bytes((PREVIOUS / 'Cargo.lock').read_bytes())
    (ROOT / 'src/main.rs').write_text(
        '#[path = "' + SOURCE.as_posix() + '"]\nmod source_uv;\nfn main() {}\n', encoding='utf-8')
    pins = {str(path): sha(path) for path in
            [SOURCE, Path(__file__).resolve(), ROOT / 'Cargo.toml', ROOT / 'src/main.rs']}
    save('PREPARED.v1.json', dict(input_pins=pins, expected_tests=3,
                                wait_pid=pid, wait_birth_ticks=ticks, full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
                                 cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL,
                                 creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid,
                            created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(queued_controller_pid=child.pid, expected_tests=3)))


def run():
    state = dict(status='waiting_for_exact_position_controller', tests_passed=0,
                 pipeline_integrated=False, gpu_rendered=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        check(prepared['input_pins'])
        pid, ticks = prepared['wait_pid'], prepared['wait_birth_ticks']
        while ticks and process_identity(pid) == ticks:
            subprocess.run([PS, '-NoProfile', '-Command',
                            f"$p=Get-Process -Id {pid} -ErrorAction SilentlyContinue; if($p){{$p.WaitForExit(45000)}}"],
                           stdout=subprocess.DEVNULL, check=True,
                           creationflags=subprocess.CREATE_NO_WINDOW)
        assert read(PREVIOUS / 'RUN-RESULT.v1.json').get('finished_utc'), 'Upstream has no terminal receipt'
        check(prepared['input_pins'])
        env = os.environ.copy()
        env.update(CARGO_TARGET_DIR='F:/OpenRPG_Forge/relocated-work/Rust-target',
                   CARGO_INCREMENTAL='0', TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'))
        with (ROOT / 'native.log').open('xb') as log:
            child = subprocess.Popen(['C:/Users/karen/.cargo/bin/cargo.exe', '+1.98.0',
                                      'test', '--offline', '-j1', '--', '--test-threads=1'],
                                     cwd=ROOT, env=env, stdout=log, stderr=log,
                                     stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='testing_actual_source_uv_bindings', cargo_pid=child.pid)
            save('RUN-RESULT.v1.json', state)
            code = child.wait()
        state['actual_exit_code'] = code
        assert code == 0, 'Actual native test failure; retained native.log'
        assert re.search(r'test result: ok\. 3 passed; 0 failed; 0 ignored;',
                         (ROOT / 'native.log').read_text('utf-8-sig'))
        check(prepared['input_pins'])
        state.update(status='three_actual_uv_binding_tests_passed', tests_passed=3,
                     input_pins=prepared['input_pins'], native_log_sha256=sha(ROOT / 'native.log'))
    except Exception as error:
        state.update(status='refused_or_interrupted', error=type(error).__name__ + ': ' + str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
