"""Check the actual WebGPU Studio sources; caches and logs stay on F:."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,subprocess
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-lod-studio-web-check-v463'
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
assert not O.exists()
O.mkdir();(O/'temp').mkdir()
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
files=[R/'Cargo.toml',R/'Cargo.lock',Path(__file__),R/'apps/forge_studio/src/authoring_assets.rs']+[R/'apps/forge_studio/src/authoring_assets'/n for n in ['source_graph_lod.rs','source_graph_host.rs','source_graph_runtime.rs','source_graph_state.rs']]
pins={str(p):sha(p) for p in files}
command=[str(BIN/'cargo.exe'),'check','--offline','--locked','-p','forge_studio','--lib','--target','wasm32-unknown-unknown','--no-default-features','--features','web']
env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_TARGET_DIR=str(W/'source-graph-private-boundary-v1/target-rust198-explicit-web-v8'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',TEMP=str(O/'temp'),TMP=str(O/'temp'))
state=dict(status='prepared',command=command,input_pins=pins,full_import=False,studio_gui_verified=False)
def save():
 state['updated_utc']=datetime.now(timezone.utc).isoformat();p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(state,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
save()
with (O/'cargo.log').open('xb') as log:
 proc=subprocess.Popen(command,cwd=R,env=env,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='checking_actual_studio_webgpu',cargo_pid=proc.pid);save();code=proc.wait()
state.update(cargo_exit_code=code,inputs_unchanged=all(sha(p)==h for p,h in pins.items()),log_sha256=sha(O/'cargo.log'))
state['status']='actual_studio_webgpu_lod_check_passed' if code==0 and state['inputs_unchanged'] else 'actual_studio_webgpu_lod_check_refused'
save();print(json.dumps(dict(status=state['status'],exit_code=code)))
