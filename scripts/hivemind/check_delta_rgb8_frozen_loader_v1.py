"""Compile real frozen engine/Studio sources, preserving concurrent Tidewater edits."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tomllib

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-rgb8-frozen-canonical-loader-v1'
SNAPSHOT = ROOT / 'snapshot'
REPO = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
HOST = W / 'studio-source-frame-admission-native-v2'
CANDIDATE = W / 'delta-rgb8-native-transport-candidate-v3'
GPU = W / 'delta-rgb8-native-transport-candidate-v4'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def save(name, value, new=False):
    with (ROOT / name).open('x' if new else 'w', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False)


def check(pins):
    for path, digest in pins.items():
        assert sha(path) == digest, 'Changed frozen input: ' + path


def identity(pid):
    output = subprocess.run([PS, '-NoProfile', '-Command',
        f'$p=Get-Process -Id {pid} -ErrorAction SilentlyContinue; if($p){{$p.StartTime.ToUniversalTime().Ticks}}'],
        check=True, capture_output=True, text=True, creationflags=subprocess.CREATE_NO_WINDOW).stdout.strip()
    assert output.isdigit(), 'Prior build must be confirmed live'
    return output


def prepare():
    assert not ROOT.exists()
    # Native RGB24 transport and fifteen existing regressions are prerequisites.
    gpu = read(GPU / 'RUN-RESULT.v1.json')
    regression_path = W / 'delta-rgb8-native-regressions-v1/RUN-RESULT.v1.json'
    regression = read(regression_path)
    assert gpu['native_rgb24_textures_verified'] == 3 and gpu['mips_verified'] == 37
    assert sha(GPU / 'INDEPENDENT-GPU-REOPEN.v1.json') == gpu['independent_receipt_sha256']
    assert regression['actual_exit_code'] == 0 and regression['tests_passed'] == 15
    assert sha(regression_path.parent / 'native.log') == regression['native_log_sha256']
    candidate = read(CANDIDATE / 'PREPARED.v1.json')
    check(candidate['input_pins'])
    expected_files = candidate['candidate_files']
    assert len(expected_files) == 3
    previous_pins = read(HOST / 'PREPARED.v2.json')['input_pins']
    drift = [dict(path=p, expected=d, observed=sha(p)) for p, d in previous_pins.items() if sha(p) != d]
    assert drift and all('/tidewater_' in r['path'].replace('\\', '/') for r in drift)
    birth = identity(28340)
    roots = [REPO / 'crates', REPO / 'third_party/rerecast-0.4.0']
    files = sorted(p for root in roots for p in root.rglob('*') if p.is_file()
                   and not any(v in p.parts for v in ['target', '.git', 'node_modules']))
    assert 500 < len(files) < 1500 and sum(p.stat().st_size for p in files) < 64*1024*1024
    studio_names = ['source_graph_scene', 'source_graph_resources', 'source_graph_frame',
                    'source_graph_intake', 'source_graph_host']
    studio = [REPO / 'apps/forge_studio/src/authoring_assets' / (n + '.rs') for n in studio_names]
    files += studio + [REPO / 'Cargo.toml', REPO / 'Cargo.lock']
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    (ROOT / 'temp').mkdir()
    origin_pins, snapshot_pins, source_rows = {}, {}, []
    for source in files:
        digest = sha(source)
        destination = SNAPSHOT / source.relative_to(REPO)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        assert sha(destination) == digest == sha(source), 'Source changed during copy: ' + str(source)
        origin_pins[str(source)] = digest
        snapshot_pins[str(destination)] = digest
        source_rows.append(dict(original=str(source), copied=str(destination), sha256=digest))
    check(origin_pins)  # Coherent snapshot before allowing later development.
    root_manifest = SNAPSHOT / 'Cargo.toml'
    manifest = root_manifest.read_text('utf-8-sig')
    for member in ['apps/forge_runtime', 'apps/forge_studio', 'tools/forge_cli']:
        line = f'    "{member}",\n'
        assert manifest.count(line) == 1
        manifest = manifest.replace(line, '')
    root_manifest.write_text(manifest, 'utf-8')
    original_toml = tomllib.loads((REPO / 'Cargo.toml').read_text('utf-8-sig'))
    copied_toml = tomllib.loads(manifest)
    copied_toml['workspace']['members'] = original_toml['workspace']['members']
    assert original_toml == copied_toml, 'Root manifest changed beyond excluded app members'
    snapshot_pins[str(root_manifest)] = sha(root_manifest)
    transitions = []
    for path, row in expected_files.items():
        assert origin_pins[path] == row['original_sha256'] and sha(row['candidate']) == row['candidate_sha256']
        destination = SNAPSHOT / Path(path).relative_to(REPO)
        original_copy = destination.with_suffix(destination.suffix + '.original')
        shutil.copyfile(destination, original_copy)
        shutil.copyfile(row['candidate'], destination)
        snapshot_pins[str(original_copy)] = row['original_sha256']
        snapshot_pins[str(destination)] = row['candidate_sha256']
        transitions.append(dict(original=path, frozen=str(destination), original_copy=str(original_copy), **row))
    # Real original five Studio modules, now pointing to the frozen real engine.
    original_main = (HOST / 'src/main.rs').read_text('utf-8-sig')
    assert original_main.count(REPO.as_posix()) == 5
    (ROOT / 'src/main.rs').write_text(original_main.replace(REPO.as_posix(), SNAPSHOT.as_posix()), 'utf-8')
    probe_manifest = (HOST / 'Cargo.toml').read_text('utf-8-sig')
    assert probe_manifest.count(REPO.as_posix()) == 2
    probe_manifest = probe_manifest.replace(REPO.as_posix(), SNAPSHOT.as_posix())
    # Reproduce the actual application's local rerecast fork, not registry fallback.
    probe_manifest += '\n[patch.crates-io]\nrerecast={path="' + (SNAPSHOT / 'third_party/rerecast-0.4.0').as_posix() + '"}\n'
    (ROOT / 'Cargo.toml').write_text(probe_manifest, 'utf-8')
    shutil.copyfile(SNAPSHOT / 'Cargo.lock', ROOT / 'Cargo.lock')
    env = os.environ.copy()
    env.update(TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'))
    with (ROOT / 'lock.log').open('xb') as log:
        result = subprocess.run([str(BIN / 'cargo.exe'), 'generate-lockfile', '--offline'], cwd=ROOT,
            env=env, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    assert result.returncode == 0, 'Frozen dependency resolution refused; preserve lock.log'
    actual = tomllib.loads((ROOT / 'Cargo.lock').read_text('utf-8-sig'))
    before = tomllib.loads((SNAPSHOT / 'Cargo.lock').read_text('utf-8-sig'))
    identities = {(p['name'], p['version'], p.get('source'), p.get('checksum')) for p in before['package']}
    added_registry = [p for p in actual['package'] if p.get('source') and
        (p['name'], p['version'], p.get('source'), p.get('checksum')) not in identities]
    assert not added_registry, 'Dependency identity drift outside actual application lock: ' + repr(added_registry)
    for path in [Path(__file__).resolve(), ROOT / 'Cargo.toml', ROOT / 'Cargo.lock', ROOT / 'src/main.rs',
                 GPU / 'RUN-RESULT.v1.json', GPU / 'INDEPENDENT-GPU-REOPEN.v1.json', regression_path]:
        snapshot_pins[str(path)] = sha(path)
    check(snapshot_pins)
    save('SNAPSHOT.v1.json', dict(original_sources=source_rows, original_input_pins=origin_pins,
        frozen_input_pins=snapshot_pins, candidate_transitions=transitions,
        prior_live_build_changed_inputs=drift, excluded_workspace_members_only=['apps/forge_runtime','apps/forge_studio','tools/forge_cli'],
        local_rerecast_fork_preserved=True, registry_dependencies_match_actual_application_lock=True,
        expected_host_tests=17, canonical_checkout_unchanged=True, full_import=False), new=True)
    target = read(HOST / 'PREPARED.v2.json')['target']
    save('PREPARED.v1.json', dict(input_pins=snapshot_pins, wait_pid=28340, wait_birth_ticks=birth,
         target=target, actual_source_files_copied=len(source_rows), expected_tests=17,
         canonical_checkout_unchanged=True, full_import=False), new=True)
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid, created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()), new=True)
    print(json.dumps(dict(controller_pid=child.pid, frozen_actual_sources=len(source_rows), expected_tests=17,
         canonical_checkout_unchanged=True, full_import=False)))


def run():
    state = dict(status='waiting_for_exact_existing_host_build', actual_host_tests_passed=0,
        frozen_actual_engine_and_studio_sources=True, canonical_renderer_installed=False,
        gpu_rendered=False, studio_verified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        check(prepared['input_pins'])
        script = (f'while($true){{$p=Get-Process -Id {prepared["wait_pid"]} -ErrorAction SilentlyContinue;'
            f'if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne {prepared["wait_birth_ticks"]}){{break}};'
            'Start-Sleep -Seconds 5}; exit 0')
        subprocess.run([PS, '-NoProfile', '-Command', script], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        env = os.environ.copy()
        env.update(RUSTC=str(BIN / 'rustc.exe'), CARGO_BUILD_JOBS='1', TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'))
        env['PATH'] = str(BIN) + os.pathsep + env.get('PATH', '')
        state['status'] = 'compiling_frozen_actual_loader_with_native_rgb8'
        save('RUN-RESULT.v1.json', state)
        with (ROOT / 'native.stdout.log').open('xb') as out, (ROOT / 'native.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(BIN / 'cargo.exe'), 'test', '--offline', '--locked',
                '--manifest-path', str(ROOT / 'Cargo.toml'), '--target-dir', prepared['target'],
                '-j1', '--', '--nocapture'], cwd=ROOT, env=env, stdout=out, stderr=err,
                stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid'] = child.pid
            save('RUN-RESULT.v1.json', state)
            state['actual_exit_code'] = child.wait()
        assert state['actual_exit_code'] == 0, 'Frozen actual engine/test compilation refused'
        output = (ROOT / 'native.stdout.log').read_text('utf-8-sig')
        assert 'test result: ok. 17 passed; 0 failed;' in output
        check(prepared['input_pins'])
        state.update(status='frozen_native_rgb8_loader_and_seventeen_actual_host_tests_passed',
             actual_host_tests_passed=17, all_frozen_input_pins_unchanged=True,
             snapshot_sha256=sha(ROOT / 'SNAPSHOT.v1.json'),
             stdout_sha256=sha(ROOT / 'native.stdout.log'), stderr_sha256=sha(ROOT / 'native.stderr.log'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
