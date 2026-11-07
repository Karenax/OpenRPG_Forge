"""One-shot continuation of the already running copy, then prepare only; never launch Unity."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, subprocess, sys, time, traceback

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
O = W/'mountain-preparation-continuation-v874'
STAGE = W/'mountain-hdrp-capture-stage-v869/RESULT.v1.json'
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
PREPARE = R/'scripts/hivemind/prepare_mountain_exact_capture_v873.py'
EXPECTED_PID = 34940

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()

def source_process():
    code = f"Get-CimInstance Win32_Process -Filter 'ProcessId={EXPECTED_PID}' | Select-Object ProcessId,Name,CommandLine,ReadTransferCount,WriteTransferCount | ConvertTo-Json -Compress"
    output = subprocess.check_output([PS,'-NoProfile','-Command',code],text=True).strip()
    process = json.loads(output) if output else None
    if process:
        assert process['Name'] == 'python.exe' and 'stage_mountain_hdrp_capture_v869.py' in process['CommandLine'], 'PID reused by another process'
    return process

def save(state):
    state['utc'] = datetime.now(timezone.utc).isoformat()
    (O/'PROGRESS.v1.json').write_text(json.dumps(state,indent=2),'utf-8')

if len(sys.argv) == 1:
    assert not O.exists()
    assert STAGE.exists() or source_process(), 'No completed stage or verified live copy'
    O.mkdir()
    pins = {str(p):sha(p) for p in [Path(__file__),PREPARE,R/'scripts/hivemind/stage_mountain_hdrp_capture_v869.py']}
    (O/'INPUT-PINS.v1.json').write_text(json.dumps(pins,indent=2),'utf-8')
    with (O/'controller.log').open('xb') as log:
        proc = subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],cwd=R,stdout=log,stderr=log,
                                stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,one_shot=True,unity_launch=False)),'utf-8')
    print(json.dumps(dict(status='one_shot_preparation_continuation_started',pid=proc.pid,root=str(O))))
else:
    assert sys.argv[1:] == ['run']
    state = dict(status='waiting_on_verified_live_copy',unity_launched=False,images_admitted=0,full_import=False)
    try:
        while not STAGE.exists():
            process = source_process()
            assert process, 'Stage process ended without a completed receipt; preserve partial project for diagnosis'
            state['copy_process'] = process
            save(state)
            time.sleep(10)
        pins = json.loads((O/'INPUT-PINS.v1.json').read_text('utf-8'))
        assert all(sha(p)==h for p,h in pins.items()), 'Preparation inputs changed'
        state['status'] = 'copy_receipt_exists_preparing_exact_capture'
        save(state)
        state['prepare_exit_code'] = subprocess.call([sys.executable,'-B',str(PREPARE)],cwd=R)
        assert state['prepare_exit_code'] == 0
        prepared = W/'mountain-exact-source-capture-v873/PREPARED.v1.json'
        state.update(status='exact_capture_prepared_no_unity_launched',prepared=str(prepared),prepared_sha256=sha(prepared))
    except Exception as exc:
        state.update(status='preparation_refusal_preserved',error=repr(exc),traceback=traceback.format_exc())
    save(state)
    (O/'RESULT.v1.json').write_text(json.dumps(state,indent=2),'utf-8')
