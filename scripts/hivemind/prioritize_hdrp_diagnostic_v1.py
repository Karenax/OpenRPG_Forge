"""Defer waiting captures, retain their inputs, diagnose after the active Villa job."""
import datetime
import json
from pathlib import Path
import subprocess
import sys

from recover_native_scene_check_queue import wait_exact
from verify_native_material_snapshot import sha

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'hdrp-diagnostic-queue-priority-v1'
PS = Path('C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe')
SCRIPTS = Path(__file__).resolve().parent
TASKS = [
    (43036, 639269812114287967, 'hdrp-native-context-diagnostic-v1', 'queue_hdrp_context_diagnostic.py', 'waiting_for_exact_existing_unity_and_reopen_controllers'),
    (9548, 639269784473133653, 'villa-urp-six-material-independent-reopen-v1', 'queue_villa_urp_material_reopen.py', 'waiting_for_exact_villa_urp_capture_controller'),
    (40648, 639269781244301185, 'unity-villa-six-original-urp-materials-v1', 'queue_villa_urp_material_capture.py', 'waiting_for_exact_delta_controller'),
    (34996, 639269677671765927, 'queued-native-material-independent-reopens-v1', 'queue_material_snapshot_reopens.py', 'waiting_for_exact_native_capture_controllers'),
    (36720, 639269657574011425, 'unity-delta-null-material-defaults-v2', 'recover_delta_material_capture_v2.py', 'waiting_for_exact_villa_api_controller'),
]


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def load(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def save(path, value):
    path = Path(path)
    temp = path.with_suffix('.tmp')
    temp.write_text(json.dumps(value, ensure_ascii=False, indent=2), 'utf-8')
    temp.replace(path)


def prepare():
    assert not ROOT.exists(), 'Existing priority operation preserved'
    # Inspect all tasks before touching any process. Active Villa keeps Delta gated.
    script = ["$ErrorActionPreference='Stop'; $api=Get-Process -Id 2432; if($api.StartTime.ToUniversalTime().Ticks -ne 639269653290947925L){throw 'Villa identity changed'}"]
    for pid, birth, folder, helper, status in TASKS:
        expected = str(SCRIPTS / helper).replace("'", "''")
        receipt = str(W / folder / 'RUN-RESULT.v1.json').replace("'", "''")
        script.append(f"$p=Get-Process -Id {pid}; if($p.StartTime.ToUniversalTime().Ticks -ne {birth}L){{throw 'PID identity changed'}}; $c=Get-CimInstance Win32_Process -Filter 'ProcessId={pid}'; if(!$c.CommandLine.Contains('{expected}') -or !$c.CommandLine.EndsWith(' run')){{throw 'Controller command differs'}}; $r=Get-Content -LiteralPath '{receipt}' -Raw|ConvertFrom-Json; if($r.status -ne '{status}'){{throw 'Controller is no longer waiting'}}")
    subprocess.run([str(PS), '-NoProfile', '-Command', '\n'.join(script)], check=True,
                   creationflags=subprocess.CREATE_NO_WINDOW)
    ROOT.mkdir()
    pins = {str(Path(__file__).resolve()): sha(__file__)}
    snapshots = []
    for pid, birth, folder, helper, status in TASKS:
        source = W / folder
        state = load(source / 'RUN-RESULT.v1.json')
        assert state['status'] == status
        backup = ROOT / folder
        backup.mkdir()
        for name in ['PREPARED.v1.json', 'RUN-RESULT.v1.json', 'JOB.v1.json']:
            original = source / name
            if original.is_file():
                data = original.read_bytes()
                (backup / name).write_bytes(data)
                assert sha(backup / name) == sha(original)
                if name == 'PREPARED.v1.json':
                    pins[str(original)] = sha(original)
        pins[str(SCRIPTS / helper)] = sha(SCRIPTS / helper)
        snapshots.append(dict(pid=pid, birth=birth, root=str(source), helper=helper,
                              previous_status=status, backup=str(backup)))
    save(ROOT / 'PREPARED.v1.json', dict(tasks=snapshots, input_pins=pins,
        reason='42 Dungeon and 3 Medieval native captures refuse empty HDRP subshader tags; diagnose context before more similar captures',
        active_villa_pid=2432, active_villa_birth=639269653290947925,
        captures_abandoned=False, sources_modified=False, native_unity_stopped=False))
    stopped = []
    for pid, birth, folder, helper, status in TASKS:
        source = W / folder
        receipt = str(source / 'RUN-RESULT.v1.json').replace("'", "''")
        # Stop downstream first so stopping an upstream waiter cannot release a capture.
        command = f"$ErrorActionPreference='Stop'; $api=Get-Process -Id 2432; if($api.StartTime.ToUniversalTime().Ticks -ne 639269653290947925L){{throw 'Active Villa ended; re-evaluate scheduling'}}; $p=Get-Process -Id {pid}; if($p.StartTime.ToUniversalTime().Ticks -ne {birth}L){{throw 'Controller identity changed'}}; $r=Get-Content -LiteralPath '{receipt}' -Raw|ConvertFrom-Json; if($r.status -ne '{status}'){{throw 'Controller became active'}}; if(Get-CimInstance Win32_Process -Filter 'ParentProcessId={pid}'|Where-Object {{$_.Name -eq 'Unity.exe'}}){{throw 'Native child must not be interrupted'}}; Stop-Process -Id {pid}; $p.WaitForExit()"
        subprocess.run([str(PS), '-NoProfile', '-Command', command], check=True,
                       creationflags=subprocess.CREATE_NO_WINDOW)
        state = load(source / 'RUN-RESULT.v1.json')
        state.update(status='deferred_for_native_hdrp_context_diagnostic',
                     prior_status=status, priority_receipt=str(ROOT / 'PREPARED.v1.json'),
                     deferred_utc=now(), captures_abandoned=False, native_unity_stopped=False)
        save(source / 'RUN-RESULT.v1.json', state)
        stopped.append(pid)
        save(ROOT / 'CONTROLLED-DEFER.v1.json', dict(stopped_waiting_controllers=stopped,
            tasks=snapshots, sources_modified=False, native_unity_stopped=False,
            all_requested_captures_retained=True, checked_utc=now()))
    assert all(sha(path) == expected for path, expected in pins.items())
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NO_WINDOW)
    save(ROOT / 'JOB.v1.json', dict(pid=child.pid, created_utc=now()))
    print(json.dumps(dict(priority_controller_pid=child.pid, deferred_controllers=stopped,
                         native_unity_stopped=False, sources_modified=False)))


def run():
    state = dict(status='waiting_for_exact_active_villa_before_hdrp_diagnostic',
                 full_import=False, captures_abandoned=False, native_unity_stopped=False)
    save(ROOT / 'RUN-RESULT.v1.json', state)
    try:
        prep = load(ROOT / 'PREPARED.v1.json')
        wait_exact(prep['active_villa_pid'], prep['active_villa_birth'])
        assert all(sha(path) == expected for path, expected in prep['input_pins'].items())
        state.update(status='running_prioritized_native_hdrp_context_diagnostic')
        save(ROOT / 'RUN-RESULT.v1.json', state)
        # Original pinned diagnostic and prepared inputs are reused unchanged.
        code = subprocess.call([sys.executable, '-B', str(SCRIPTS / 'queue_hdrp_context_diagnostic.py'), 'run'],
                               cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW)
        diagnostic = load(W / 'hdrp-native-context-diagnostic-v1/RUN-RESULT.v1.json')
        state.update(status='native_diagnostic_terminal_deferred_captures_require_context_resolution',
                     diagnostic_process_exit_code=code, diagnostic_status=diagnostic['status'],
                     diagnostic_receipt=str(W / 'hdrp-native-context-diagnostic-v1/RUN-RESULT.v1.json'),
                     diagnostic_receipt_sha256=sha(W / 'hdrp-native-context-diagnostic-v1/RUN-RESULT.v1.json'),
                     deferred_tasks=prep['tasks'])
        assert all(sha(path) == expected for path, expected in prep['input_pins'].items())
    except Exception as error:
        state.update(status='priority_operation_refused_preserved', error=str(error))
    state['finished_utc'] = now()
    save(ROOT / 'RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 else prepare()
