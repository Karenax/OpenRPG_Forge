"""Check actual Studio frame admission after the currently live native build."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys

REPO = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
ROOT = Path('F:/OpenRPG_ImportWork/studio-source-frame-admission-native-v2')
PRIOR = Path('F:/OpenRPG_ImportWork/studio-source-scene-host-native-v1')
TARGET = Path('F:/OpenRPG_Forge/relocated-work/Rust-target')
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(name, value):
    path = ROOT / name
    temp = path.with_suffix('.tmp')
    temp.write_text(json.dumps(value, indent=2), 'utf-8')
    temp.replace(path)


def identity(pid):
    script = f'$p=Get-Process -Id {int(pid)} -ErrorAction SilentlyContinue; if($p){{$p.StartTime.ToUniversalTime().Ticks}}; exit 0'
    return subprocess.check_output([PS, '-NoProfile', '-Command', script], text=True,
        creationflags=subprocess.CREATE_NO_WINDOW).strip()


def check(pins):
    for path, expected in pins.items():
        assert sha(path) == expected, 'Native input changed: ' + path


def prepare():
    assert not ROOT.exists()
    # This is the live Cargo build inspected before preparation; never kill it.
    wait_pid = 16372
    birth = identity(wait_pid)
    assert birth.isdigit(), 'Current native build identity required'
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    (ROOT / 'temp').mkdir()
    manifest = (PRIOR / 'Cargo.toml').read_text('utf-8-sig').replace(
        'openrpg-studio-source-scene-host-check', 'openrpg-studio-source-frame-admission-check')
    manifest += '\n[profile.dev.package.openrpg-studio-source-frame-admission-check]\ndebug=0\n[profile.dev.package.forge_engine]\ndebug=0\n'
    (ROOT / 'Cargo.toml').write_text(manifest, 'utf-8')
    modules = ['source_graph_scene', 'source_graph_resources', 'source_graph_frame', 'source_graph_intake', 'source_graph_host']
    paths = [REPO / 'apps/forge_studio/src/authoring_assets' / (name + '.rs') for name in modules]
    expected_tests = sum(len(re.findall(r'#\[test\]', path.read_text('utf-8-sig'))) for path in paths)
    assert expected_tests == 17
    (ROOT / 'src/main.rs').write_text('\n'.join(f'#[allow(dead_code)]\n#[path="{path.as_posix()}"]\nmod {name};'
        for name, path in zip(modules, paths)) + '\nfn main(){}\n', 'utf-8')
    # Preserve the old checks and pin the actual current sources. These are not
    # narrowed replicas of the Studio modules or the forge_engine dependency.
    pins = {str(path): sha(path) for path in [Path(__file__).resolve(), ROOT / 'Cargo.toml', ROOT / 'src/main.rs',
        REPO / 'Cargo.lock', PRIOR / 'RUN-RESULT.v1.json', *paths]}
    for package in ['forge_engine', 'forge_assets']:
        for path in (REPO / 'crates' / package / 'src').rglob('*'):
            if path.is_file() and path.suffix in ['.rs', '.wgsl']:
                pins[str(path)] = sha(path)
        manifest_path = REPO / 'crates' / package / 'Cargo.toml'
        pins[str(manifest_path)] = sha(manifest_path)
    save('PREPARED.v2.json', dict(input_pins=pins, wait_pid=wait_pid, wait_birth_ticks=birth,
        expected_tests=expected_tests, target=str(TARGET), new_frame_admission_tests=3,
        actual_studio_modules=True, actual_forge_engine_dependency=True, gpu_rendered=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            stdout=log, stderr=log, stdin=subprocess.DEVNULL, cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v2.json', dict(controller_pid=child.pid, prepared_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(controller_pid=child.pid, expected_tests=17, status='queued_after_current_native_build')))


def run():
    prepared = json.loads((ROOT / 'PREPARED.v2.json').read_text('utf-8-sig'))
    state = dict(status='waiting_for_exact_current_native_build', tests_passed=0,
        gpu_rendered=False, studio_verified=False, full_import=False)
    save('RUN-RESULT.v2.json', state)
    try:
        script = (f'while($true){{$p=Get-Process -Id {prepared["wait_pid"]} -ErrorAction SilentlyContinue;'
            f'if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne {prepared["wait_birth_ticks"]}){{break}};'
            'Start-Sleep -Seconds 10}; exit 0')
        subprocess.run([PS, '-NoProfile', '-Command', script], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        check(prepared['input_pins'])
        environment = os.environ.copy()
        environment.update(RUSTC=str(BIN / 'rustc.exe'), CARGO_BUILD_JOBS='1', TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'))
        environment['PATH'] = str(BIN) + os.pathsep + environment.get('PATH', '')
        state['status'] = 'compiling_actual_engine_and_seventeen_studio_host_tests'
        save('RUN-RESULT.v2.json', state)
        with (ROOT / 'native.stdout.log').open('xb') as out, (ROOT / 'native.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(BIN / 'cargo.exe'), 'test', '--offline', '--manifest-path', str(ROOT / 'Cargo.toml'),
                '--target-dir', str(TARGET), '--', '--nocapture'], stdout=out, stderr=err, cwd=ROOT, env=environment,
                stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid'] = child.pid
            save('RUN-RESULT.v2.json', state)
            code = child.wait()
        state['actual_exit_code'] = code
        assert code == 0, 'Actual native host compile/tests refused; logs preserved'
        output = (ROOT / 'native.stdout.log').read_text('utf-8-sig')
        assert 'test result: ok. 17 passed; 0 failed;' in output
        for name in ['projective_and_mismatched_world_frames_refuse_before_material_update',
            'singular_draw_instance_refuses_before_material_update', 'rotated_scaled_draw_and_camera_origin_keep_explicit_frame']:
            assert name + ' ... ok' in output
        check(prepared['input_pins'])
        state.update(status='seventeen_actual_host_frame_admission_tests_passed', tests_passed=17,
            input_pins=prepared['input_pins'], all_input_pins_unchanged=True,
            stdout_sha256=sha(ROOT / 'native.stdout.log'), stderr_sha256=sha(ROOT / 'native.stderr.log'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v2.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
