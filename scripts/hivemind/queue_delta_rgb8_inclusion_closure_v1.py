"""Wait for the exact v4 controller, then resume only its include-file refusal."""
from pathlib import Path
import datetime
import json
import subprocess
import sys
from verify_native_urp_material_snapshot import sha

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'delta-rgb8-inclusion-closure-queue-v1'
OLD=W/'delta-rgb8-frozen-canonical-loader-v4'
NEXT=Path(__file__).with_name('resume_delta_rgb8_frozen_loader_v5.py')
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'

def read(path):return json.loads(Path(path).read_text('utf-8-sig'))
def save(name,value):
    p=ROOT/name; temporary=p.with_suffix('.tmp')
    temporary.write_text(json.dumps(value,indent=2),encoding='utf-8');temporary.replace(p)

def prepare():
    assert not ROOT.exists()
    old=read(OLD/'RUN-RESULT.v1.json');pid=read(OLD/'JOB.v1.json')['controller_pid']
    birth='0'
    if not old.get('finished_utc'):
        birth=subprocess.check_output([PS,'-NoProfile','-Command',
            f'$p=Get-Process -Id {pid} -ErrorAction SilentlyContinue;if($p){{$p.StartTime.ToUniversalTime().Ticks}}'],
            text=True,creationflags=subprocess.CREATE_NO_WINDOW).strip()
        assert birth.isdigit(), 'Missing controller without terminal evidence; inspect instead of restarting'
    ROOT.mkdir()
    save('PREPARED.v1.json',dict(wait_pid=pid,wait_birth_ticks=birth,
        input_pins={str(p):sha(p) for p in [Path(__file__),NEXT,OLD/'PREPARED.v1.json']},full_import=False))
    with (ROOT/'controller.log').open('xb') as out:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdin=subprocess.DEVNULL,stdout=out,stderr=out,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid,created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(controller_pid=child.pid,wait_for_exact_controller=pid,restart_live_job=False)))

def run():
    state=dict(status='waiting_for_exact_frozen_v4_controller',full_import=False)
    save('RUN-RESULT.v1.json',state)
    try:
        prepared=read(ROOT/'PREPARED.v1.json')
        assert all(sha(p)==h for p,h in prepared['input_pins'].items())
        pid,birth=prepared['wait_pid'],prepared['wait_birth_ticks']
        command=(f'while($true){{$p=Get-Process -Id {pid} -ErrorAction SilentlyContinue;'
            f'if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne {birth}){{break}};Start-Sleep -Seconds 5}};exit 0')
        subprocess.run([PS,'-NoProfile','-Command',command],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
        old=read(OLD/'RUN-RESULT.v1.json');assert old.get('finished_utc')
        if old.get('actual_exit_code')==0 and old.get('actual_host_tests_passed')==17:
            state.update(status='previous_success_preserved_no_retry',retry_launched=False)
        else:
            assert old['actual_exit_code']==101 and old['status']=='refused_or_unverified'
            assert all(sha(p)==h for p,h in prepared['input_pins'].items())
            with (ROOT/'recovery.stdout.log').open('xb') as out,(ROOT/'recovery.stderr.log').open('xb') as err:
                code=subprocess.run([sys.executable,'-B',str(NEXT)],cwd=ROOT,stdout=out,stderr=err,
                    creationflags=subprocess.CREATE_NO_WINDOW).returncode
            assert code==0, 'Closure preparation refused; preserve recovery logs'
            next_job=W/'delta-rgb8-frozen-canonical-loader-v5/JOB.v1.json'
            state.update(status='terminal_include_refusal_recovered_v5_launched',retry_launched=True,
                previous_terminal_receipt_sha256=sha(OLD/'RUN-RESULT.v1.json'),
                next_job=str(next_job),next_job_sha256=sha(next_job))
    except Exception as error:
        state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error))
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
