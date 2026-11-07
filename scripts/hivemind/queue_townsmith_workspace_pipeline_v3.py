"""Run the actual layout probe within the product workspace, after common tests."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os
import subprocess
import sys
import time
from queue_townsmith_bevy_pipeline_v2 import live

S = Path(__file__).parent
R = S.parents[1]
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-s-paint-bevy-pipeline-v3'
INSTALL = W / 'normal-decode-swizzle-common-install-v1'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def save(name, data):
    p = ROOT / name
    temp = p.with_suffix('.tmp')
    temp.write_text(json.dumps(data, indent=2), encoding='utf-8')
    temp.replace(p)


def prepare():
    assert not ROOT.exists()
    pid = read(INSTALL / 'JOB.v1.json')['pid']
    assert live(pid) or read(INSTALL / 'RUN-RESULT.v1.json')['status'] == 'normal_swizzle_common_installed_workspace_tests_passed'
    exported_path = W / 'townsmith-s-paint-composed-wgsl-v1/RESULT.v1.json'
    exported = read(exported_path)
    assert exported['status'] == 'seven_real_bevy_compositions_exported_to_standalone_wgsl'
    pins = dict(exported['input_pins'])
    pins[str(exported_path)] = sha(exported_path)
    paths = [Path(__file__), S / 'queue_townsmith_bevy_pipeline_v2.py',
             S / 'candidates/SourceTownsmithBevyPipelineAdmissionV1.rs',
             R / 'crates/forge_engine/examples/verify_townsmith_material_layouts.rs', R / 'Cargo.toml', R / 'Cargo.lock', R / '.cargo/config.toml']
    for crate in ('forge_engine', 'forge_assets', 'forge_navigation'):
        paths += [R / 'crates' / crate / 'Cargo.toml']
        paths += list((R / 'crates' / crate / 'src').rglob('*.rs'))
    pins.update({str(p): sha(p) for p in paths})
    for p, h in pins.items():
        assert sha(p) == h, p
    ROOT.mkdir()
    save('INPUT.v1.json', {'cases': exported['cases']})
    pins[str(ROOT / 'INPUT.v1.json')] = sha(ROOT / 'INPUT.v1.json')
    save('PREPARED.v1.json', {'input_pins': pins, 'upstream_controller_pid': pid,
          'build_policy': 'Product workspace Cargo.lock, rerecast patch and opt-level profiles; no standalone dependency graph or cache deletion.', 'full_import': False})
    save('RUN-RESULT.v1.json', {'status': 'actual_bevy_workspace_probe_waiting_for_common_tests', 'upstream_controller_pid': pid, 'full_import': False, 'complete_assets_delivered_increment': 0})
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': child.pid})
    print(json.dumps({'pid': child.pid, 'root': str(ROOT), 'upstream_controller_pid': pid}))


def run():
    state = read(ROOT / 'RUN-RESULT.v1.json')
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        while live(prepared['upstream_controller_pid']):
            time.sleep(3)
        upstream = read(INSTALL / 'RUN-RESULT.v1.json')
        assert upstream['status'] == 'normal_swizzle_common_installed_workspace_tests_passed', upstream
        for label in ('assets', 'engine', 'unlit', 'program', 'glb'):
            assert upstream[label + '_exit_code'] == 0
        while subprocess.check_output(['powershell', '-NoProfile', '-Command', '@(Get-Process cargo -ErrorAction SilentlyContinue).Count'], text=True).strip() != '0':
            state['status'] = 'actual_bevy_workspace_probe_waiting_for_existing_cargo'
            save('RUN-RESULT.v1.json', state)
            time.sleep(10)
        for p, h in prepared['input_pins'].items():
            assert sha(p) == h, p
        env = os.environ.copy()
        env.update(PATH=str(BIN) + os.pathsep + env.get('PATH', ''), RUSTC=str(BIN / 'rustc.exe'), CARGO_BUILD_JOBS='1', CARGO_INCREMENTAL='0', CARGO_TARGET_DIR=str(W / 'firepit-static-glb-product-check-v2/target'), TEMP=str(ROOT), TMP=str(ROOT))
        command = [str(BIN / 'cargo.exe'), 'run', '--verbose', '--locked', '--offline', '-p', 'forge_engine', '--example', 'verify_townsmith_material_layouts', '--', str(ROOT)]
        save('COMMAND.v1.json', {'command': command, 'cwd': str(R)})
        with (ROOT / 'stdout.log').open('xb') as out, (ROOT / 'stderr.log').open('xb') as err:
            child = subprocess.Popen(command, cwd=R, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='actual_bevy_workspace_probe_build_and_device_running', cargo_pid=child.pid)
            save('RUN-RESULT.v1.json', state)
            state['actual_exit_code'] = child.wait()
            assert child.returncode == 0
        native = read(ROOT / 'NATIVE-RESULT.v1.json')
        for p, h in prepared['input_pins'].items():
            assert sha(p) == h, p
        state.update(status=native['status'], pipelines_created=native['pipelines_created'], native_receipt_sha256=sha(ROOT / 'NATIVE-RESULT.v1.json'), actual_resource_bind_groups_created=False, gpu_draw_executed=False, source_unity_render_parity_qualified=False, forge_editor_verified=False)
    except Exception as error:
        state.update(status='actual_bevy_workspace_probe_refusal_retained', error=repr(error), traceback=__import__('traceback').format_exc())
    state['finished_utc'] = datetime.now(timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
