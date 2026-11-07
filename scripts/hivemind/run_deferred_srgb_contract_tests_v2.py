from pathlib import Path
from datetime import datetime,timezone
import os,subprocess,json,hashlib
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');W=Path('F:/OpenRPG_ImportWork');ROOT=W/'deferred-srgb-contract-tests-v2';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(d):
 p=ROOT/'ENGINE-RESULT.v1.json';tmp=p.with_suffix('.tmp');tmp.write_text(json.dumps(d,indent=2),encoding='utf-8');tmp.replace(p)
paths=[R/'Cargo.lock',R/'Cargo.toml',R/'crates/forge_engine/src/source_graph.rs',R/'crates/forge_assets/src/source_graph_bundle.rs',Path(__file__)]
paths += [R/'crates/forge_engine/src/source_graph_glb.rs']
for crate in ['forge_engine','forge_assets','forge_navigation']:
 paths += list((R/'crates'/crate/'src').rglob('*.rs'))
pins={str(p):sha(p) for p in paths};command=[str(BIN/'cargo.exe'),'test','--locked','--offline','-p','forge_engine','deferred_srgb_required_resource_refuses_absence_removal_and_corruption']
env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='1',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
d={'status':'test_running','command':command,'input_pins':pins,'started_utc':datetime.now(timezone.utc).isoformat(),'incremental_compiler_cache_enabled':True,'scope':'required resource dimensions, absence, removal and corruption; no rendering fidelity qualification','full_import':False};save(d)
with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
 child=subprocess.Popen(command,cwd=R,env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW);d['cargo_pid']=child.pid;save(d);d['actual_exit_code']=child.wait()
d['inputs_unchanged']=all(sha(Path(p))==h for p,h in pins.items());d['status']='test_passed' if d['actual_exit_code']==0 and d['inputs_unchanged'] else 'test_refused';d['finished_utc']=datetime.now(timezone.utc).isoformat();save(d)
