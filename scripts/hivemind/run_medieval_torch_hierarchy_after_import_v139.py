"""One-shot owned wait for v137, followed by the complete hierarchy verifier."""
from pathlib import Path
from datetime import datetime, timezone
import ctypes
import hashlib
import json
import subprocess
import sys
import traceback

W = Path('F:/OpenRPG_ImportWork')
O = W/'medieval-torch-full-hierarchy-contract-v139'
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
helper = Path(__file__).with_name('verify_medieval_torch_hierarchy_v139.py')


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(state):
    state.update(updated_utc=datetime.now(timezone.utc).isoformat(),
                 recurring_automation=False, full_import=False)
    temp = O/'QUEUE.tmp'
    temp.write_text(json.dumps(state, indent=2), 'utf-8')
    temp.replace(O/'QUEUE.v1.json')


state = dict(status='waiting_for_exact_prefab_import_controller', waiting_pid=39944)
try:
    assert not (O/'QUEUE.v1.json').exists(), 'Preserve previous controller receipt'
    prep = read(O/'PREPARED.v1.json')
    assert sha(helper) == prep['input_pins'][str(helper.resolve())]
    state['controller_sha256'] = sha(__file__)
    save(state)
    command = subprocess.check_output([
        PS, '-NoProfile', '-Command',
        '(Get-CimInstance Win32_Process -Filter "ProcessId=39944").CommandLine'],
        text=True).strip()
    if command:
        assert 'run_medieval_torch_prefab_import_v137.py' in command
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.OpenProcess.argtypes = [ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
        kernel.OpenProcess.restype = ctypes.c_void_p
        kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
        kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        handle = kernel.OpenProcess(0x100000, False, 39944)
        assert handle, 'Cannot acquire exact predecessor handle'
        try:
            assert kernel.WaitForSingleObject(handle, 0xffffffff) == 0
        finally:
            kernel.CloseHandle(handle)
    run = read(W/'medieval-torch-native-prefab-import-v137/RUN-RESULT.v1.json')
    assert run['status'] == 'complete_private_torch_prefab_native_import_and_hierarchy_inspected', run
    assert sha(helper) == prep['input_pins'][str(helper.resolve())]
    with (O/'verify.stdout.log').open('xb') as out, (O/'verify.stderr.log').open('xb') as err:
        child = subprocess.Popen([sys.executable, '-B', str(helper), 'verify'],
                                 cwd=O, stdin=subprocess.DEVNULL, stdout=out,
                                 stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
        state.update(status='comparing_actual_native_hierarchy', verifier_pid=child.pid)
        save(state)
        state['verifier_exit_code'] = child.wait()
    assert state['verifier_exit_code'] == 0, 'Native hierarchy verifier refused; see logs'
    result = read(O/'RESULT.v1.json')
    assert result['native_matrix_verified'] and len(result['checks']) == 8
    state.update(status='native_hierarchy_comparison_terminal_verified',
                 result_sha256=sha(O/'RESULT.v1.json'))
except Exception as error:
    state.update(status='native_hierarchy_comparison_refusal_retained',
                 error=repr(error), traceback=traceback.format_exc())
finally:
    save(state)
