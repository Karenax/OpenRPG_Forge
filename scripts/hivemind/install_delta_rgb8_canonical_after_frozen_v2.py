"""Guarded RGB8 source installation; qualify current sources separately from frozen proof."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import sys

W = Path('F:/OpenRPG_ImportWork')
REPO = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
ROOT = W / 'delta-rgb8-canonical-loader-native-v2'
FROZEN = W / 'delta-rgb8-frozen-canonical-loader-v5'
SNAP = W / 'delta-rgb8-frozen-canonical-loader-v1/snapshot'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def check(pins):
    for path, digest in pins.items():
        assert sha(path) == digest, 'Changed input: ' + path


def save(name, value, new=False):
    target = ROOT / name
    if new:
        with target.open('x', encoding='utf-8') as stream:
            json.dump(value, stream, indent=2, allow_nan=False)
    else:
        temporary = target.with_suffix('.tmp')
        temporary.write_text(json.dumps(value, indent=2, allow_nan=False), 'utf-8')
        os.replace(temporary, target)


def idle_compilers():
    result = subprocess.run([PS, '-NoProfile', '-Command',
        'Get-Process -Name cargo,rustc -ErrorAction SilentlyContinue | ForEach-Object {$_.Id}'],
        capture_output=True, text=True, creationflags=subprocess.CREATE_NO_WINDOW)
    assert not result.stdout.strip(), 'Existing compilations preserved; retry after their completion'


def prepare():
    assert not ROOT.exists(), 'Existing delivery must be reopened, not overwritten'
    idle_compilers()
    proof = read(FROZEN / 'RUN-RESULT.v1.json')
    assert proof['actual_exit_code'] == 0 and proof['actual_host_tests_passed'] == 17
    assert proof['all_frozen_input_pins_unchanged']
    for name, key in [('SNAPSHOT.v1.json', 'snapshot_sha256'),
                      ('native.stdout.log', 'stdout_sha256'), ('native.stderr.log', 'stderr_sha256')]:
        assert sha(FROZEN / name) == proof[key]
    check(read(FROZEN / 'PREPARED.v1.json')['input_pins'])
    candidate_path = W / 'delta-rgb8-native-transport-candidate-v3/PREPARED.v1.json'
    candidate = read(candidate_path)
    check(candidate['input_pins'])
    gpu_root = W / 'delta-rgb8-native-transport-candidate-v4'
    gpu = read(gpu_root / 'RUN-RESULT.v1.json')
    assert gpu['native_rgb24_textures_verified'] == 3 and gpu['mips_verified'] == 37
    assert sha(gpu_root / 'INDEPENDENT-GPU-REOPEN.v1.json') == gpu['independent_receipt_sha256']
    regression_root = W / 'delta-rgb8-native-regressions-v1'
    regression = read(regression_root / 'RUN-RESULT.v1.json')
    assert regression['actual_exit_code'] == 0 and regression['tests_passed'] == 15
    assert sha(regression_root / 'native.log') == regression['native_log_sha256']
    files = candidate['candidate_files']
    assert len(files) == 3
    for path, row in files.items():
        assert sha(path) == row['original_sha256']
        assert sha(row['candidate']) == row['candidate_sha256']
        assert sha(SNAP / Path(path).relative_to(REPO)) == row['candidate_sha256']
    ROOT.mkdir()
    for name in ['src', 'backup', 'temp']:
        (ROOT / name).mkdir()
    # The exact registry lock is retained. Only local path dependencies change.
    manifest = (FROZEN / 'Cargo.toml').read_text('utf-8-sig').replace(SNAP.as_posix(), REPO.as_posix())
    main = (FROZEN / 'src/main.rs').read_text('utf-8-sig').replace(SNAP.as_posix(), REPO.as_posix())
    assert SNAP.as_posix() not in manifest + main
    (ROOT / 'Cargo.toml').write_text(manifest, 'utf-8')
    (ROOT / 'src/main.rs').write_text(main, 'utf-8')
    shutil.copyfile(FROZEN / 'Cargo.lock', ROOT / 'Cargo.lock')
    pins = {str(p): sha(p) for p in [Path(__file__).resolve(), candidate_path,
        FROZEN / 'RUN-RESULT.v1.json', FROZEN / 'native.stdout.log', FROZEN / 'native.stderr.log',
        gpu_root / 'RUN-RESULT.v1.json', gpu_root / 'INDEPENDENT-GPU-REOPEN.v1.json',
        regression_root / 'RUN-RESULT.v1.json', ROOT / 'Cargo.toml', ROOT / 'Cargo.lock', ROOT / 'src/main.rs']}
    # Pin current corresponding source trees, not historical Tidewater hashes.
    for directory in [REPO / 'crates', REPO / 'third_party/rerecast-0.4.0',
                      REPO / 'apps/forge_studio/src/authoring_assets']:
        for path in directory.rglob('*'):
            if path.is_file() and (path.suffix in {'.rs', '.wgsl', '.toml'} or path.name == 'Cargo.lock'):
                pins[str(path)] = sha(path)
    for path in [REPO / 'Cargo.toml', REPO / 'Cargo.lock']:
        pins[str(path)] = sha(path)
    # Retain every literal embedded resource from the tested frozen tree.
    for path in read(FROZEN / 'PREPARED.v1.json')['input_pins']:
        p = Path(path)
        if p.is_relative_to(SNAP) and p.suffix not in {'.rs', '.toml', '.lock'}:
            current = REPO / p.relative_to(SNAP)
            if current.is_file():
                pins[str(current)] = sha(current)
    save('PREPARED.v1.json', dict(input_pins=pins, candidate_files=files,
        target=read(FROZEN / 'PREPARED.v1.json')['target'], expected_tests=17,
        frozen_proof_preserved=True, full_import=False), new=True)
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdin=subprocess.DEVNULL, stdout=log, stderr=log,
            creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid,
        created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()), new=True)
    print(json.dumps(dict(controller_pid=child.pid, guarded_source_modules=3,
        expected_host_tests=17, receipt_root=str(ROOT), full_import=False)))


def run():
    state = dict(status='checking_current_sources_before_guarded_install',
        canonical_renderer_installed=False, actual_host_tests_passed=0,
        gpu_rendered=False, studio_verified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        check(prepared['input_pins'])
        idle_compilers()
        transition = []
        for ordinal, (path, row) in enumerate(prepared['candidate_files'].items()):
            assert sha(path) == row['original_sha256']
            assert sha(row['candidate']) == row['candidate_sha256']
            backup = ROOT / 'backup' / f'{ordinal:02}-{Path(path).name}'
            shutil.copyfile(path, backup)
            assert sha(backup) == row['original_sha256']
            transition.append(dict(path=path, before_sha256=row['original_sha256'],
                after_sha256=row['candidate_sha256'], backup=str(backup), candidate=row['candidate']))
        save('SOURCE-TRANSITION-PREPARED.v1.json', dict(files=transition, full_import=False), new=True)
        check(prepared['input_pins'])
        for row in transition:
            path = Path(row['path'])
            assert sha(path) == row['before_sha256']
            temporary = path.with_name(path.name + '.rgb8-import.tmp')
            with temporary.open('xb') as stream:
                stream.write(Path(row['candidate']).read_bytes())
                stream.flush()
                os.fsync(stream.fileno())
            assert sha(temporary) == row['after_sha256'] and sha(path) == row['before_sha256']
            os.replace(temporary, path)
        pins = dict(prepared['input_pins'])
        for row in transition:
            pins[row['path']] = row['after_sha256']
            pins[row['backup']] = row['before_sha256']
            pins[row['candidate']] = row['after_sha256']
        check(pins)
        save('SOURCE-TRANSITION-INSTALLED.v1.json', dict(files=transition, input_pins=pins,
            canonical_renderer_installed=True, whole_loader_compiled=False, full_import=False), new=True)
        state.update(status='compiling_current_canonical_loader_with_native_rgb8',
            canonical_renderer_installed=True,
            source_transition_sha256=sha(ROOT / 'SOURCE-TRANSITION-INSTALLED.v1.json'))
        save('RUN-RESULT.v1.json', state)
        env = os.environ.copy()
        env.update(RUSTC=str(BIN / 'rustc.exe'), CARGO_BUILD_JOBS='1',
                   TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'), CARGO_INCREMENTAL='0')
        env['PATH'] = str(BIN) + os.pathsep + env.get('PATH', '')
        with (ROOT / 'native.stdout.log').open('xb') as out, (ROOT / 'native.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(BIN / 'cargo.exe'), 'test', '--locked', '--offline',
                '--manifest-path', str(ROOT / 'Cargo.toml'), '--target-dir', prepared['target'],
                '-j1', '--', '--nocapture'], cwd=ROOT, env=env, stdout=out, stderr=err,
                stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid'] = child.pid
            save('RUN-RESULT.v1.json', state)
            state['actual_exit_code'] = child.wait()
        assert state['actual_exit_code'] == 0, 'Current canonical loader compile/test refused; preserve logs'
        assert 'test result: ok. 17 passed; 0 failed;' in (ROOT / 'native.stdout.log').read_text('utf-8-sig')
        check(pins)
        state.update(status='current_canonical_rgb8_loader_and_seventeen_host_tests_passed',
            actual_host_tests_passed=17, all_current_input_pins_unchanged=True,
            stdout_sha256=sha(ROOT / 'native.stdout.log'), stderr_sha256=sha(ROOT / 'native.stderr.log'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
