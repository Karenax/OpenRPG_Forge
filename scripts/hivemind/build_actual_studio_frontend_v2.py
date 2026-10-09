"""Build the real Studio frontend on F after the exact WASM check passes."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

from recover_native_scene_check_queue import wait_exact

REPO = Path(__file__).resolve().parents[2]
ROOT = Path('F:/OpenRPG_ImportWork/studio-actual-frontend-build-v2')
CHECK = Path('F:/OpenRPG_ImportWork/studio-current-source-wasm-check-v3')
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TOOLS = Path('C:/Users/karen/.cargo/bin')
PS = Path('C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell')


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def save(name, value):
    path = ROOT / name
    temporary = path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2), 'utf-8')
    temporary.replace(path)


def prepare():
    assert not ROOT.exists(), 'Previous frontend build evidence preserved'
    check_pins = read(CHECK / 'PREPARED.v1.json')['source_pins']
    extras = [Path(__file__).resolve(), Path(__file__).with_name('recover_native_scene_check_queue.py'),
              REPO / 'scripts/Build-NavigationWorker.ps1', TOOLS / 'trunk.exe',
              TOOLS / 'wasm-bindgen.exe', BIN / 'cargo.exe', BIN / 'rustc.exe', PS / 'pwsh.exe']
    extras.extend(p for p in (REPO / 'apps/forge_studio').iterdir()
                  if p.is_file() and p.suffix in ('.html', '.css', '.js', '.toml'))
    pins = {**check_pins, **{str(p): sha(p) for p in extras}}
    assert all(sha(p) == expected for p, expected in pins.items()), 'Prepared sources changed'
    ROOT.mkdir()
    (ROOT / 'temp').mkdir()
    save('PREPARED.v1.json', dict(input_pins=pins, wait_pid=45180,
         wait_birth_ticks=639269712529910233, frontend_profile='debug', navigation_profile='release'))
    with (ROOT / 'controller.log').open('xb') as output:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
              stdin=subprocess.DEVNULL, stdout=output, stderr=output, cwd=REPO,
              creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(pid=child.pid, started_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(pid=child.pid, root=str(ROOT), built=False)))


def run():
    state = dict(status='waiting_for_exact_wasm_v3_check', gpu_rendered=False,
                 studio_verified=False, full_import=False, frontend_profile='debug')
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        wait_exact(prepared['wait_pid'], prepared['wait_birth_ticks'])
        check = read(CHECK / 'RUN-RESULT.v1.json')
        assert check['status'] == 'actual_studio_wasm_frontend_compile_check_passed', 'WASM check has not passed'
        assert check['actual_exit_code'] == 0 and not check['changed_source_files']
        pins = prepared['input_pins']
        assert all(sha(p) == expected for p, expected in pins.items()), 'Source or tool changed before frontend build'
        env = os.environ.copy()
        env.update(PATH=os.pathsep.join([str(BIN), str(TOOLS), str(PS), env.get('PATH', '')]),
                   RUSTC=str(BIN / 'rustc.exe'), CARGO_TARGET_DIR='F:/OpenRPG_Forge/relocated-work/Rust-target',
                   CARGO_BUILD_JOBS='1', CARGO_INCREMENTAL='0', CARGO_NET_OFFLINE='true',
                   TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'), NO_COLOR='true')
        command = [str(TOOLS / 'trunk.exe'), 'build', '--config', 'Trunk.toml',
                   '--offline', 'true', '--locked', 'true', '--skip-version-check',
                   '--dist', str(ROOT / 'dist')]
        save('COMMAND.v1.json', dict(command=command, cwd=str(REPO / 'apps/forge_studio')))
        with (ROOT / 'stdout.log').open('xb') as out, (ROOT / 'stderr.log').open('xb') as err:
            child = subprocess.Popen(command, cwd=REPO / 'apps/forge_studio', env=env,
                    stdin=subprocess.DEVNULL, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='building_actual_studio_frontend_and_navigation_worker', trunk_pid=child.pid)
            save('RUN-RESULT.v1.json', state)
            code = child.wait()
        state.update(actual_exit_code=code, changed_source_files=[p for p, h in pins.items() if sha(p) != h],
                     stdout_sha256=sha(ROOT / 'stdout.log'), stderr_sha256=sha(ROOT / 'stderr.log'))
        assert code == 0 and not state['changed_source_files'], 'Frontend build failed or sources changed'
        dist = ROOT / 'dist'
        worker = read(dist / 'forge-navigation-worker-manifest.json')
        assert worker['source_unchanged_during_build'] and worker['build_only']
        for pin in worker['source_pins']:
            assert sha(REPO / pin['path']) == pin['sha256'], 'Worker source changed'
        for artifact in worker['artifacts']:
            assert sha(dist / artifact['path']) == artifact['sha256'], 'Worker artifact changed'
        for name in ['index.html', 'styles.css', 'forge_studio.js',
                     'forge_studio_bg.wasm', 'forge_navigation.js', 'forge_navigation_bg.wasm',
                     'forge-navigation-worker.js', 'tidewater-webgpu-compat.js']:
            assert (dist / name).is_file(), 'Required frontend artifact missing: ' + name
        for name in ['forge_studio_bg.wasm', 'forge_navigation_bg.wasm']:
            with (dist / name).open('rb') as stream:
                assert stream.read(8) == b'\x00asm\x01\x00\x00\x00', 'Invalid WASM header'
        state.update(status='actual_studio_frontend_and_navigation_worker_built',
                     artifacts={str(p.relative_to(dist)): dict(bytes=p.stat().st_size, sha256=sha(p))
                                for p in sorted(dist.rglob('*')) if p.is_file()},
                     input_pins=pins, prerequisite_receipt_sha256=sha(CHECK / 'RUN-RESULT.v1.json'))
    except Exception as error:
        state.update(status='actual_frontend_build_refused', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
