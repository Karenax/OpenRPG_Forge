"""Independently reopen the three remaining Townsmith poses after assembly."""
import datetime
import json
from pathlib import Path
import subprocess
import sys
import time

from recover_native_scene_check_queue import wait_exact
from verify_townsmith_prefab_pose import sha, read, require

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK / 'townsmith-remaining-prefab-pose-queue-v1'
ASSEMBLY = WORK / 'townsmith-remaining-prefab-queue-v1'
RECEIPT = WORK / 'townsmith-remaining-prefab-bindings-v1/RESULT.v1.json'
FIRST = WORK / 'townsmith-closed-prefab-bindings-v1/RESULT.v1.json'
OUTPUT = WORK / 'townsmith-remaining-three-prefab-pose-reopen-v1'
HELPER = Path(__file__).with_name('verify_townsmith_prefab_pose.py')
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'


def save(name, value):
    target = ROOT / name
    temporary = target.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2) + '\n', 'utf-8')
    for attempt in range(40):
        try:
            temporary.replace(target)
            return
        except PermissionError:
            if attempt == 39:
                raise
            time.sleep(.25)


def prepare():
    require(not ROOT.exists() and not OUTPUT.exists(), 'Existing review evidence preserved')
    job = read(ASSEMBLY / 'JOB.v1.json')
    birth = subprocess.check_output([PS, '-NoProfile', '-Command',
        f'(Get-Process -Id {job["pid"]} -ErrorAction Stop).StartTime.ToUniversalTime().Ticks'],
        text=True).strip()
    require(birth.isdigit(), 'Exact assembly controller must be live now')
    compile(HELPER.read_text('utf-8-sig'), str(HELPER), 'exec')
    paths = [Path(__file__).resolve(), HELPER, FIRST, ASSEMBLY / 'PREPARED.v1.json',
             Path(__file__).with_name('recover_native_scene_check_queue.py')]
    ROOT.mkdir()
    save('PREPARED.v1.json', {'wait_pid': job['pid'], 'wait_birth_ticks': int(birth),
        'input_pins': {str(path): sha(path) for path in paths}, 'expected_prefabs': 3,
        'prior_five_prefabs_not_repeated': True})
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdin=subprocess.DEVNULL, stdout=log, stderr=log,
            creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': child.pid,
         'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({'pid': child.pid, 'expected_prefabs': 3, 'root': str(ROOT)}))


def run():
    state = {'status': 'waiting_for_exact_remaining_prefab_assembly', 'prefabs_verified': 0,
             'gpu_rendered': False, 'studio_verified': False, 'full_import': False}
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        wait_exact(prepared['wait_pid'], prepared['wait_birth_ticks'])
        assembly_path = ASSEMBLY / 'RUN-RESULT.v1.json'
        assembly = read(assembly_path)
        require(assembly['status'] == 'three_remaining_prefab_bindings_reopened_render_pending'
                and assembly['prefabs_bound'] == 3, 'Three actual prefab assemblies required')
        require(sha(RECEIPT) == assembly['receipt_sha256'], 'Assembly receipt changed')
        prior_ids = {row['asset_id'] for row in read(FIRST)['rows']}
        new_ids = {row['asset_id'] for row in read(RECEIPT)['rows']}
        require(len(prior_ids) == 5 and len(new_ids) == 3 and prior_ids.isdisjoint(new_ids),
                'Remaining pose review repeats or omits a prefab')
        pins = {**prepared['input_pins'], str(RECEIPT): sha(RECEIPT),
                str(assembly_path): sha(assembly_path)}
        for path, expected in pins.items():
            require(sha(path) == expected, 'Prepared review input changed')
        state['status'] = 'independently_reopening_three_native_prefab_poses'
        save('RUN-RESULT.v1.json', state)
        with (ROOT / 'review.stdout.log').open('xb') as out, (ROOT / 'review.stderr.log').open('xb') as err:
            child = subprocess.Popen([sys.executable, '-B', str(HELPER), '--receipt', str(RECEIPT),
                '--output', str(OUTPUT), '--expected', '3'], cwd=ROOT, stdout=out, stderr=err,
                creationflags=subprocess.CREATE_NO_WINDOW)
            save('REVIEW-JOB.v1.json', {'pid': child.pid})
            require(child.wait() == 0, 'Independent pose review refused; logs preserved')
        result_path = OUTPUT / 'RESULT.v1.json'
        result = read(result_path)
        require(result['status'] == 'native_prefab_pose_and_renderer_identities_reopened'
                and result['prefabs_verified'] == 3
                and {row['asset_id'] for row in result['rows']} == new_ids,
                'Complete independent pose review required')
        for path, expected in pins.items():
            require(sha(path) == expected, 'Review input changed during reopen')
        state.update(status='three_remaining_native_prefab_poses_reopened_render_pending',
            prefabs_verified=3, receipt=str(result_path), receipt_sha256=sha(result_path),
            input_pins=pins)
    except Exception as error:
        state.update(status='refused_or_unverified', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)
    require(state['status'] != 'refused_or_unverified', state.get('error', 'Review refused'))


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
