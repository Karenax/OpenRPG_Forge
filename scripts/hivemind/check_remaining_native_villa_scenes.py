"""Admit each remaining physical batch with the already qualified native reader."""
import datetime
import json
from pathlib import Path
import shutil
import subprocess
import sys
import time

import check_native_villa_delivery as first
from assemble_native_villa_delivery import read, require, sha, BATCHES
from recover_native_scene_check_queue import wait_exact

ROOT = first.W / 'villa-remaining-canonical-admission-v1'
RECOVERY = first.W / 'native-scene-check-queue-recovery-v2'
EXECUTABLE = Path('F:/OpenRPG_Forge/relocated-work/Rust-target/debug/openrpg-native-source-scene-delivery-check.exe')


def save(name, data):
    path = ROOT / name
    temporary = path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(data, indent=2), 'utf-8')
    temporary.replace(path)


def prepare():
    require(not ROOT.exists(), 'Existing admission evidence preserved')
    job = read(RECOVERY / 'JOB.v1.json')
    ticks = subprocess.check_output([first.PS, '-NoProfile', '-Command',
        f'(Get-Process -Id {job["pid"]} -ErrorAction Stop).StartTime.ToUniversalTime().Ticks'], text=True).strip()
    require(ticks.isdigit(), 'Recovery controller must be currently live')
    scope = [row for row in read(BATCHES)['batches'] if row['id'] != 'L04.VILLA.01']
    require(sum(row['expected_scenes'] for row in scope) == 187, 'Exact remaining scene scope required')
    ROOT.mkdir()
    paths = [Path(__file__).resolve(), Path(__file__).with_name('recover_native_scene_check_queue.py'),
             Path(first.__file__), BATCHES, RECOVERY / 'PREPARED.v1.json']
    save('PREPARED.v1.json', {'wait_pid': job['pid'], 'wait_birth_ticks': int(ticks),
        'scope': scope, 'input_pins': {str(p): sha(p) for p in paths},
        'expected_remaining_scenes': 187, 'gpu_rendered': False, 'full_import': False})
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': child.pid, 'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({'pid': child.pid, 'root': str(ROOT), 'expected_remaining_scenes': 187,
                      'actual_canonical_scenes': 0}))


def run():
    state = {'status': 'waiting_for_first_qualified_native_reader', 'closed_batches': [],
             'canonical_scenes': 0, 'gpu_rendered': False, 'studio_verified': False, 'full_import': False}
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        wait_exact(prepared['wait_pid'], prepared['wait_birth_ticks'])
        recovery_path = RECOVERY / 'RUN-RESULT.v1.json'
        recovery = read(recovery_path)
        require(recovery['status'] == 'actual_module_tests_and_20_native_scene_admissions_verified'
                and recovery['canonical_scenes'] == 20 and recovery['negative_controls_passed'] == 2
                and recovery['all_input_pins_unchanged'], 'First actual native reader not qualified')
        pins = {**prepared['input_pins'], **recovery['input_pins'], str(recovery_path): sha(recovery_path)}
        def verify_pins():
            for path, expected in pins.items():
                require(sha(path) == expected, 'Qualified reader/source changed: ' + path)
        verify_pins()
        executable_sha = sha(EXECUTABLE)
        immutable_executable = ROOT / 'canonical-reader.immutable.exe'
        shutil.copyfile(EXECUTABLE, immutable_executable)
        require(sha(EXECUTABLE) == sha(immutable_executable) == executable_sha, 'Reader executable changed during copy')
        pins[str(immutable_executable)] = executable_sha
        for batch in prepared['scope']:
            identity = batch['id']
            frame_root = first.W / ('villa-native-source-frame-bound-' + identity.lower().replace('.', '-') + '-v1')
            frame_path = frame_root / 'RUN-RESULT.v1.json'
            state.update(status='waiting_for_closed_physical_batch', active_batch=identity)
            save('RUN-RESULT.v1.json', state)
            while True:
                if frame_path.exists():
                    receipt = read(frame_path)
                    if receipt['status'] == 'native_original_source_frames_bound_and_reopened':
                        break
                    require(receipt['status'] != 'refused_or_partial', 'Frame batch refused; inspect retained evidence')
                serial = read(first.W / 'villa-native-sequential-delivery-v2/RUN-RESULT.v1.json')
                require(serial['status'] != 'refused_or_partial', 'Physical delivery stopped before this batch')
                time.sleep(30)
            require(receipt['verified_scenes'] == batch['expected_scenes'], 'Closed physical count mismatch')
            inputs_path = frame_root / 'INPUTS.v1.json'
            rows = read(inputs_path)
            require(len(rows) == batch['expected_scenes']
                    and {r['asset_id'] for r in rows} == set(batch['asset_ids'])
                    and {r['asset_id'] for r in receipt['rows']} == set(batch['asset_ids']), 'Exact native IDs required')
            batch_pins = {str(frame_path): sha(frame_path), str(inputs_path): sha(inputs_path),
                          **{r['glb']: r['sha256'] for r in rows}}
            for path, expected in batch_pins.items():
                require(sha(path) == expected, 'Native batch changed before admission')
            verify_pins()
            state['status'] = 'admitting_native_scenes'
            save('RUN-RESULT.v1.json', state)
            label = identity.lower().replace('.', '-')
            stdout_path, stderr_path = ROOT / (label + '.stdout.log'), ROOT / (label + '.stderr.log')
            with stdout_path.open('xb') as out, stderr_path.open('xb') as err:
                child = subprocess.Popen([str(immutable_executable), str(inputs_path)], stdout=out, stderr=err,
                    cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW)
                save('READER-JOB.v1.json', {'pid': child.pid, 'batch_id': identity})
                require(child.wait() == 0, 'Actual canonical reader refused this batch')
            results = [json.loads(line) for line in stdout_path.read_text('utf-8').splitlines() if line.startswith('{')]
            summaries = [row for row in results if 'canonical_scenes' in row]
            require(len(summaries) == 1 and summaries[0]['canonical_scenes'] == batch['expected_scenes']
                    and summaries[0]['negative_controls_passed'] == 2, 'Actual admission/control count mismatch')
            require({row['asset_id'] for row in results if 'asset_id' in row} == set(batch['asset_ids']), 'Actual admitted IDs differ')
            for path, expected in batch_pins.items():
                require(sha(path) == expected, 'Native batch changed during admission')
            verify_pins()
            state['closed_batches'].append({'batch_id': identity, 'canonical_scenes': batch['expected_scenes'],
                'negative_controls_passed': 2, 'input_pins': batch_pins,
                'stdout': str(stdout_path), 'stdout_sha256': sha(stdout_path), 'summary': summaries[0],
                'gpu_rendered': False, 'studio_verified': False})
            state['canonical_scenes'] += batch['expected_scenes']
            save('RUN-RESULT.v1.json', state)
        require(state['canonical_scenes'] == 187, 'Full remaining canonical scope incomplete')
        state.update(status='187_remaining_native_scenes_canonically_admitted_runtime_pending', input_pins=pins)
    except Exception as error:
        state.update(status='refused_or_unverified', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)
    require(state['status'] != 'refused_or_unverified', state.get('error', 'Admission refused'))


if __name__ == '__main__':
    {'prepare': prepare, 'run': run}[sys.argv[1]]()
