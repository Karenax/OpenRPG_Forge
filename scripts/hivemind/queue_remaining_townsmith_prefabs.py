"""Assemble only the three remaining prefabs after the current geometry job closes."""
import datetime
import json
from pathlib import Path
import subprocess
import sys

from assemble_native_villa_delivery import read, require, sha
from recover_native_scene_check_queue import wait_exact

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK / 'townsmith-remaining-prefab-queue-v1'
SERIAL = WORK / 'townsmith-sequential-remaining-v2'
HELPER = Path(__file__).with_name('assemble_remaining_townsmith_prefabs.py')
PRIOR = WORK / 'townsmith-closed-prefab-bindings-v1/RESULT.v1.json'
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'


def save(name, value):
    target = ROOT / name
    temporary = target.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2), 'utf-8')
    temporary.replace(target)


def prepare():
    require(not ROOT.exists(), 'Existing queue evidence preserved')
    job = read(SERIAL / 'JOB.v1.json')
    birth = subprocess.check_output([PS, '-NoProfile', '-Command',
        f'(Get-Process -Id {job["pid"]} -ErrorAction Stop).StartTime.ToUniversalTime().Ticks'], text=True).strip()
    require(birth.isdigit(), 'Existing Townsmith controller must be live now')
    paths = [Path(__file__).resolve(), HELPER, PRIOR,
             Path(__file__).with_name('assemble_nested_prefab_bindings.py'),
             Path(__file__).with_name('recover_native_scene_check_queue.py')]
    ROOT.mkdir()
    save('PREPARED.v1.json', {'wait_pid': job['pid'], 'wait_birth_ticks': int(birth),
        'input_pins': {str(p): sha(p) for p in paths}, 'expected_new_prefabs': 3,
        'existing_prefabs_preserved': 5, 'studio_verified': False, 'full_import': False})
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdin=subprocess.DEVNULL, stdout=log, stderr=log,
            creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': child.pid, 'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({'pid': child.pid, 'root': str(ROOT), 'expected_new_prefabs': 3}))


def run():
    state = {'status': 'waiting_for_existing_geometry_controller', 'prefabs_bound': 0,
             'glb_created': False, 'material_render_qualified': False,
             'studio_verified': False, 'full_import': False}
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        wait_exact(prepared['wait_pid'], prepared['wait_birth_ticks'])
        serial_path = SERIAL / 'RUN-RESULT.v1.json'
        serial = read(serial_path)
        require(serial['status'] == 'remaining_intermediate_geometry_batches_closed_final_prefabs_runtime_pending'
                and serial['new_verified_intermediate_geometries'] == 773,
                'Existing geometry chain must actually close all remaining cases')
        pins = {**prepared['input_pins'], str(serial_path): sha(serial_path)}
        for path, expected in pins.items():
            require(sha(path) == expected, 'Queue input changed: ' + path)
        state['status'] = 'assembling_three_remaining_prefabs'
        save('RUN-RESULT.v1.json', state)
        with (ROOT / 'assembly.stdout.log').open('xb') as out, (ROOT / 'assembly.stderr.log').open('xb') as err:
            child = subprocess.Popen([sys.executable, '-B', str(HELPER)], cwd=ROOT,
                stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            save('ASSEMBLY-JOB.v1.json', {'pid': child.pid})
            require(child.wait() == 0, 'Remaining prefab assembly refused; inspect retained logs')
        receipt_path = WORK / 'townsmith-remaining-prefab-bindings-v1/RESULT.v1.json'
        receipt = read(receipt_path)
        require(receipt['prefabs_bound'] == 3 and receipt['previous_prefabs_preserved'] == 5
                and receipt['source_and_derivative_pins_unchanged'], 'New prefab closure not proven')
        for path, expected in pins.items():
            require(sha(path) == expected, 'Queue input changed during assembly: ' + path)
        state.update(status='three_remaining_prefab_bindings_reopened_render_pending',
            prefabs_bound=3, existing_prefabs_preserved=5,
            receipt=str(receipt_path), receipt_sha256=sha(receipt_path), input_pins=pins,
            finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    except Exception as error:
        state.update(status='refused_or_unverified', error=str(error))
    save('RUN-RESULT.v1.json', state)
    require(state['status'] != 'refused_or_unverified', state.get('error', 'Queue refused'))


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
