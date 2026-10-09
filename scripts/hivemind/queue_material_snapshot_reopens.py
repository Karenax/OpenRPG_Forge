"""Reopen all 58 queued material captures after their exact controllers exit."""
import datetime
import json
from pathlib import Path
import subprocess
import sys
import time

from recover_native_scene_check_queue import wait_exact
from verify_native_material_snapshot import reconcile, sha

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'queued-native-material-independent-reopens-v1'
JOBS = [
    ('hdrp42', 3384, 639269632496501426, W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4'),
    ('medieval3', 41288, 639269641116536114, W / 'unity-medieval-cloth-material-source-v1'),
    ('delta13', 36720, 639269657574011425, W / 'unity-delta-null-material-defaults-v2'),
]


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def save(name, value):
    path = ROOT / name
    temporary = path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2, allow_nan=False), 'utf-8')
    for attempt in range(40):
        try:
            temporary.replace(path)
            return
        except PermissionError:
            if attempt == 39:
                raise
            time.sleep(.25)


def cases(kind, folder):
    if kind == 'hdrp42':
        prepared_path = folder / 'PREPARED.v4.json'
        prep = read(prepared_path)
        stable = {path: expected for path, expected in prep['input_pins'].items()
                  if not path.startswith(str(folder / 'project'))
                  or (path.startswith(str(folder / 'project/Assets')) and not path.endswith('.meta'))}
        return [dict(id=kind, manifest=str(folder / 'manifest.v4.json'),
                     native=str(folder / 'NATIVE-RESULT.v4.json'), expected=42,
                     prepared=str(prepared_path), pins=stable)]
    prepared_path = folder / 'PREPARED.v1.json'
    prep = read(prepared_path)
    if kind == 'medieval3':
        stable = {path: expected for path, expected in prep['input_pins'].items()
                  if not path.startswith(str(folder / 'project'))
                  or (path.startswith(str(folder / 'project/Assets')) and not path.endswith('.meta'))}
        return [dict(id=kind, manifest=str(folder / 'manifest.v1.json'),
                     native=str(folder / 'NATIVE-RESULT.v1.json'), expected=3,
                     prepared=str(prepared_path), pins=stable)]
    return [dict(id=project['pack_id'], manifest=project['manifest'], native=project['native_result'],
                 expected=project['expected'], prepared=str(prepared_path), pins=prep['source_pins'])
            for project in prep['projects']]


def prepare():
    assert not ROOT.exists(), 'Existing reopening queue preserved'
    tasks = []
    pins = {str(path): sha(path) for path in [Path(__file__).resolve(),
        Path(__file__).with_name('verify_native_material_snapshot.py'),
        Path(__file__).with_name('recover_native_scene_check_queue.py')]}
    for kind, pid, birth, folder in JOBS:
        items = cases(kind, folder)
        for item in items:
            pins[item['manifest']] = sha(item['manifest'])
            pins[item['prepared']] = sha(item['prepared'])
        tasks.append(dict(kind=kind, pid=pid, birth=birth, cases=items))
    assert sum(item['expected'] for task in tasks for item in task['cases']) == 58
    ROOT.mkdir()
    save('PREPARED.v1.json', dict(tasks=tasks, input_pins=pins, expected_materials=58))
    with (ROOT / 'controller.log').open('xb') as output:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdin=subprocess.DEVNULL, stdout=output, stderr=output,
            creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(pid=child.pid, started_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(pid=child.pid, expected_materials=58, native_materials_verified=0)))


def run():
    prep = read(ROOT / 'PREPARED.v1.json')
    state = dict(status='waiting_for_exact_native_capture_controllers', expected_materials=58,
                 native_materials_verified=0, cases=[], gpu_rendered=False, studio_verified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    for task in prep['tasks']:
        state.update(current_controller=task['pid'], current_group=task['kind'])
        save('RUN-RESULT.v1.json', state)
        wait_exact(task['pid'], task['birth'])
        for item in task['cases']:
            result = dict(id=item['id'], expected=item['expected'], verified=0)
            try:
                pins = {**prep['input_pins'], **item['pins']}
                assert all(sha(path) == expected for path, expected in pins.items()), 'Pinned source/input changed'
                manifest, native = read(item['manifest']), read(item['native'])
                native_sha = sha(item['native'])
                rows = reconcile(manifest, native)
                assert len(rows) == item['expected']
                assert sha(item['native']) == native_sha
                assert all(sha(path) == expected for path, expected in pins.items()), 'Input changed during reopening'
                result.update(status='native_materials_and_original_source_pins_independently_reopened',
                              verified=len(rows), rows=rows, source_pins_reopened=True,
                              native_sha256=native_sha, native=item['native'], input_pins=pins)
                state['native_materials_verified'] += len(rows)
            except Exception as error:
                result.update(status='refusal_preserved', error=str(error))
            state['cases'].append(result)
            save('RUN-RESULT.v1.json', state)
    state.update(status=('58_native_material_snapshots_independently_reopened'
                         if state['native_materials_verified'] == 58 else 'partial_or_refused_native_material_reopens'),
                 finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
