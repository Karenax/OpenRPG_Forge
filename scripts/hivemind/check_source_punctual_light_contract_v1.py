"""Isolated tests for source light mounting and hierarchy admission."""
from pathlib import Path
import datetime, hashlib, json, os, subprocess, sys
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'source-punctual-light-contract-check-v1'
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,value):
    p=ROOT/name;t=p.with_suffix('.tmp');t.write_text(json.dumps(value,indent=2),'utf-8');t.replace(p)
def prepare():
    assert not ROOT.exists()
    prior=json.loads((W/'firepit-punctual-scene-v2/RUN-RESULT.v1.json').read_text())
    assert 'actual_exit_code' in prior,'Native compiler must be terminal first'
    ROOT.mkdir();(ROOT/'src').mkdir()
    files=[R/'apps/forge_studio/src/authoring_assets/source_graph_lights.rs',R/'crates/forge_engine/src/source_graph_scene.rs']
    for name in ['Cargo.toml','Cargo.lock']:(ROOT/name).write_bytes((W/'firepit-punctual-scene-v2'/name).read_bytes())
    (ROOT/'src/main.rs').write_text('\n'.join(f'#[allow(dead_code)] #[path="{p.as_posix()}"] mod {p.stem};' for p in files)+'\nfn main() {}\n','utf-8')
    files += [Path(__file__).resolve(),ROOT/'Cargo.toml',ROOT/'Cargo.lock',ROOT/'src/main.rs']
    save('PREPARED.v1.json',{'input_pins':{str(p):sha(p) for p in files},'expected_tests':7,'full_import':False})
    with (ROOT/'controller.log').open('xb') as out:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],stdout=out,stderr=out,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',{'pid':child.pid});print(json.dumps({'pid':child.pid,'root':str(ROOT)}))
def run():
    state={'status':'source_light_contract_tests_running','full_import':False}
    try:
        pins=json.loads((ROOT/'PREPARED.v1.json').read_text())['input_pins']
        assert all(sha(p)==h for p,h in pins.items())
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
        with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
            child=subprocess.Popen([str(BIN/'cargo.exe'),'test','--locked','--offline','--','--nocapture'],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid']=child.pid;save('RUN-RESULT.v1.json',state)
            state['actual_exit_code']=child.wait()
        assert state['actual_exit_code']==0
        assert '7 passed; 0 failed' in (ROOT/'stdout.log').read_text()
        assert all(sha(p)==h for p,h in pins.items())
        state.update(status='source_light_photometry_ownership_and_hierarchy_refusals_verified',tests_verified=7,input_pins=pins)
    except Exception as error:state.update(status='source_light_contract_test_refusal_retained',error=repr(error))
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if len(sys.argv)==1 else run()
