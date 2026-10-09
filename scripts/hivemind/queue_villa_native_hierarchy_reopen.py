"""Run the independent pose/binding review after the exact capture controller."""
import datetime
import json
from pathlib import Path
import subprocess
import sys
import time

from recover_native_scene_check_queue import wait_exact
from plan_villa_native_prefab_hierarchies import sha

WORK=Path('F:/OpenRPG_ImportWork')
ROOT=WORK/'villa-native-prefab-hierarchy-reopen-queue-v1'
CAPTURE=WORK/'villa-native-prefab-hierarchy-capture-v1'
HELPER=Path(__file__).with_name('verify_villa_native_prefab_hierarchies.py')
PLAN=WORK/'villa-native-prefab-hierarchy-plan-v1/PLAN.v1.json'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def save(name,value):
    target=ROOT/name
    temporary=target.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value,indent=2),'utf-8')
    for attempt in range(40):
        try:
            temporary.replace(target)
            return
        except PermissionError:
            if attempt==39:raise
            time.sleep(.25)


def prepare():
    assert not ROOT.exists(),'Existing reopener preserved'
    job=read(CAPTURE/'JOB.v1.json')
    birth=subprocess.check_output([PS,'-NoProfile','-Command',
        f'(Get-Process -Id {job["pid"]} -ErrorAction Stop).StartTime.ToUniversalTime().Ticks'],text=True).strip()
    assert birth.isdigit(),'Capture controller must be live now'
    compile(HELPER.read_text('utf-8-sig'),str(HELPER),'exec')
    paths=[Path(__file__).resolve(),HELPER,PLAN,CAPTURE/'PREPARED.v1.json',
           Path(__file__).with_name('recover_native_scene_check_queue.py'),
           Path(__file__).with_name('plan_villa_native_prefab_hierarchies.py'),
           Path(__file__).with_name('reconcile_villa_scene_transforms.py')]
    ROOT.mkdir()
    save('PREPARED.v1.json',{'wait_pid':job['pid'],'wait_birth_ticks':int(birth),
         'input_pins':{str(p):sha(p) for p in paths},'expected_logical_scenes':226})
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],
             cwd=ROOT,stdin=subprocess.DEVNULL,stdout=log,stderr=log,
             creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',{'pid':child.pid,'started_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({'pid':child.pid,'root':str(ROOT),'native_logical_scenes_reopened':0}))


def run():
    state={'status':'waiting_for_exact_native_capture_controller',
           'logical_scenes_reconciled':0,'gpu_rendered':False,'studio_verified':False,'full_import':False}
    save('RUN-RESULT.v1.json',state)
    try:
        prepared=read(ROOT/'PREPARED.v1.json')
        wait_exact(prepared['wait_pid'],prepared['wait_birth_ticks'])
        capture=read(CAPTURE/'RUN-RESULT.v1.json')
        assert capture['status']=='76_native_prefab_roots_captured_independent_reopen_pending'
        assert capture['native_prefab_roots_captured']==76
        assert sha(Path(capture['capture']))==capture['capture_sha256']
        for path,expected in prepared['input_pins'].items():assert sha(Path(path))==expected
        state['status']='independently_reopening_native_pose_and_binding_data'
        save('RUN-RESULT.v1.json',state)
        with (ROOT/'review.stdout.log').open('xb') as out,(ROOT/'review.stderr.log').open('xb') as err:
            child=subprocess.Popen([sys.executable,'-B',str(HELPER)],cwd=ROOT,stdout=out,stderr=err,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
            save('REVIEW-JOB.v1.json',{'pid':child.pid})
            assert child.wait()==0,'Review process failed; logs retained'
        result_path=WORK/'villa-native-prefab-hierarchy-independent-reopen-v1/RESULT.v1.json'
        result=read(result_path)
        for path,expected in prepared['input_pins'].items():assert sha(Path(path))==expected
        assert result['status']=='226_native_prefab_pose_and_renderer_bindings_reopened', \
            'Native poses/bindings disagree; retained detailed review must be resolved'
        assert result['logical_scenes_reconciled']==226 and not result['refusals'] and not result['changed_inputs']
        state.update(status='226_native_logical_scene_pose_and_bindings_reopened',
                     logical_scenes_reconciled=226,receipt=str(result_path),receipt_sha256=sha(result_path),
                     finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    except Exception as error:
        state.update(status='refused_or_unverified',error=str(error))
    save('RUN-RESULT.v1.json',state)


if __name__=='__main__':
    prepare() if len(sys.argv)==1 else run()
