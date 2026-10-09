"""Install the proven RGB8 candidate after exact live builds, then test the actual loader."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import sys

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-rgb8-canonical-loader-native-v1'
CANDIDATE = W / 'delta-rgb8-native-transport-candidate-v3'
GPU = W / 'delta-rgb8-native-transport-candidate-v4'
HOST = W / 'studio-source-frame-admission-native-v2'
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')


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
        assert sha(path) == digest, 'Changed input: ' + path


def birth(pid):
    out = subprocess.run([PS, '-NoProfile', '-Command',
        f'$p=Get-Process -Id {pid} -ErrorAction SilentlyContinue; if($p){{$p.StartTime.ToUniversalTime().Ticks}}'],
        capture_output=True, text=True, check=True, creationflags=subprocess.CREATE_NO_WINDOW).stdout.strip()
    assert out.isdigit(), 'Exact build must be currently live: ' + str(pid)
    return out


def prepare():
    assert not ROOT.exists()
    gpu = read(GPU / 'RUN-RESULT.v1.json')
    proof = read(GPU / 'INDEPENDENT-GPU-REOPEN.v1.json')
    regression_path = W / 'delta-rgb8-native-regressions-v1/RUN-RESULT.v1.json'
    regression = read(regression_path)
    assert gpu['native_rgb24_textures_verified'] == proof['native_rgb24_textures_verified'] == 3
    assert gpu['mips_verified'] == proof['mips_verified'] == 37
    assert sha(GPU / 'INDEPENDENT-GPU-REOPEN.v1.json') == gpu['independent_receipt_sha256']
    assert regression['actual_exit_code'] == 0 and regression['tests_passed'] == 15
    assert sha(regression_path.parent / 'native.log') == regression['native_log_sha256']
    host = read(HOST / 'PREPARED.v2.json')
    assert host['expected_tests'] == 17
    check(host['input_pins'])
    candidate = read(CANDIDATE / 'PREPARED.v1.json')
    check(candidate['input_pins'])
    files = candidate['candidate_files']
    assert len(files) == 3
    for path, row in files.items():
        assert sha(path) == row['original_sha256']
        assert sha(row['candidate']) == row['candidate_sha256']
    # Preserve both confirmed live compilations. A reused process ID is not a wait.
    waits = [dict(pid=pid, birth_ticks=birth(pid)) for pid in [28340, 9128]]
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    (ROOT / 'backup').mkdir()
    (ROOT / 'temp').mkdir()
    for name in ['Cargo.toml', 'Cargo.lock', 'src/main.rs']:
        shutil.copyfile(HOST / name, ROOT / name)
    pins = {str(p): sha(p) for p in [Path(__file__).resolve(),
        CANDIDATE / 'PREPARED.v1.json', GPU / 'RUN-RESULT.v1.json',
        GPU / 'INDEPENDENT-GPU-REOPEN.v1.json', regression_path,
        regression_path.parent / 'native.log', HOST / 'PREPARED.v2.json',
        ROOT / 'Cargo.toml', ROOT / 'Cargo.lock', ROOT / 'src/main.rs']}
    save('PREPARED.v1.json', dict(input_pins=pins, host_pins=host['input_pins'],
        candidate_files=files, exact_waits=waits, target=host['target'], expected_host_tests=17,
        canonical_renderer_installed=False, full_import=False), new=True)
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid,
         created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()), new=True)
    print(json.dumps(dict(controller_pid=child.pid, exact_builds_waited=[x['pid'] for x in waits],
         candidate_source_modules=3, expected_host_tests=17, canonical_renderer_installed=False)))


def run():
    state = dict(status='waiting_for_exact_existing_builds', canonical_renderer_installed=False,
                 actual_host_tests_passed=0, gpu_rendered=False, studio_verified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        check(prepared['input_pins'])
        for wait in prepared['exact_waits']:
            script = (f'while($true){{$p=Get-Process -Id {wait["pid"]} -ErrorAction SilentlyContinue;'
                f'if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne {wait["birth_ticks"]}){{break}};'
                'Start-Sleep -Seconds 5}; exit 0')
            subprocess.run([PS, '-NoProfile', '-Command', script], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        prior = read(HOST / 'RUN-RESULT.v2.json')
        assert prior['status'] == 'seventeen_actual_host_frame_admission_tests_passed'
        assert prior['actual_exit_code'] == 0 and prior['tests_passed'] == 17
        assert prior['all_input_pins_unchanged']
        assert sha(HOST / 'native.stdout.log') == prior['stdout_sha256']
        assert sha(HOST / 'native.stderr.log') == prior['stderr_sha256']
        check(prepared['host_pins'])
        check(prepared['input_pins'])
        transition = []
        for ordinal, (path, row) in enumerate(prepared['candidate_files'].items()):
            assert sha(path) == row['original_sha256']
            assert sha(row['candidate']) == row['candidate_sha256']
            backup = ROOT / 'backup' / f'{ordinal:02}-{Path(path).name}'
            shutil.copyfile(path, backup)
            assert sha(backup) == row['original_sha256']
            transition.append(dict(path=path, before_sha256=row['original_sha256'],
                after_sha256=row['candidate_sha256'], backup=str(backup), candidate=row['candidate']))
        save('SOURCE-TRANSITION-PREPARED.v1.json', dict(files=transition,
             prior_host_receipt_sha256=sha(HOST / 'RUN-RESULT.v2.json'), full_import=False), new=True)
        # Only these three checked modules change; each old byte sequence stays on F.
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
        pins = dict(prepared['host_pins'])
        for row in transition:
            pins[row['path']] = row['after_sha256']
            pins[row['backup']] = row['before_sha256']
            pins[row['candidate']] = row['after_sha256']
        for path in [ROOT / 'Cargo.toml', ROOT / 'Cargo.lock', ROOT / 'src/main.rs']:
            pins[str(path)] = sha(path)
        check(pins)
        save('SOURCE-TRANSITION-INSTALLED.v1.json', dict(files=transition, input_pins=pins,
             historical_receipts_preserved=True, canonical_renderer_installed=True,
             whole_loader_compiled=False, full_import=False), new=True)
        state.update(status='compiling_actual_loader_with_native_rgb8', canonical_renderer_installed=True,
             source_transition_sha256=sha(ROOT / 'SOURCE-TRANSITION-INSTALLED.v1.json'))
        save('RUN-RESULT.v1.json', state)
        env = os.environ.copy()
        env.update(RUSTC=str(BIN / 'rustc.exe'), CARGO_BUILD_JOBS='1',
                   TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'))
        env['PATH'] = str(BIN) + os.pathsep + env.get('PATH', '')
        with (ROOT / 'native.stdout.log').open('xb') as out, (ROOT / 'native.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(BIN / 'cargo.exe'), 'test', '--locked', '--offline',
                '--manifest-path', str(ROOT / 'Cargo.toml'), '--target-dir', prepared['target'],
                '-j1', '--', '--nocapture'], cwd=ROOT, env=env, stdout=out, stderr=err,
                stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid'] = child.pid
            save('RUN-RESULT.v1.json', state)
            state['actual_exit_code'] = child.wait()
        assert state['actual_exit_code'] == 0, 'Actual canonical loader compile/test refused; preserve logs'
        assert 'test result: ok. 17 passed; 0 failed;' in (ROOT / 'native.stdout.log').read_text('utf-8-sig')
        check(pins)
        state.update(status='native_rgb8_canonical_loader_compiled_and_seventeen_host_tests_passed',
            actual_host_tests_passed=17, all_input_pins_unchanged=True,
            stdout_sha256=sha(ROOT / 'native.stdout.log'), stderr_sha256=sha(ROOT / 'native.stderr.log'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
