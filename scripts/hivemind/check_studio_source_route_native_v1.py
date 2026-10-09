"""Test the actual common carrier route, including the preserved FirePit source scene."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,os,sys,subprocess,ctypes,ctypes.wintypes as wt
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');W=Path('F:/OpenRPG_ImportWork');ROOT=W/'studio-source-renderer-route-native-v1';PRIOR=W/'studio-current-source-wasm-check-v6';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
MODULE=R/'apps/forge_studio/src/authoring_assets/source_graph_route.rs'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(state):
    state['updated_utc']=datetime.now(timezone.utc).isoformat();tmp=ROOT/'RUN-RESULT.tmp';tmp.write_text(json.dumps(state,indent=2),'utf-8');tmp.replace(ROOT/'RUN-RESULT.v1.json')
def prepare():
    assert not ROOT.exists();fixtures=list((W/'firepit-complete-static-glb-v2').glob('*.candidate.glb'));assert len(fixtures)==1;fixture=fixtures[0]
    assert sha(fixture)=='a8df029352894ef3f588f341141a3aa81ff446bd5268ecf205c4aeef37622ce6'
    ROOT.mkdir();(ROOT/'src').mkdir()
    cargo=ROOT/'Cargo.toml';cargo.write_text('[package]\nname="openrpg-studio-source-route-check"\nversion="0.1.0"\nedition="2024"\n[workspace]\n[dependencies]\nforge_assets={path="'+(R/'crates/forge_assets').as_posix()+'"}\n','utf-8')
    wrapper=ROOT/'src/lib.rs';wrapper.write_text('#[path="'+MODULE.as_posix()+'"]\nmod source_graph_route;\n#[test]\nfn actual_preserved_source_scene_requires_its_source_renderer(){let bytes=std::fs::read("'+fixture.as_posix()+'").unwrap();assert!(source_graph_route::read(&bytes).unwrap().is_some());assert!(source_graph_route::require_ordinary_loader(&bytes).is_err());}\n','utf-8')
    files=[MODULE,Path(__file__).resolve(),fixture,cargo,wrapper,R/'Cargo.toml',R/'Cargo.lock']
    for folder in [R/'crates/forge_assets',R/'crates/forge_core']:
        files += [p for p in folder.rglob('*') if p.is_file() and p.suffix in ('.rs','.toml','.wgsl')]
    pins={str(p):sha(p) for p in files};(ROOT/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,expected_tests=5,wait_pid=read(PRIOR/'JOB.v1.json')['pid']),indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(pid=child.pid),indent=2),'utf-8');print(json.dumps(dict(pid=child.pid,expected_tests=5)))
def run():
    prep=read(ROOT/'PREPARED.v1.json');state=dict(status='waiting_for_existing_frontend_check',actual_tests_passed=0,gpu_rendered=False,actual_editor_global_qualified=False,full_import=False);save(state)
    try:
        prior=read(PRIOR/'RUN-RESULT.v1.json')
        if 'actual_exit_code' not in prior:
            kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.OpenProcess.argtypes=[wt.DWORD,wt.BOOL,wt.DWORD];kernel.OpenProcess.restype=wt.HANDLE;kernel.WaitForSingleObject.argtypes=[wt.HANDLE,wt.DWORD];kernel.WaitForSingleObject.restype=wt.DWORD;kernel.CloseHandle.argtypes=[wt.HANDLE]
            handle=kernel.OpenProcess(0x100000,False,prep['wait_pid'])
            if handle:
                try:assert kernel.WaitForSingleObject(handle,3*60*60*1000)==0
                finally:kernel.CloseHandle(handle)
        prior=read(PRIOR/'RUN-RESULT.v1.json');assert prior.get('actual_exit_code')==0,'Frontend compile failure must be addressed first'
        for name,digest in prep['input_pins'].items():assert sha(name)==digest, name
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
        for stage,args in [('lock',['generate-lockfile','--offline']),('native',['test','--locked','--offline','--lib','--','--nocapture'])]:
            state['status']='checking_actual_studio_route_'+stage;save(state)
            with (ROOT/(stage+'.stdout.log')).open('xb') as out,(ROOT/(stage+'.stderr.log')).open('xb') as err:
                child=subprocess.Popen([str(BIN/'cargo.exe'),*args],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
                state['cargo_pid']=child.pid;save(state);state['actual_exit_code']=child.wait();assert state['actual_exit_code']==0,stage
        log=(ROOT/'native.stdout.log').read_text('utf-8',errors='replace');assert '5 passed; 0 failed' in log
        for name,digest in prep['input_pins'].items():assert sha(name)==digest,name
        state.update(status='five_actual_route_tests_and_preserved_source_scene_passed',actual_tests_passed=5,input_pins=prep['input_pins'],stdout_sha256=sha(ROOT/'native.stdout.log'),stderr_sha256=sha(ROOT/'native.stderr.log'),source_renderer_integrated=False)
    except Exception as exc:
        state.update(status='actual_source_route_test_refusal_retained',error=repr(exc))
    finally:save(state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
