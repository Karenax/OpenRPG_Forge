"""One-shot continuation of the owned native job; no scheduled automation."""
from pathlib import Path
import ctypes
from ctypes import wintypes
import datetime
import hashlib
import json
import subprocess
import sys
import time

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'detail-mask-surface-finish-v1'
NATIVE = W / 'unity-source-null-mask-surface-v8'
HERE = Path(__file__).resolve().parent

def read(path):
    return json.loads(path.read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def save(data):
    temp = ROOT / 'RUN-RESULT.v1.json.tmp'
    temp.write_text(json.dumps(data, indent=2), 'utf-8')
    temp.replace(ROOT / 'RUN-RESULT.v1.json')

def run():
    state = dict(status='waiting_for_owned_native_controller', full_import=False)
    save(state)
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = None
    try:
        pins = read(ROOT / 'PREPARED.v1.json')
        for path, digest in pins['input_pins'].items():
            assert sha(Path(path)) == digest
        handle = kernel.OpenProcess(0x100000, False, pins['native_controller_pid'])
        assert handle, 'Owned native controller missing'
        while True:
            native = read(NATIVE / 'RUN-RESULT.v1.json')
            if native.get('finished_utc'):
                break
            assert kernel.WaitForSingleObject(handle, 0) == 258, 'Native controller exited without terminal receipt'
            time.sleep(3)
        state['native_status'] = native['status']
        if native.get('unity_exit_code') == 0:
            for path, digest in pins['input_pins'].items():
                assert sha(Path(path)) == digest
            with (ROOT / 'reopen.stdout.log').open('xb') as out, (ROOT / 'reopen.stderr.log').open('xb') as err:
                code = subprocess.run([sys.executable, '-B', str(HERE / 'reopen_null_mask_surface_v8.py')],
                    cwd=ROOT, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW).returncode
            state['independent_reopen_exit_code'] = code
            assert code == 0
            independent = read(W / 'detail-mask-surface-independent-reopen-v1/RESULT.v1.json')
            state['independent_status'] = independent['status']
            state['native_surface_default_qualified'] = independent['native_surface_default_qualified']
        else:
            state['native_surface_default_qualified'] = False
        with (ROOT / 'tracker.stdout.log').open('xb') as out, (ROOT / 'tracker.stderr.log').open('xb') as err:
            code = subprocess.run([sys.executable, '-B', str(HERE / 'record_active_batch_focus.py')],
                cwd=ROOT, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW).returncode
        assert code == 0
        state['status'] = 'native_job_reopened_and_tracker_updated'
    except Exception as error:
        state.update(status='refused_or_unverified', error=repr(error))
    finally:
        if handle:
            kernel.CloseHandle(handle)
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save(state)

def prepare():
    assert not ROOT.exists()
    native = read(NATIVE / 'RUN-RESULT.v1.json')
    assert not native.get('finished_utc'), 'Native job already terminal: reopen directly'
    pid = read(NATIVE / 'JOB.v1.json')['controller_pid']
    ROOT.mkdir()
    paths = [Path(__file__).resolve(), HERE/'reopen_null_mask_surface_v8.py',
        HERE/'record_active_batch_focus.py', NATIVE/'JOB.v1.json', NATIVE/'PREPARED.v1.json']
    (ROOT / 'PREPARED.v1.json').write_text(json.dumps(dict(native_controller_pid=pid,
        input_pins={str(path):sha(path) for path in paths}, one_shot=True, full_import=False), indent=2), 'utf-8')
    with (ROOT / 'controller.log').open('xb') as out:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdout=out, stderr=out, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT / 'JOB.v1.json').write_text(json.dumps(dict(controller_pid=child.pid)), 'utf-8')
    print(json.dumps(dict(controller_pid=child.pid, one_shot=True)))

if __name__ == '__main__':
    run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
