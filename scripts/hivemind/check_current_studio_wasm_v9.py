"""Check the actual current Studio frontend, without claiming rendered parity."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time

REPO = Path(__file__).resolve().parents[2]
ROOT = Path('F:/OpenRPG_ImportWork/studio-current-source-wasm-check-v9')
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
CACHE = Path('F:/OpenRPG_ImportWork/firepit-static-glb-product-check-v2/target')


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(name, value):
    path = ROOT / name
    temporary = path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2), 'utf-8')
    for attempt in range(40):
        try:
            temporary.replace(path)
            return
        except PermissionError:
            if attempt == 39:
                raise
            time.sleep(.25)


def prepare():
    assert not ROOT.exists(), 'Previous build evidence preserved'
    files = {Path(__file__).resolve(), REPO / 'Cargo.toml', REPO / 'Cargo.lock', REPO / 'rust-toolchain.toml'}
    for folder in [REPO / 'crates', REPO / 'apps/forge_studio', REPO / 'third_party']:
        for path in folder.rglob('*'):
            if path.is_file() and path.suffix in ['.rs', '.toml', '.wgsl'] and not any(
                part in ['target', 'dist', 'node_modules', '.git'] for part in path.relative_to(folder).parts):
                files.add(path)
    pins = {str(path): sha(path) for path in sorted(files)}
    ROOT.mkdir()
    (ROOT / 'temp').mkdir()
    save('PREPARED.v1.json', dict(source_pins=pins, source_files=len(pins)))
    with (ROOT / 'controller.log').open('xb') as output:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=REPO, stdin=subprocess.DEVNULL, stdout=output, stderr=output,
            creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(pid=child.pid, started_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(pid=child.pid, source_files_pinned=len(pins), compiler_check_passed=False)))


def run():
    pins = json.loads((ROOT / 'PREPARED.v1.json').read_text('utf-8'))['source_pins']
    state = dict(status='checking_actual_studio_wasm_frontend', gpu_rendered=False,
                 studio_verified=False, full_import=False, target='wasm32-unknown-unknown')
    save('RUN-RESULT.v1.json', state)
    try:
        assert all(sha(p) == expected for p, expected in pins.items()), 'Prepared source changed'
        env = os.environ.copy()
        env.update(PATH=str(BIN) + os.pathsep + env.get('PATH', ''), RUSTC=str(BIN / 'rustc.exe'),
                   CARGO_BUILD_JOBS='1', CARGO_INCREMENTAL='0', CARGO_TARGET_DIR=str(CACHE), TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'))
        command = [str(BIN / 'cargo.exe'), 'check', '--locked', '--offline', '-p', 'forge_studio',
                   '--target', 'wasm32-unknown-unknown', '--no-default-features', '--features', 'web']
        save('COMMAND.v1.json', dict(command=command, current_working_directory=str(REPO)))
        with (ROOT / 'stdout.log').open('xb') as out, (ROOT / 'stderr.log').open('xb') as err:
            child = subprocess.Popen(command, cwd=REPO, env=env, stdin=subprocess.DEVNULL,
                                     stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid'] = child.pid
            save('RUN-RESULT.v1.json', state)
            code = child.wait()
        changed = [p for p, expected in pins.items() if sha(p) != expected]
        state.update(actual_exit_code=code, changed_source_files=changed, input_pins=pins,
                     stdout_sha256=sha(ROOT / 'stdout.log'), stderr_sha256=sha(ROOT / 'stderr.log'),
                     status='actual_studio_wasm_frontend_compile_check_passed' if code == 0 and not changed
                            else 'wasm_compile_failure_or_changed_sources_retained')
    except Exception as error:
        state.update(status='wasm_check_refused', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()

