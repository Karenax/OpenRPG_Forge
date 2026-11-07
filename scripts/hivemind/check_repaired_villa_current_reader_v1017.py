"""Build the common checkout reader and admit only the completed, pinned native derivative lot."""
import json
import os
import subprocess
import time
from pathlib import Path

from assemble_native_villa_delivery import read, sha, require, now, digest

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W = Path('F:/OpenRPG_ImportWork')
O = W / 'villa-repaired-current-canonical-reader-v1017'
NATIVE = W / 'villa-repaired-native-storage-and-frames-v1015'
SDK = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TARGET = Path('F:/OpenRPG_Forge/relocated-work/Rust-target')
EXAMPLE = 'verify_native_source_scene_manifest'


def save(name, value):
    p = O / name
    temporary = p.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2), 'utf-8')
    os.replace(temporary, p)


def main():
    require(not O.exists(), 'Existing reader job preserved')
    O.mkdir()
    state = dict(status='PINNING_CURRENT_COMMON_CHECKOUT', started_utc=now(), full_import=False,
                 registry_modified=False, gpu_rendered=False, studio_verified=False)
    save('RESULT.v1.json', state)
    save('JOB.v1.json', dict(pid=os.getpid(), started_utc=now(), script=str(Path(__file__).resolve())))
    try:
        inventory = subprocess.check_output(['git', '-C', str(R), 'ls-files', '-c', '-o', '--exclude-standard', '-z'])
        dirty = subprocess.check_output(['git', '-C', str(R), 'status', '--porcelain=v1', '-z'])
        (O / 'checkout-input-paths.zlist').write_bytes(inventory)
        (O / 'checkout-status.zlist').write_bytes(dirty)
        paths = set(p.decode('utf-8') for p in inventory.split(b'\x00') if p)
        selected = {p for p in paths if p.startswith(('crates/', 'third_party/', '.cargo/'))}
        selected.update({'Cargo.toml', 'Cargo.lock', 'rust-toolchain.toml'})
        pins = {}
        for rel in sorted(selected):
            p = R / rel
            require(p.is_file() and not p.is_symlink(), 'Source input missing or linked: ' + rel)
            pins[str(p)] = sha(p)
        for p in (Path(__file__).resolve(), SDK / 'cargo.exe', SDK / 'rustc.exe', SDK / 'rustdoc.exe'):
            pins[str(p)] = sha(p)
        require(str(R / ('crates/forge_engine/examples/' + EXAMPLE + '.rs')) in pins, 'Current verifier input missing')
        save('PREPARED.v1.json', dict(created_utc=now(), compile_input_pins=pins,
                     tracked_dirty_untracked_inventory_sha256=digest(inventory), status_sha256=digest(dirty),
                     common_checkout=str(R), target=str(TARGET),
                     compile_scope='Rust crates and local third-party inputs; full git path/status inventory retained',
                     whole_checkout_qualification=False, full_import=False))
        env = os.environ.copy()
        env.update(RUSTC=str(SDK / 'rustc.exe'), RUSTDOC=str(SDK / 'rustdoc.exe'),
                   CARGO_TARGET_DIR=str(TARGET), TEMP=str(W), TMP=str(W))
        state.update(status='BUILDING_CURRENT_CANONICAL_READER', compile_inputs_pinned=len(pins))
        save('RESULT.v1.json', state)
        args = [str(SDK / 'cargo.exe'), 'build', '--locked', '--offline', '-p', 'forge_engine', '--example', EXAMPLE]
        with (O / 'build.stdout.log').open('wb') as out, (O / 'build.stderr.log').open('wb') as err:
            process = subprocess.Popen(args, cwd=R, env=env, stdout=out, stderr=err,
                                       creationflags=subprocess.CREATE_NO_WINDOW)
            save('BUILD-JOB.v1.json', dict(pid=process.pid, command=args, started_utc=now()))
            require(process.wait() == 0, 'Current canonical reader build refused; see build.stderr.log')
        for p, expected in pins.items():
            require(sha(p) == expected, 'Compile input changed during build: ' + p)
        state.update(status='WAITING_FOR_NATIVE_DERIVATIVE_LOT', build_exit_code=0)
        save('RESULT.v1.json', state)
        while True:
            result = read(NATIVE / 'RESULT.v1.json')
            if result['status'] != 'RUNNING':
                break
            pid = read(NATIVE / 'JOB.v1.json')['pid']
            command = ['powershell.exe', '-NoProfile', '-NonInteractive', '-Command',
                       'if (Get-Process -Id ' + str(int(pid)) + ' -ErrorAction SilentlyContinue) {exit 0} else {exit 1}']
            require(subprocess.call(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                    creationflags=subprocess.CREATE_NO_WINDOW) == 0,
                    'Native lot still marked RUNNING but its process is absent; inspect evidence, do not restart')
            time.sleep(2)
        require(result['status'] == '19_NATIVE_STORAGE_AND_CAPTURED_FRAMES_REOPENED' and
                len(result['rows']) == result['verified_scenes'] == 19, 'Native derivative lot incomplete/refused')
        source = NATIVE / 'INPUTS.v1.json'
        source_sha = sha(source)
        rows = read(source)
        require(len(rows) == len({r['asset_id'] for r in rows}) == 19, 'Exact 19-scene input scope required')
        exe = TARGET / 'debug/examples' / (EXAMPLE + '.exe')
        exe_sha = sha(exe)
        state.update(status='READING_19_NATIVE_SCENES', input_manifest_sha256=source_sha, executable_sha256=exe_sha)
        save('RESULT.v1.json', state)
        with (O / 'reader.stdout.jsonl').open('wb') as out, (O / 'reader.stderr.log').open('wb') as err:
            process = subprocess.Popen([str(exe), str(source)], cwd=R, env=env, stdout=out, stderr=err,
                                       creationflags=subprocess.CREATE_NO_WINDOW)
            save('READER-JOB.v1.json', dict(pid=process.pid, started_utc=now(), executable_sha256=exe_sha))
            require(process.wait() == 0, 'Current canonical reader refused a native derivative; see reader.stderr.log')
        lines = [json.loads(line) for line in (O / 'reader.stdout.jsonl').read_text('utf-8').splitlines() if line.strip()]
        summary = lines[-1]
        require(summary['canonical_scenes'] == 19 and summary['negative_controls_passed'] == 2, 'Reader coverage mismatch')
        require({r['asset_id'] for r in lines[:-1]} == {r['asset_id'] for r in rows}, 'Reader scene identities differ')
        require(sha(source) == source_sha and sha(exe) == exe_sha, 'Manifest or reader changed')
        for p, expected in pins.items():
            require(sha(p) == expected, 'Compile input changed during reader validation: ' + p)
        for row in rows:
            require(sha(row['glb']) == row['sha256'], 'Native derivative changed after reader')
        state.update(status='19_CURRENT_CANONICAL_SCENES_ADMITTED', reader_exit_code=0,
                     coverage=summary, native_receipt_sha256=sha(NATIVE / 'RESULT.v1.json'),
                     next_action='Qualify full source material rendering and placement/save/runtime before Vault admission')
    except Exception as error:
        state.update(status='FAILED', error=str(error))
        raise
    finally:
        state['finished_or_last_update_utc'] = now()
        save('RESULT.v1.json', state)


if __name__ == '__main__':
    main()
