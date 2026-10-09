"""One-shot final storage review after the existing SHA controller exits."""
from pathlib import Path
from datetime import datetime, timezone
import ctypes, ctypes.wintypes as wt
import hashlib,json,os,subprocess,sys
S=Path(__file__).resolve().parent
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'vault-current-final-review-job-v2'
SOURCE=W/'vault-indexed-coverage-plan-v1/RUN-RESULT.v2.json'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(value):
    value['updated_utc']=datetime.now(timezone.utc).isoformat()
    tmp=ROOT/'RUN-RESULT.tmp'
    tmp.write_text(json.dumps(value,indent=2),'utf-8');tmp.replace(ROOT/'RUN-RESULT.v1.json')
def prepare():
    assert not ROOT.exists(), 'Do not duplicate the final reviewer'
    run=read(SOURCE)
    assert run['status'] in ('running','all_uncovered_physical_sha_batches_completed')
    assert not (W/'vault-current-final-review-v4').exists()
    files=[Path(__file__).resolve(), S/'close_current_vault_integrity_v4.py',S/'map_indexed_vault_coverage_v1.py']
    ROOT.mkdir()
    prep=dict(source_controller_pid=run['controller_pid'],source_checkpoint=str(SOURCE),input_pins={str(p):sha(p) for p in files},vault_written=False,recurring_automation_created=False)
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=child.pid),indent=2),'utf-8')
    print(json.dumps(dict(controller_pid=child.pid,source_controller_pid=run['controller_pid'])))
def run():
    prep=read(ROOT/'PREPARED.v1.json')
    state=dict(status='waiting_for_owned_sha_controller',controller_pid=os.getpid(),source_controller_pid=prep['source_controller_pid'],whole_current_vault_qualified=False,full_import=False)
    handle=None
    try:
        for name,value in prep['input_pins'].items():assert sha(Path(name))==value
        save(state)
        if read(SOURCE)['status']=='running':
            kernel=ctypes.WinDLL('kernel32',use_last_error=True)
            kernel.OpenProcess.argtypes=[wt.DWORD,wt.BOOL,wt.DWORD];kernel.OpenProcess.restype=wt.HANDLE
            kernel.WaitForSingleObject.argtypes=[wt.HANDLE,wt.DWORD];kernel.WaitForSingleObject.restype=wt.DWORD
            kernel.GetExitCodeProcess.argtypes=[wt.HANDLE,ctypes.POINTER(wt.DWORD)];kernel.GetExitCodeProcess.restype=wt.BOOL
            kernel.CloseHandle.argtypes=[wt.HANDLE];kernel.CloseHandle.restype=wt.BOOL
            handle=kernel.OpenProcess(0x100000|0x1000,False,prep['source_controller_pid'])
            if not handle:
                assert read(SOURCE)['status']=='all_uncovered_physical_sha_batches_completed', 'Missing controller without terminal proof'
            else:
                result=kernel.WaitForSingleObject(handle,3*60*60*1000)
                assert result==0, ('No terminal process observation',result)
                code=wt.DWORD();assert kernel.GetExitCodeProcess(handle,ctypes.byref(code))
                assert code.value==0, ('Source SHA controller failed',code.value)
                state['source_controller_observed_exit_code']=code.value
        source=read(SOURCE)
        assert source['status']=='all_uncovered_physical_sha_batches_completed' and source['completed_batches']==source['batch_count']==206
        for name,value in prep['input_pins'].items():assert sha(Path(name))==value
        state['status']='reviewing_complete_current_vault_and_original_transfer';save(state)
        env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT))
        with (ROOT/'review.stdout.log').open('xb') as out,(ROOT/'review.stderr.log').open('xb') as err:
            child=subprocess.Popen([sys.executable,'-B',str(S/'close_current_vault_integrity_v4.py')],cwd=ROOT,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state['review_pid']=child.pid;save(state);state['review_exit_code']=child.wait()
        assert state['review_exit_code']==0, 'Final storage reviewer retained a refusal'
        final=W/'vault-current-final-review-v4/RESULT.v1.json'
        receipt=read(final);assert receipt['whole_current_vault_qualified'] and not receipt['full_import']
        for name,value in prep['input_pins'].items():assert sha(Path(name))==value
        state.update(status='whole_current_vault_integrity_review_completed',receipt=str(final),receipt_sha256=sha(final),whole_current_vault_qualified=True,actual_editor_global_qualified=False,conversion_fidelity_qualified=False)
    except Exception as error:
        state.update(status='final_storage_review_refusal_retained',error=repr(error))
        raise
    finally:
        if handle:kernel.CloseHandle(handle)
        save(state)
if __name__=='__main__':
    prepare() if sys.argv[-1]=='prepare' else run()
