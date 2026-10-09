"""Independently reopen six URP captures and ten default observations."""
import datetime
import json
import os
from pathlib import Path
import subprocess
import sys

from recover_native_scene_check_queue import wait_exact
from verify_native_urp_material_snapshot import controls, urp_controls, reconcile_urp, sha

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'villa-urp-six-material-independent-reopen-v1'
CAPTURE = W / 'unity-villa-six-original-urp-materials-v1'
PLAN = W / 'villa-urp-native-capture-inputs-v1/RESULT.v1.json'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def save(name, value):
    target = ROOT / name
    temporary = target.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2, allow_nan=False), 'utf-8')
    temporary.replace(target)


def prepare():
    assert not ROOT.exists(), 'Existing reopening preserved'
    fixture = dict(shared_controls=controls(), urp_controls=urp_controls(), native_materials_verified=0)
    plan = read(PLAN)
    pins = dict(plan['input_pins'])
    for path in [PLAN, CAPTURE / 'PREPARED.v1.json', CAPTURE / 'manifest.v1.json',
                 Path(__file__).resolve(), Path(__file__).with_name('verify_native_urp_material_snapshot.py'),
                 Path(__file__).with_name('recover_native_scene_check_queue.py')]:
        pins[str(path)] = sha(path)
    capture = read(CAPTURE / 'PREPARED.v1.json')
    for path, expected in capture['input_pins'].items():
        if path.startswith(str(CAPTURE / 'project/Assets/Editor')) and not path.endswith('.meta'):
            pins[path] = expected
    assert all(sha(p) == h for p, h in pins.items())
    ROOT.mkdir()
    save('CONTROLS.v1.json', fixture)
    save('PREPARED.v1.json', dict(input_pins=pins, expected_materials=6, expected_defaults=10,
                                  capture_pid=40648, capture_birth=639269781244301185))
    with (ROOT / 'controller.log').open('xb') as output:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
                                  stdout=output, stderr=output, stdin=subprocess.DEVNULL,
                                  creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(pid=child.pid, started_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(pid=child.pid, materials=6, defaults=10, native_materials_verified=0)))


def run():
    state = dict(status='waiting_for_exact_villa_urp_capture_controller', native_materials_verified=0,
                 native_defaults_verified=0, gpu_verified=False, studio_verified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        prep = read(ROOT / 'PREPARED.v1.json')
        wait_exact(prep['capture_pid'], prep['capture_birth'])
        assert all(sha(p) == h for p, h in prep['input_pins'].items()), 'Pinned source changed'
        capture = read(CAPTURE / 'RUN-RESULT.v1.json')
        assert capture['status'] == 'six_original_urp_material_passes_and_defaults_captured', capture
        native_path = CAPTURE / 'NATIVE-RESULT.v1.json'
        expected_native = sha(native_path)
        assert expected_native == capture['native_result_sha256']
        result = reconcile_urp(read(PLAN), read(native_path))
        assert all(sha(p) == h for p, h in prep['input_pins'].items()) and sha(native_path) == expected_native
        state.update(status='six_urp_materials_and_ten_defaults_independently_reopened',
                     native_materials_verified=len(result['materials']), native_defaults_verified=len(result['defaults']),
                     result=result, native_result_sha256=expected_native, source_pins_reopened=True,
                     input_pins=prep['input_pins'], original_authoring_version_qualified=False)
    except Exception as error:
        state.update(status='refusal_preserved', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
