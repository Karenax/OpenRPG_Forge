"""Wait for post-install tests, then test actual Bevy material/view/model layouts."""
from pathlib import Path
from datetime import datetime, timezone
import ctypes
import hashlib
import json
import os
import subprocess
import sys
import time

S = Path(__file__).parent
R = S.parents[1]
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-s-paint-bevy-pipeline-v1'
INSTALL = W / 'native-texture-capture-common-install-v4'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(name, data):
    path = ROOT / name
    temp = path.with_suffix('.tmp')
    temp.write_text(json.dumps(data, indent=2), encoding='utf-8')
    temp.replace(path)


def live(pid):
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.OpenProcess.restype = ctypes.c_void_p
    kernel.GetExitCodeProcess.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_ulong)]
    kernel.CloseHandle.argtypes = [ctypes.c_void_p]
    handle = kernel.OpenProcess(0x1000, False, int(pid))
    if not handle:
        return False
    try:
        value = ctypes.c_ulong()
        assert kernel.GetExitCodeProcess(handle, ctypes.byref(value))
        return value.value == 259
    finally:
        kernel.CloseHandle(handle)


def prepare():
    assert not ROOT.exists()
    upstream_pid = read(INSTALL / 'JOB.v1.json')['pid']
    upstream = read(INSTALL / 'RUN-RESULT.v1.json')
    assert live(upstream_pid) or upstream['status'] == 'common_native_capture_installed_workspace_tests_passed'
    exported_path = W / 'townsmith-s-paint-composed-wgsl-v1/RESULT.v1.json'
    exported = read(exported_path)
    assert exported['status'] == 'seven_real_bevy_compositions_exported_to_standalone_wgsl'
    pins = dict(exported['input_pins'])
    pins[str(exported_path)] = sha(exported_path)
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    baseline = W / 'source-punctual-light-contract-check-v1'
    for name in ('Cargo.toml', 'Cargo.lock'):
        text = (baseline / name).read_text(encoding='utf-8')
        assert 'openrpg-firepit-source-ao-scene-v1' in text
        path = ROOT / name
        path.write_text(text.replace('openrpg-firepit-source-ao-scene-v1', 'openrpg-townsmith-actual-bevy-pipeline-v1'), encoding='utf-8')
        pins[str(path)] = sha(path)
    candidate = S / 'candidates/SourceTownsmithBevyPipelineAdmissionV1.rs'
    source = ROOT / 'src/main.rs'
    source.write_bytes(candidate.read_bytes())
    manifest = ROOT / 'INPUT.v1.json'
    manifest.write_text(json.dumps({'cases': exported['cases']}, indent=2), encoding='utf-8')
    paths = [candidate, source, manifest, Path(__file__)]
    paths += [R / p for p in ('crates/forge_assets/src/source_graph_bundle.rs', 'crates/forge_assets/src/source_graph_bundle/imported_storage.rs',
                             'crates/forge_engine/src/source_graph.rs', 'crates/forge_engine/src/source_graph_program.rs')]
    pins.update({str(p): sha(p) for p in paths})
    for path, expected in pins.items():
        assert sha(path) == expected, path
    save('PREPARED.v1.json', {'input_pins': pins, 'upstream_controller_pid': upstream_pid})
    save('RUN-RESULT.v1.json', {'status': 'actual_bevy_pipeline_waiting_for_post_install_tests', 'full_import': False, 'complete_assets_delivered_increment': 0, 'upstream_controller_pid': upstream_pid})
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': child.pid})
    print(json.dumps({'pid': child.pid, 'root': str(ROOT), 'upstream_controller_pid': upstream_pid}))


def run():
    state = read(ROOT / 'RUN-RESULT.v1.json')
    try:
        prep = read(ROOT / 'PREPARED.v1.json')
        while live(prep['upstream_controller_pid']):
            time.sleep(3)
        upstream = read(INSTALL / 'RUN-RESULT.v1.json')
        assert upstream['status'] == 'common_native_capture_installed_workspace_tests_passed', upstream
        for label in ('assets', 'engine', 'unlit', 'program', 'glb'):
            assert upstream[label + '_exit_code'] == 0
        for path, expected in prep['input_pins'].items():
            assert sha(path) == expected, path
        assert subprocess.check_output(['powershell', '-NoProfile', '-Command', '@(Get-Process cargo -ErrorAction SilentlyContinue).Count'], text=True).strip() == '0'
        env = os.environ.copy()
        env.update(PATH=str(BIN) + os.pathsep + env.get('PATH', ''), RUSTC=str(BIN / 'rustc.exe'), CARGO_BUILD_JOBS='1', CARGO_INCREMENTAL='0',
                   CARGO_TARGET_DIR=str(W / 'firepit-static-glb-product-check-v2/target'), TEMP=str(ROOT), TMP=str(ROOT))
        command = [str(BIN / 'cargo.exe'), 'run', '--locked', '--offline', '--', str(ROOT)]
        save('COMMAND.v1.json', {'command': command, 'cwd': str(ROOT)})
        with (ROOT / 'stdout.log').open('xb') as out, (ROOT / 'stderr.log').open('xb') as err:
            child = subprocess.Popen(command, cwd=ROOT, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='actual_bevy_pipeline_build_and_device_probe_running', cargo_pid=child.pid)
            save('RUN-RESULT.v1.json', state)
            state['actual_exit_code'] = child.wait()
            assert child.returncode == 0
        native = read(ROOT / 'NATIVE-RESULT.v1.json')
        for path, expected in prep['input_pins'].items():
            assert sha(path) == expected, path
        state.update(status=native['status'], pipelines_created=native['pipelines_created'], native_receipt_sha256=sha(ROOT / 'NATIVE-RESULT.v1.json'),
                     actual_resource_bind_groups_created=False, gpu_draw_executed=False, source_unity_render_parity_qualified=False, forge_editor_verified=False)
    except Exception as error:
        state.update(status='actual_bevy_pipeline_probe_refusal_retained', error=repr(error))
    state['finished_utc'] = datetime.now(timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
