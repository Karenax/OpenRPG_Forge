"""Exercise actual uniform encoding and invalid-basis refusals in an isolated test crate."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,subprocess,os,sys
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'source-diffuse-probe-packet-check-v1'
ENGINE=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/src')
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,d):
    p=ROOT/name;t=p.with_suffix(p.suffix+'.tmp');t.write_text(json.dumps(d,indent=2),'utf-8');t.replace(p)
def prepare():
    assert not ROOT.exists();ROOT.mkdir();(ROOT/'src').mkdir();(ROOT/'temp').mkdir()
    (ROOT/'Cargo.toml').write_text('[package]\nname="openrpg-source-diffuse-probe-packet-check-v1"\nversion="0.1.0"\nedition="2024"\n[workspace]\n[dependencies]\nbevy={version="=0.19.1",default-features=false,features=["3d","ui"]}\n','utf-8')
    (ROOT/'src/main.rs').write_text('#[path="'+str(ENGINE/'source_graph_diffuse_probe.rs').replace('\\','/')+'"]\nmod probe;\nfn main() {}\n','utf-8')
    # The isolated lock may shed unused engine packages; exact Bevy is retained.
    (ROOT/'Cargo.lock.before').write_bytes((W/'firepit-forge-shadow-pair-v4/Cargo.lock').read_bytes())
    (ROOT/'Cargo.lock').write_bytes((ROOT/'Cargo.lock.before').read_bytes())
    files=[Path(__file__).resolve(),ENGINE/'source_graph_diffuse_probe.rs',ENGINE/'shaders/source_diffuse_probe.wgsl',ROOT/'Cargo.toml',ROOT/'src/main.rs',ROOT/'Cargo.lock.before']
    save('PREPARED.v1.json',dict(input_pins={str(p):sha(p) for p in files},expected_tests=2,network=False,full_import=False))
    with (ROOT/'controller.log').open('xb') as f:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=f,stderr=f,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(pid=child.pid,created_utc=datetime.now(timezone.utc).isoformat()));print(json.dumps(dict(pid=child.pid,root=str(ROOT))))
def run():
    state=dict(status='actual_diffuse_probe_packet_tests_running',gpu_rendered=False,full_import=False)
    try:
        pins=json.loads((ROOT/'PREPARED.v1.json').read_text())['input_pins']
        for p,h in pins.items():assert sha(p)==h,p
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'))
        command=[str(BIN/'cargo.exe'),'test','--offline','--bin','openrpg-source-diffuse-probe-packet-check-v1','--','--nocapture']
        save('COMMAND.v1.json',dict(command=command,cwd=str(ROOT)))
        with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
            child=subprocess.Popen(command,cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state['cargo_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait()
        changed=[p for p,h in pins.items() if sha(p)!=h]
        stdout=(ROOT/'stdout.log').read_text('utf-8');passed=state['actual_exit_code']==0 and not changed and '2 passed; 0 failed' in stdout
        state.update(status='actual_diffuse_probe_uniform_encoding_and_refusals_verified' if passed else 'diffuse_probe_packet_test_refusal_retained',tests_verified=2 if passed else 0,input_pins=pins,changed_source_files=changed,lock_sha256=sha(ROOT/'Cargo.lock'),stdout_sha256=sha(ROOT/'stdout.log'),stderr_sha256=sha(ROOT/'stderr.log'))
    except Exception as e:state.update(status='diffuse_probe_packet_tests_refused',error=repr(e))
    state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
