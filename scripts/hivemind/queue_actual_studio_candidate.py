"""Build the real authoring candidate on F after the exact frontend finishes."""
import datetime
import json
import os
from pathlib import Path
import subprocess
import sys

from recover_native_scene_check_queue import wait_exact
from verify_native_material_snapshot import sha

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'studio-native-library-candidate-queue-v1'
OUTPUT = W / 'studio-native-library-candidate-v1'
FRONTEND = W / 'studio-actual-frontend-build-v2'
KIT = W / 'studio-candidate-input-preparation-v1/RESULT.v1.json'
SCRIPT = R / 'scripts/Build-StudioCandidate.ps1'
PS = Path('C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe')
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TOOLS = Path('C:/Users/karen/.cargo/bin')


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def save(name, value):
    target = ROOT / name
    temporary = target.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2), 'utf-8')
    temporary.replace(target)


def prepare():
    assert not ROOT.exists() and not OUTPUT.exists(), 'Existing candidate evidence preserved'
    kit = read(KIT)
    assert kit['status'] == 'existing_governed_kit_candidate_inputs_reopened' and kit['assets_reopened'] == 8
    pins = dict(kit['input_pins'])
    for path in [KIT, SCRIPT, PS, BIN / 'cargo.exe', BIN / 'rustc.exe', TOOLS / 'cargo-tauri.exe',
                 Path(__file__).resolve(), Path(__file__).with_name('recover_native_scene_check_queue.py'),
                 Path(__file__).with_name('verify_native_material_snapshot.py'), FRONTEND / 'PREPARED.v1.json']:
        pins[str(path)] = sha(path)
    assert all(sha(p) == h for p, h in pins.items()), 'Prepared input changed'
    ROOT.mkdir()
    (ROOT / 'temp').mkdir()
    save('PREPARED.v1.json', dict(input_pins=pins, frontend_pid=44760,
                                 frontend_birth=639269716279805785, output=str(OUTPUT), kit_root=kit['kit_root']))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=R,
                                  stdout=log, stderr=log, stdin=subprocess.DEVNULL,
                                  creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(pid=child.pid, started_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(pid=child.pid, output=str(OUTPUT), candidate_built=False)))


def run():
    state = dict(status='waiting_for_exact_actual_frontend_controller', candidate_built=False,
                 studio_verified=False, gpu_rendered=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        prep = read(ROOT / 'PREPARED.v1.json')
        wait_exact(prep['frontend_pid'], prep['frontend_birth'])
        frontend = read(FRONTEND / 'RUN-RESULT.v1.json')
        assert frontend['status'] == 'actual_studio_frontend_and_navigation_worker_built', frontend
        assert frontend['actual_exit_code'] == 0 and not frontend['changed_source_files']
        pins = {**prep['input_pins'], **frontend['input_pins'],
                str(FRONTEND / 'RUN-RESULT.v1.json'): sha(FRONTEND / 'RUN-RESULT.v1.json')}
        for relative, row in frontend['artifacts'].items():
            path = FRONTEND / 'dist' / relative
            assert path.resolve().is_relative_to((FRONTEND / 'dist').resolve())
            assert path.stat().st_size == row['bytes']
            pins[str(path)] = row['sha256']
        assert all(sha(p) == h for p, h in pins.items()), 'Frontend, source or tool changed'
        assert not OUTPUT.exists(), 'Candidate output already exists'
        wasm_sha = sha(FRONTEND / 'dist/forge_studio_bg.wasm')
        env = os.environ.copy()
        env.update(PATH=os.pathsep.join([str(BIN), str(TOOLS), str(PS.parent), env.get('PATH','')]),
                   RUSTC=str(BIN / 'rustc.exe'), CARGO_BUILD_JOBS='1', CARGO_INCREMENTAL='0',
                   CARGO_NET_OFFLINE='true', TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'), NO_COLOR='true')
        config_count = int(env.get('GIT_CONFIG_COUNT', '0'))
        env[f'GIT_CONFIG_KEY_{config_count}'] = 'safe.directory'
        env[f'GIT_CONFIG_VALUE_{config_count}'] = str(R).replace('\\','/')
        env['GIT_CONFIG_COUNT'] = str(config_count + 1)
        args = [str(PS), '-NoProfile', '-File', str(SCRIPT), '-Frontend', str(FRONTEND / 'dist'),
                '-ExpectedWasmSha256', wasm_sha, '-Output', str(OUTPUT), '-GovernedKit', prep['kit_root'],
                '-Authoring', '-BuildCacheDirectory', 'F:/OpenRPG_Forge/relocated-work/Rust-target',
                '-TemporaryDirectory', str(ROOT / 'temp'), '-ExpectedNavigationWorkerConfiguration', 'release']
        for stage, command in [('validating_actual_candidate_inputs', args + ['-ValidateOnly']),
                               ('building_actual_native_authoring_candidate', args)]:
            save(stage + '.COMMAND.v1.json', dict(command=command, cwd=str(R)))
            with (ROOT / (stage + '.stdout.log')).open('xb') as out, (ROOT / (stage + '.stderr.log')).open('xb') as err:
                child = subprocess.Popen(command, cwd=R, env=env, stdout=out, stderr=err,
                                         stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
                state.update(status=stage, powershell_pid=child.pid)
                save('RUN-RESULT.v1.json', state)
                code = child.wait()
            state[stage + '_exit_code'] = code
            assert code == 0, 'Candidate stage failed: ' + stage
            assert all(sha(p) == h for p, h in pins.items()), 'Input changed during candidate stage'
        proof = read(OUTPUT / 'build-proof.json')
        assert proof['status'] == 'BUILD_ONLY' and proof['authoring'] is True and proof['runtime_verified'] is False
        executable = OUTPUT / 'forge-studio-candidate.exe'
        assert sha(executable).lower() == proof['executable_sha256'].lower()
        with executable.open('rb') as stream:
            assert stream.read(2) == b'MZ', 'Native executable header absent'
        for pin in proof['frontend_pins']:
            path = OUTPUT / 'web' / pin['path']
            assert path.resolve().is_relative_to((OUTPUT / 'web').resolve())
            assert sha(path).lower() == pin['sha256'].lower()
        state.update(status='actual_native_authoring_candidate_built_and_reopened', candidate_built=True,
                     executable=str(executable), executable_sha256=sha(executable),
                     proof_sha256=sha(OUTPUT / 'build-proof.json'), input_pins=pins,
                     next_action='Actual Studio library search, images, preview and placement review; runtime qualification remains required')
    except Exception as error:
        state.update(status='candidate_build_refused', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
