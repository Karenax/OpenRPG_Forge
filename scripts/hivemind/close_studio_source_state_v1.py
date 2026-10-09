"""One-shot closure of the state sub-lot; no scheduler or new compiler."""
from pathlib import Path
from datetime import datetime, timezone
import ctypes, ctypes.wintypes as wt, hashlib, json, subprocess, sys

W=Path('F:/OpenRPG_ImportWork'); R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
ROOT=W/'studio-source-state-install-v1'; NATIVE=W/'studio-source-state-native-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(p,v):
    t=p.with_suffix('.tmp');t.write_text(json.dumps(v,indent=2),'utf-8');t.replace(p)
def prepare():
    assert not (ROOT/'CLOSURE-JOB.v1.json').exists()
    with (ROOT/'closure.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    write(ROOT/'CLOSURE-JOB.v1.json',dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()))
    print(json.dumps(dict(pid=proc.pid,one_shot=True)))
def run():
    result=dict(status='waiting_for_native_state_tests',gpu_rendered=False,studio_verified=False,full_import=False)
    write(ROOT/'CLOSURE-RUN.v1.json',result)
    try:
        native=read(NATIVE/'RUN-RESULT.v1.json')
        if native['status'].startswith('checking_'):
            kernel=ctypes.WinDLL('kernel32',use_last_error=True)
            kernel.OpenProcess.argtypes=[wt.DWORD,wt.BOOL,wt.DWORD];kernel.OpenProcess.restype=wt.HANDLE
            kernel.WaitForSingleObject.argtypes=[wt.HANDLE,wt.DWORD];kernel.WaitForSingleObject.restype=wt.DWORD
            kernel.CloseHandle.argtypes=[wt.HANDLE]
            handle=kernel.OpenProcess(0x100000,False,read(NATIVE/'JOB.v1.json')['pid'])
            if handle:
                try:assert kernel.WaitForSingleObject(handle,3*60*60*1000)==0,'Native job did not terminate'
                finally:kernel.CloseHandle(handle)
        native=read(NATIVE/'RUN-RESULT.v1.json')
        assert native['status']=='seven_actual_state_tests_and_both_preserved_scenes_passed',native['status']
        assert native['actual_tests_passed']==7 and native['actual_exit_code']==0
        assert (native['nodes_verified'],native['renderer_declarations_verified'],native['lod_groups_retained'])==(95,63,14)
        compile_path=W/'studio-current-source-wasm-check-v8/RUN-RESULT.v1.json'
        compile=read(compile_path)
        assert compile['status']=='actual_studio_wasm_frontend_compile_check_passed' and not compile['changed_source_files']
        for name,digest in native['input_pins'].items():assert sha(name)==digest,name
        installed=read(ROOT/'INSTALLED.v1.json')
        for name,digest in installed['source_pins'].items():assert sha(name)==digest,name
        changed=[name for name,digest in compile['input_pins'].items() if sha(name)!=digest]
        assert not changed,repr(changed)
        pins={str(p):sha(p) for p in [NATIVE/'RUN-RESULT.v1.json',compile_path,ROOT/'INSTALLED.v1.json',W/'rocks-firepit-renderer-state-glb-v1/INDEPENDENT-REOPEN.v1.json']}
        result.update(status='installed_scene_state_adapter_native_and_full_web_compile_reopened',actual_tests_passed=7,canonical_installed=True,actual_compile_qualified=True,nodes_verified=95,renderer_declarations_verified=63,lod_groups_retained=14,camera_lod_driver_qualified=False,source_pins=installed['source_pins'],proof_pins=pins,frontend_source_pins_reopened=len(compile['input_pins']))
        write(ROOT/'RESULT.v1.json',result)
    except Exception as exc:result.update(status='source_state_closure_refusal_retained',error=repr(exc))
    finally:
        result['finished_utc']=datetime.now(timezone.utc).isoformat();write(ROOT/'CLOSURE-RUN.v1.json',result)
        if result['status']=='installed_scene_state_adapter_native_and_full_web_compile_reopened':write(ROOT/'RESULT.v1.json',result)
        subprocess.run([sys.executable,'-B',str(R/'scripts/hivemind/record_batch_dashboard_v3.py')],check=True)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
