"""One-shot sequential capture verification/conversion, with receipts per subset."""
import datetime
import json
from pathlib import Path
import subprocess
import sys
import time

from assemble_native_villa_delivery import read, require, sha
import verify_townsmith_closed_capture_batch as native
import convert_verified_townsmith_batch as original

ROOT = native.W / 'townsmith-sequential-remaining-v1'
HERE = Path(__file__).resolve().parent
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
PRIOR = native.W / 'townsmith-native-geometry-0060-0079-v1/RESULT.v1.json'


def save(name, data):
    p = ROOT / name
    temp = p.with_suffix('.json.tmp')
    temp.write_text(json.dumps(data, indent=2), 'utf-8')
    temp.replace(p)


def live_capture():
    query = "$p=Get-Process -Id 12644 -ErrorAction SilentlyContinue;if($p){$p.StartTime.ToUniversalTime().Ticks}"
    result = subprocess.run([PS, '-NoProfile', '-Command', query], capture_output=True, text=True,
                            creationflags=subprocess.CREATE_NO_WINDOW)
    require(result.returncode == 0, 'Capture process observation failed; do not infer termination')
    return result.stdout.strip() == '639269259859704516'


def prepare():
    require(not ROOT.exists(), 'Existing sequential evidence preserved')
    prior = read(PRIOR)
    require(prior['verified_meshes'] == 20 and prior['status'] == '20_verified_townsmith_geometry_derivatives_reopened',
            'Fourth subset must be closed before continuing')
    for start in range(80, 893, 20):
        count = min(20, 893 - start)
        suffix = f'{start:04d}-{start+count-1:04d}-v1'
        for prefix in ['townsmith-native-partial-reopen-', 'townsmith-native-geometry-']:
            require(not (native.W / (prefix + suffix)).exists(), 'Existing remaining subset must be reviewed')
    require(live_capture(), 'Expected native capture process required at launch')
    ROOT.mkdir()
    helpers = [Path(__file__).resolve(), HERE/'verify_townsmith_closed_capture_batch.py',
               HERE/'convert_verified_townsmith_subset.py', HERE/'convert_closed_nested_geometry.py',
               Path(original.__file__), original.CONVERTER, native.VERIFIER, PRIOR]
    save('PREPARED.v1.json', {'first_ordinal': 80, 'expected_remaining_cases': 813,
         'batch_sizes': [min(20, 893-start) for start in range(80, 893, 20)],
         'helper_pins': {str(p): sha(p) for p in helpers}, 'full_import': False})
    with (ROOT/'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': child.pid, 'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({'pid': child.pid, 'root': str(ROOT), 'remaining_cases': 813, 'planned_batches': 41}))


def run():
    state = {'status': 'checking_inputs', 'closed_batches': [], 'new_verified_intermediate_geometries': 0,
             'whole_native_capture_qualified': False, 'studio_verified': False, 'full_import': False}
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT/'PREPARED.v1.json')
        for start in range(80, 893, 20):
            count = min(20, 893-start)
            for p, expected in prepared['helper_pins'].items():
                require(sha(p) == expected, 'Prepared input changed: '+p)
            state.update(status='waiting_for_native_cases', start_ordinal=start, expected_cases=count)
            save('RUN-RESULT.v1.json', state)
            while True:
                _, progress = native.progress_snapshot()
                if len(progress['cases']) >= start+count:
                    break
                require(live_capture(), 'Native capture ended before requested subset was written; inspect retained evidence')
                time.sleep(30)
            for script, arguments, stage in [
                ('verify_townsmith_closed_capture_batch.py', [start, count], 'verifying_native_subset'),
                ('convert_verified_townsmith_subset.py', [start, count], 'converting_verified_subset')]:
                state['status'] = stage
                save('RUN-RESULT.v1.json', state)
                subprocess.run([sys.executable, '-B', str(HERE/script), *map(str, arguments)],
                    check=True, creationflags=subprocess.CREATE_NO_WINDOW)
            suffix = f'{start:04d}-{start+count-1:04d}-v1'
            capture_path = native.W / ('townsmith-native-partial-reopen-'+suffix) / 'RESULT.v1.json'
            geometry_path = native.W / ('townsmith-native-geometry-'+suffix) / 'RESULT.v1.json'
            captured, geometry = read(capture_path), read(geometry_path)
            require(captured['verified_native_mesh_cases'] == geometry['verified_meshes'] == count,
                    'Subset count mismatch')
            require(geometry['status'] == 'verified_townsmith_geometry_derivatives_reopened', 'Geometry not closed')
            require([r['capture_ordinal'] for r in geometry['rows']] == list(range(start, start+count)),
                    'Derived subset scope mismatch')
            for row in geometry['rows']:
                target = Path(row['derivative'])
                require(sha(target/'GEOMETRY.v1.json') == row['geometry_receipt_sha256'], 'Geometry receipt changed')
                for name, details in read(target/'GEOMETRY.v1.json')['derived_streams'].items():
                    require(sha(target/name) == details['sha256'], 'Closed derived stream changed')
            state['closed_batches'].append({'start_ordinal': start, 'count': count,
                 'capture_receipt': str(capture_path), 'capture_receipt_sha256': sha(capture_path),
                 'geometry_receipt': str(geometry_path), 'geometry_receipt_sha256': sha(geometry_path),
                 'streams_reopened': geometry['derived_streams_reopened'], 'studio_verified': False})
            state['new_verified_intermediate_geometries'] += count
            save('RUN-RESULT.v1.json', state)
        require(state['new_verified_intermediate_geometries'] == 813, 'Remaining scope incomplete')
        state['status'] = 'remaining_intermediate_geometry_batches_closed_final_prefabs_runtime_pending'
    except Exception as error:
        state.update(status='refused_or_partial', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)
    require(state['status'] != 'refused_or_partial', state.get('error', 'Sequential job refused'))


if __name__ == '__main__':
    {'prepare': prepare, 'run': run}[sys.argv[1]]()
