from pathlib import Path
from datetime import datetime,timezone
import subprocess,os,json,hashlib,sys
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'source-graph-ten-texture-engine-check-v1';R=Path(__file__).parents[2]
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,data):
 p=ROOT/name;t=p.with_suffix('.tmp');t.write_text(json.dumps(data,indent=2),encoding='utf-8');t.replace(p)
def prepare():
 assert not ROOT.exists()
 # An unrelated rustc can link a separate SDK checkout without owning this cache.
 guard="@(Get-Process cargo -ErrorAction SilentlyContinue).Count; @(Get-CimInstance Win32_Process -Filter \"Name='rustc.exe'\" | Where-Object { $_.CommandLine -like '*firepit-static-glb-product-check-v2*' }).Count"
 assert subprocess.check_output(['powershell','-NoProfile','-Command',guard],text=True).split()==['0','0']
 ROOT.mkdir();files=[R/p for p in ['crates/forge_assets/src/source_graph_bundle.rs','crates/forge_engine/src/source_graph.rs','crates/forge_engine/src/source_graph_glb.rs']]+[Path(__file__)]
 save('PREPARED.v1.json',{'input_pins':{str(p):sha(p) for p in files},'full_import':False})
 with (ROOT/'controller.log').open('xb') as log:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',{'pid':child.pid});print(json.dumps({'pid':child.pid,'root':str(ROOT)}))
def run():
 state={'status':'ten_texture_engine_binding_tests_running','full_import':False}
 try:
  prep=json.loads((ROOT/'PREPARED.v1.json').read_text())
  for p,h in prep['input_pins'].items():assert sha(p)==h,p
  env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
  command=[str(BIN/'cargo.exe'),'test','--locked','--offline','-p','forge_engine','--lib','source_graph::ten_texture_binding_tests']
  save('COMMAND.v1.json',{'command':command,'cwd':str(R)})
  with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
   child=subprocess.Popen(command,cwd=R,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   state['cargo_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait();assert child.returncode==0
  for p,h in prep['input_pins'].items():assert sha(p)==h,p
  state.update(status='ten_texture_engine_binding_tests_passed_inputs_unchanged',stdout_sha256=sha(ROOT/'stdout.log'),stderr_sha256=sha(ROOT/'stderr.log'),engine_binding_tests_verified=True,actual_material_gpu_layout_verified=False,complete_assets_delivered_increment=0)
 except Exception as e:state.update(status='ten_texture_engine_binding_tests_refusal_retained',error=repr(e))
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if len(sys.argv)==1 else run()
