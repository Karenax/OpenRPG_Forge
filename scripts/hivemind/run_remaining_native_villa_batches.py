"""Serial physical delivery only; GPU, library and runtime remain separate gates."""
import datetime
import json
import os
from pathlib import Path
import subprocess
import sys

import assemble_native_villa_delivery as assembly
from assemble_native_villa_delivery import read, require, sha

W = assembly.W
ROOT = W / 'villa-native-sequential-delivery-v2'
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
FRAME_HELPER = Path(__file__).with_name('bind_native_villa_source_frames.py')
PRIOR = W / 'villa-native-source-frame-bound-l04-villa-03-v1/RUN-RESULT.v1.json'

def save(name, value):
    p = ROOT / name
    t = p.with_suffix('.json.tmp')
    t.write_text(json.dumps(value, indent=2), 'utf-8')
    os.replace(t, p)

def prepare():
    require(not ROOT.exists(), 'Existing serial job preserved')
    previous = read(PRIOR)
    require(previous['status'] == 'native_original_source_frames_bound_and_reopened'
            and previous['verified_scenes'] == 20, 'Closed third-frame receipt required')
    for number in range(4, 12):
        suffix = f'l04-villa-{number:02d}-v2'
        require(not (W / ('villa-native-scene-delivery-' + suffix)).exists(),
                'Existing remaining batch must be reviewed, not repeated')
    ROOT.mkdir()
    helpers = [Path(__file__).resolve(), Path(assembly.__file__).resolve(), FRAME_HELPER, PRIOR]
    save('PREPARED.v1.json', {'prior_batch_closed': True,
        'first_batch': 4, 'last_batch': 11, 'helper_pins': {str(p): sha(p) for p in helpers},
        'scope': 'physical_source_storage_normals_and_object_frames_only', 'full_import': False})
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': child.pid, 'launch_utc': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({'pid': child.pid, 'root': str(ROOT), 'planned_batches': 8, 'new_verified_scenes': 0}))

def run():
    state = {'status': 'checking_closed_third_frame_batch', 'closed_batches': [],
        'new_verified_physical_scenes': 0, 'gpu_rendered': False, 'studio_verified': False, 'full_import': False}
    save('RUN-RESULT.v1.json', state)
    try:
        prep = read(ROOT / 'PREPARED.v1.json')
        previous = read(PRIOR)
        require(previous['status'] == 'native_original_source_frames_bound_and_reopened' and previous['verified_scenes'] == 20,
                'Third native frame batch incomplete/refused')
        for p, expected in prep['helper_pins'].items():
            require(sha(p) == expected, 'Prepared helper changed: ' + p)
        scope = read(assembly.BATCHES)
        for number in range(4, 12):
            batch_id = f'L04.VILLA.{number:02d}'
            batch = next(b for b in scope['batches'] if b['id'] == batch_id)
            folder = 'villa-native-scene-delivery-' + batch_id.lower().replace('.', '-') + '-v2'
            assembly.ROOT = W / folder
            require(not assembly.ROOT.exists(), 'Existing batch must be reviewed, not repeated: ' + folder)
            assembly.ROOT.mkdir()
            inputs = [assembly.BATCHES, assembly.TEXTURES, assembly.NORMALS, Path(assembly.__file__).resolve(),
                Path(assembly.__file__).with_name('apply_scene_normal_bindings.py'), assembly.PROBE / 'DECODER-PARITY.v1.json']
            assembly.save('PREPARED.v1.json', {'batch_id': batch_id, 'created_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
                'input_pins': {str(p): sha(p) for p in inputs}, 'serial_driver_receipt': str(ROOT / 'PREPARED.v1.json')})
            state.update(status='assembling_native_batch', active_batch=batch_id)
            save('RUN-RESULT.v1.json', state)
            assembly.run(batch_id)
            assembled_path = assembly.ROOT / 'RUN-RESULT.v1.json'
            assembled = read(assembled_path)
            require(assembled['verified_derivatives'] == batch['expected_scenes'], 'Incomplete physical batch')
            state['status'] = 'binding_native_source_frames'
            save('RUN-RESULT.v1.json', state)
            code = subprocess.run([sys.executable, '-B', str(FRAME_HELPER), batch_id], cwd=ROOT,
                creationflags=subprocess.CREATE_NO_WINDOW).returncode
            require(code == 0, 'Frame binding failed; retained batch proof')
            frame_path = W / ('villa-native-source-frame-bound-' + batch_id.lower().replace('.', '-') + '-v1/RUN-RESULT.v1.json')
            frame = read(frame_path)
            require(frame['status'] == 'native_original_source_frames_bound_and_reopened'
                    and frame['verified_scenes'] == batch['expected_scenes']
                    and {r['asset_id'] for r in frame['rows']} == set(batch['asset_ids']), 'Frame scope mismatch')
            for row in frame['rows']:
                require(sha(row['glb']) == row['sha256'], 'Closed frame derivative changed')
            for p, expected in prep['helper_pins'].items():
                require(sha(p) == expected, 'Serial helper changed during delivery')
            state['closed_batches'].append({'batch_id': batch_id, 'verified_scenes': frame['verified_scenes'],
                'assembly_receipt': str(assembled_path), 'assembly_receipt_sha256': sha(assembled_path),
                'frame_receipt': str(frame_path), 'frame_receipt_sha256': sha(frame_path),
                'canonical_reader_accepted': False, 'studio_verified': False, 'full_import': False})
            state['new_verified_physical_scenes'] += frame['verified_scenes']
            save('RUN-RESULT.v1.json', state)
        require(state['new_verified_physical_scenes'] == 147, 'Expected remaining eligible scope')
        state['status'] = 'remaining_147_native_physical_derivatives_reopened_runtime_unqualified'
    except (Exception, SystemExit) as error:
        state.update(status='refused_or_partial', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)
    if state['status'] == 'refused_or_partial':
        raise SystemExit(1)

if __name__ == '__main__':
    {'prepare': prepare, 'run': run}[sys.argv[1]]()
