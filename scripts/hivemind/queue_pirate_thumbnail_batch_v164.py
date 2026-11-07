"""Serialize the prepared Pirate.07 capture after the owned torch reopener."""
from pathlib import Path
from datetime import datetime, timezone
import ctypes
import hashlib
import json
import subprocess
import sys
import traceback
import queue_house_thumbnail_batch_v1 as capture

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'pirate-source-thumbnail-batch-0113-0120-v163'
PREDECESSOR = W / 'medieval-torch-native-materials-reopen-v161/RUN-RESULT.v1.json'
PS = capture.PS

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def write(state):
    state['updated_utc'] = datetime.now(timezone.utc).isoformat()
    tmp = ROOT / 'QUEUE-RESULT.tmp'
    tmp.write_text(json.dumps(state, indent=2), 'utf-8')
    tmp.replace(ROOT / 'QUEUE-RESULT.v1.json')

def run():
    state = dict(status='waiting_for_exact_torch_reopener', waiting_pid=5144, full_import=False)
    try:
        pins = capture.read(ROOT / 'QUEUE-JOB.v1.json')['input_pins']
        for path, digest in pins.items():
            assert sha(path) == digest, path
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.OpenProcess.argtypes = [ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
        kernel.OpenProcess.restype = ctypes.c_void_p
        kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
        kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        handle = kernel.OpenProcess(0x100000, False, 5144)
        if handle:
            try:
                command = subprocess.check_output([PS, '-NoProfile', '-Command', '(Get-CimInstance Win32_Process -Filter "ProcessId=5144").CommandLine'], text=True).strip()
                if command:
                    assert 'verify_torch_native_materials_v161.py' in command, command
                write(state)
                assert kernel.WaitForSingleObject(handle, 0xffffffff) == 0
            finally:
                kernel.CloseHandle(handle)
        previous = capture.read(PREDECESSOR)
        assert previous.get('finished_utc'), 'Predecessor has no terminal receipt'
        state.update(status='predecessor_terminal_capture_starting', predecessor_status=previous['status'], predecessor_receipt_sha256=sha(PREDECESSOR))
        write(state)
        for path, digest in pins.items():
            assert sha(path) == digest, path
        capture.ROOT = ROOT
        capture.PROJECT = W / 'unity-library-source-thumbnail-pirate-bench-v2/project'
        capture.run()
        receipt = capture.read(ROOT / 'RUN-RESULT.v1.json')
        state.update(status='capture_terminal_review_required', capture_status=receipt['status'], capture_receipt_sha256=sha(ROOT / 'RUN-RESULT.v1.json'))
    except Exception as exc:
        state.update(status='pirate_capture_queue_refusal_retained', error=repr(exc), traceback=traceback.format_exc())
    finally:
        state['finished_utc'] = datetime.now(timezone.utc).isoformat()
        write(state)

def launch():
    assert (ROOT / 'PREPARED.v1.json').is_file()
    assert not (ROOT / 'QUEUE-JOB.v1.json').exists()
    assert not (ROOT / 'RUN-RESULT.v1.json').exists()
    pins = {str(path): sha(path) for path in [Path(__file__).resolve(), Path(capture.__file__).resolve()]}
    capture.write(ROOT / 'QUEUE-JOB.v1.json', dict(input_pins=pins, created_utc=datetime.now(timezone.utc).isoformat()))
    with (ROOT / 'queue-controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
        capture.write(ROOT / 'QUEUE-JOB.v1.json', dict(controller_pid=child.pid, input_pins=pins, created_utc=datetime.now(timezone.utc).isoformat()))
    print(json.dumps(dict(controller_pid=child.pid, prepared_cards=8, images_expected=16, output=str(ROOT))))

if __name__ == '__main__':
    run() if sys.argv[-1] == 'run' else launch()
