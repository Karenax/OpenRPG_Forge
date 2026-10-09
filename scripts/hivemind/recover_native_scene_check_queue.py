"""Preserve the original queue; recover only after its exact processes have ended."""
import ctypes
from ctypes import wintypes
import datetime
import json
import os
from pathlib import Path
import re
import subprocess
import sys

from assemble_native_villa_delivery import read, require, sha
import check_native_villa_delivery as delivery

ROOT = delivery.W / 'native-scene-check-queue-recovery-v2'
PROJECTS = [delivery.W/'studio-source-scene-attachment-native-v1',
            delivery.W/'studio-source-scene-resources-native-v1', delivery.ROOT]
KERNEL = ctypes.WinDLL('kernel32', use_last_error=True)
KERNEL.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
KERNEL.OpenProcess.restype = wintypes.HANDLE
KERNEL.GetProcessTimes.argtypes = [wintypes.HANDLE, *([ctypes.POINTER(wintypes.FILETIME)] * 4)]
KERNEL.GetProcessTimes.restype = wintypes.BOOL
KERNEL.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
KERNEL.WaitForSingleObject.restype = wintypes.DWORD
KERNEL.CloseHandle.argtypes = [wintypes.HANDLE]


def wait_exact(pid, birth):
    handle = KERNEL.OpenProcess(0x00100000 | 0x1000, False, pid)
    if not handle:
        require(ctypes.get_last_error() == 87, 'Process observation failed; do not infer termination')
        return
    try:
        times = [wintypes.FILETIME() for _ in range(4)]
        require(KERNEL.GetProcessTimes(handle, *(ctypes.byref(t) for t in times)), 'Cannot verify process birth')
        # FILETIME starts in 1601; .NET DateTime.UtcTicks starts in year 1.
        actual = ((times[0].dwHighDateTime << 32) | times[0].dwLowDateTime) + 504911232000000000
        if actual != birth:
            return
        while True:
            result = KERNEL.WaitForSingleObject(handle, 45000)
            require(result in (0, 258), 'Process wait failed; do not restart')
            if result == 0:
                return
    finally:
        KERNEL.CloseHandle(handle)


def save(name, value):
    target = ROOT/name
    temporary = target.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2), 'utf-8')
    temporary.replace(target)


def prepare():
    require(not ROOT.exists(), 'Existing recovery retained')
    # Pin the exact birth from the original job, rather than trusting a stale PID.
    geometry_job = read(delivery.W/'canonical-source-scene-geometry-integration-v1/CARGO-JOB.v1.json')
    require(geometry_job['pid'] == 45220, 'Canonical build identity changed; review before preparing recovery')
    paths = [Path(__file__).resolve(), Path(delivery.__file__).resolve()]
    for project in PROJECTS:
        paths.append(project/'PREPARED.v1.json')
        paths.extend(Path(p) for p in read(project/'PREPARED.v1.json')['input_pins'])
    jobs = [(read(p/'PREPARED.v1.json')['wait_pid'], int(read(p/'PREPARED.v1.json')['wait_birth_ticks'])) for p in PROJECTS]
    require([pid for pid, _ in jobs] == [46292, 46380, 1224], 'Original queue identities differ')
    jobs.append((46704, 639269361611402122))
    # Compare the native FILETIME conversion against .NET on our live process.
    handle = KERNEL.OpenProcess(0x00100000 | 0x1000, False, os.getpid())
    require(bool(handle), 'Self process observation unavailable')
    try:
        times = [wintypes.FILETIME() for _ in range(4)]
        require(KERNEL.GetProcessTimes(handle, *(ctypes.byref(t) for t in times)), 'Self birth unavailable')
        birth = ((times[0].dwHighDateTime << 32) | times[0].dwLowDateTime) + 504911232000000000
        dotnet = subprocess.check_output([delivery.PS, '-NoProfile', '-Command',
            f'(Get-Process -Id {os.getpid()} -ErrorAction Stop).StartTime.ToUniversalTime().Ticks'], text=True).strip()
        require(birth == int(dotnet), 'Native process birth differs from .NET ticks')
        require(KERNEL.WaitForSingleObject(handle, 0) == 258, 'Live process wait control failed')
    finally:
        KERNEL.CloseHandle(handle)
    # A reused PID and an absent PID must return safely.
    wait_exact(os.getpid(), 1)
    wait_exact(0, 1)
    ROOT.mkdir()
    save('PREPARED.v1.json', {'jobs': jobs, 'input_pins': {str(p): sha(p) for p in paths},
                            'native_wait_controls': {'live_process_birth_matches_dotnet': True,
                                                     'live_wait_times_out': True,
                                                     'mismatched_and_absent_processes_return': True},
                            'whole_studio_application_checked': False, 'gpu_rendered': False})
    with (ROOT/'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, cwd=ROOT,
            creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': child.pid, 'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({'pid': child.pid, 'root': str(ROOT), 'tests_passed': 0}))


def run():
    state = {'status': 'waiting_for_original_queue_to_finish', 'tests_passed': 0,
             'canonical_scenes': 0, 'gpu_rendered': False, 'studio_verified': False, 'full_import': False}
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT/'PREPARED.v1.json')
        for pid, birth in prepared['jobs']:
            wait_exact(pid, birth)
        prior = read(delivery.PRIOR)
        require(prior['status'] == 'canonical_scene_geometry_native_verified'
                and prior['actual_tests'] == 6 and prior['all_input_pins_unchanged'],
                'Actual canonical API tests must pass before recovery')
        pins = {**prepared['input_pins'], **prior['source_pins'], str(delivery.PRIOR): sha(delivery.PRIOR)}
        frame_path = delivery.FRAME/'RUN-RESULT.v1.json'
        frame = read(frame_path)
        require(frame['status'] == 'native_original_source_frames_bound_and_reopened'
                and frame['verified_scenes'] == 20, 'First native frame batch must be closed')
        inputs = delivery.FRAME/'INPUTS.v1.json'
        rows = read(inputs)
        require(len(rows) == 20 and {r['asset_id'] for r in rows} == {r['asset_id'] for r in frame['rows']}, 'Native input scope mismatch')
        pins.update({str(frame_path): sha(frame_path), str(inputs): sha(inputs)})
        pins.update({r['glb']: r['sha256'] for r in rows})
        def verify_pins():
            for path, expected in pins.items():
                require(sha(path) == expected, 'Input changed: '+path)
        verify_pins()
        env = os.environ.copy()
        env.update(CARGO_TARGET_DIR='F:/OpenRPG_Forge/relocated-work/Rust-target', CARGO_INCREMENTAL='0',
                   CARGO_BUILD_JOBS='1', RUSTC=str(delivery.BIN/'rustc.exe'),
                   RUSTDOC=str(delivery.BIN/'rustdoc.exe'), RUSTUP_TOOLCHAIN='1.98.0-x86_64-pc-windows-msvc',
                   TEMP=str(ROOT), TMP=str(ROOT))
        env['PATH'] = str(delivery.BIN)+os.pathsep+env.get('PATH','')
        for index, project in enumerate(PROJECTS):
            label = ['attachment', 'resources', 'native_scenes'][index]
            state.update(status='checking_'+label)
            save('RUN-RESULT.v1.json', state)
            old = read(project/'RUN-RESULT.v1.json')
            expected_statuses = ['studio_source_attachment_structural_native_tests_verified',
                                 'actual_studio_source_resource_and_attachment_modules_native_checked']
            if index < 2 and old.get('status') == expected_statuses[index] and old.get('actual_tests_passed') == [6, 9][index] and old.get('all_input_pins_unchanged'):
                state[label+'_tests_passed'] = old['actual_tests_passed']
                continue
            if index == 2 and old.get('status') == '20_native_source_scenes_canonical_integrity_and_geometry_admitted' and old.get('canonical_scenes') == 20 and old.get('all_input_pins_unchanged'):
                require(old['negative_controls_passed'] == 2, 'Native controls absent')
                state.update(canonical_scenes=20, negative_controls_passed=2)
                continue
            commands = [('lock', ['generate-lockfile', '--offline'])]
            arguments = (['test', '--locked', '--offline', '-j1', 'source_scene_host::tests::', '--', '--nocapture']
                         if index == 0 else ['test', '--locked', '--offline', '-j1', '--', '--nocapture']
                         if index == 1 else ['run', '--locked', '--offline', '-j1', '--', str(inputs)])
            commands.append(('native', arguments))
            for stage, args in commands:
                with (ROOT/f'{label}.{stage}.stdout.log').open('xb') as out, (ROOT/f'{label}.{stage}.stderr.log').open('xb') as err:
                    child = subprocess.Popen([str(delivery.BIN/'cargo.exe'), *args], cwd=project, env=env,
                        stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
                    save('CARGO-JOB.v1.json', {'pid': child.pid, 'project': label, 'stage': stage})
                    require(child.wait() == 0, 'Actual build/test failed: '+label+'/'+stage)
            output = (ROOT/f'{label}.native.stdout.log').read_text('utf-8')
            if index < 2:
                matches = re.findall(r'test result: ok\. (\d+) passed; 0 failed', output)
                require(matches and int(matches[0]) == [6,9][index], 'Actual test count mismatch')
                state[label+'_tests_passed'] = [6,9][index]
            else:
                results = [json.loads(line) for line in output.splitlines() if line.startswith('{')]
                summaries = [r for r in results if 'canonical_scenes' in r]
                require(len(summaries) == 1 and summaries[0]['canonical_scenes'] == 20
                        and summaries[0]['negative_controls_passed'] == 2, 'Native admission/control count mismatch')
                require({r['asset_id'] for r in results if 'asset_id' in r} == {r['asset_id'] for r in rows}, 'Actual admitted IDs differ')
                state.update(summaries[0])
            verify_pins()
            save('RUN-RESULT.v1.json', state)
        verify_pins()
        state.update(status='actual_module_tests_and_20_native_scene_admissions_verified', input_pins=pins,
                     all_input_pins_unchanged=True, tests_passed=state['attachment_tests_passed']+state['resources_tests_passed'])
    except Exception as error:
        state.update(status='refused_or_unverified', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)
    require(state['status'] != 'refused_or_unverified', state.get('error','Recovery refused'))


if __name__ == '__main__':
    {'prepare': prepare, 'run': run}[sys.argv[1]]()
