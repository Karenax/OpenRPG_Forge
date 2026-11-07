"""One-shot dependent checks; never relaunch Unity or install unreviewed images."""
from pathlib import Path
import datetime, hashlib, json, subprocess, sys, time

R=Path(__file__).resolve().parents[2]; W=Path('F:/OpenRPG_ImportWork')
O=W/'pirate-lod-next-dependent-checks-v795'; CAP=W/'pirate-lod-thumbnail-batch-next-v791'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
assert not O.exists(); O.mkdir()
def save(state):
    state['utc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
    p=O/'STATE.tmp';p.write_text(json.dumps(state,indent=2),'utf-8');p.replace(O/'STATE.v1.json')
def alive(pid, expected):
    command=f"$p=Get-CimInstance Win32_Process -Filter 'ProcessId = {int(pid)}'; if($p -and $p.CommandLine -like '*{expected}*') {{'live'}}"
    return subprocess.check_output([PS,'-NoProfile','-Command',command],text=True).strip()=='live'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
scripts=[Path(__file__),R/'scripts/hivemind/audit_pirate_lod_next_v793.py',R/'scripts/hivemind/reopen_pirate_lod_next_v794.py',
    R/'scripts/hivemind/audit_and_guard_pirate_lod_batch04_v493.py',R/'scripts/hivemind/audit_and_guard_pirate_lod_batch03_v369.py',
    R/'scripts/hivemind/reopen_pirate_lod_thumbnail_batch03_v370.py']
pins={str(p):sha(p) for p in scripts}
(O/'INPUT-PINS.v1.json').write_text(json.dumps(pins,indent=2),'utf-8')
state=dict(status='waiting_for_existing_capture',full_import_increment=0,images_installed_in_vault=False)
save(state)
try:
    while True:
        receipt=CAP/'RUN-RESULT.v1.json'
        if receipt.exists():
            run=json.loads(receipt.read_text('utf-8-sig'))
            if run.get('finished_utc'):
                assert run.get('unity_exit_code')==0,run
                break
        job=CAP/'JOB.v1.json'
        if job.exists():
            pid=json.loads(job.read_text('utf-8-sig'))['controller_pid']
        else:
            pid=19540
        if not alive(pid,'run_pirate_lod_thumbnail_next_v791.py'):
            # Re-read after process exit to avoid racing its final receipt.
            if receipt.exists() and json.loads(receipt.read_text('utf-8-sig')).get('finished_utc'):
                continue
            raise RuntimeError('Capture preparation/controller ended without a complete receipt; no relaunch')
        time.sleep(5)
    for script in scripts[1:3]:
        state.update(status='checking_capture',step=script.name);save(state)
        with (O/(script.stem+'.log')).open('wb') as log:
            code=subprocess.call([sys.executable,'-B',str(script)],cwd=R,stdout=log,stderr=log)
        if code!=0:raise RuntimeError(f'{script.name} exit {code}; inspect retained log')
    changed=[p for p,h in pins.items() if sha(p)!=h]
    assert not changed,changed
    state.update(status='capture_guards_and_png_reopen_complete_visual_review_pending')
except Exception as exc:
    state.update(status='dependent_capture_check_failed',error=repr(exc))
finally:
    save(state);(O/'RESULT.v1.json').write_text(json.dumps(state,indent=2),'utf-8')
    print(json.dumps(state))
