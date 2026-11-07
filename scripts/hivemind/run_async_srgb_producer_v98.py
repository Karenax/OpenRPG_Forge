from pathlib import Path
import subprocess,sys,os,json,hashlib
from datetime import datetime,timezone
S=Path(__file__).parent;R=S.parents[1];W=Path('F:/OpenRPG_ImportWork');O=W/'async-srgb-producer-v98';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin');E=R/'crates/forge_engine/examples/verify_async_srgb_producer_v98.rs'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(v):
 t=O/'RUN-RESULT.tmp';t.write_text(json.dumps(v,indent=2),encoding='utf-8');t.replace(O/'RUN-RESULT.v1.json')
if len(sys.argv)==1:
 assert not O.exists();assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process cargo -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0';subprocess.run([str(BIN/'rustfmt.exe'),'--edition','2024','--config','newline_style=Unix',str(E)],check=True);O.mkdir();pins={str(p):sha(p) for p in [Path(__file__),E,R/'crates/forge_engine/src/source_graph_srgb_async.rs',R/'crates/forge_engine/src/source_graph_srgb_runtime.rs',R/'crates/forge_engine/src/source_graph.rs',R/'crates/forge_engine/src/source_graph_srgb_measure.rs',R/'crates/forge_engine/src/source_graph_srgb_curve.rs',R/'Cargo.lock',R/'Cargo.toml',R/'.cargo/config.toml']};(O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,purchased_asset_inputs=0,unity_pixel_inputs=0,full_import=False),indent=2),encoding='utf-8');save(dict(status='generated_srgb_transfer_ramp_prepared',full_import=False))

 with (O/'controller.log').open('xb') as f:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],stdout=f,stderr=f,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=child.pid)),encoding='utf-8');print(json.dumps(dict(controller_pid=child.pid,root=str(O))));sys.exit()
state=dict(status='generated_srgb_transfer_ramp_compiling',full_import=False)
try:
 prep=json.loads((O/'PREPARED.v1.json').read_text());
 for key,h in prep['input_pins'].items():assert sha(Path(key))==h,key
 env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(O),TMP=str(O));cmd=[str(BIN/'cargo.exe'),'run','--locked','--offline','-p','forge_engine','--example','verify_async_srgb_producer_v98','--',str(O)]
 with (O/'stdout.log').open('xb') as out,(O/'stderr.log').open('xb') as err:
  child=subprocess.Popen(cmd,cwd=R,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state['cargo_pid']=child.pid;save(state);state['actual_exit_code']=child.wait()
 assert state['actual_exit_code']==0;native=json.loads((O/'NATIVE-RESULT.v1.json').read_text());assert native['curve_bytes']==1024 and native['wrong_device_rejected'] and native['gpu_errors']==[];state.update(status=native['status'],gpu_probe_executed=True,native_receipt_sha256=sha(O/'NATIVE-RESULT.v1.json'))
 for key,h in prep['input_pins'].items():assert sha(Path(key))==h,key
except Exception as e:state.update(status='generated_srgb_transfer_probe_refusal_retained',error=repr(e),traceback=__import__('traceback').format_exc())
state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)
