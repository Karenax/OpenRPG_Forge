"""Install tested common sources only after the pinned engine job succeeds."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys,os,time,ctypes,shutil
S=Path(__file__).parent;R=S.parents[1];W=Path('F:/OpenRPG_ImportWork');ROOT=W/'native-texture-capture-common-install-v4';CAND=W/'native-texture-identity-contract-candidate-v3';ENGINE=W/'source-graph-ten-texture-engine-check-v3';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,d):
 p=ROOT/name;t=p.with_suffix('.tmp');t.write_text(json.dumps(d,indent=2),encoding='utf-8');t.replace(p)
def live(pid):
 k=ctypes.WinDLL('kernel32',use_last_error=True);k.OpenProcess.restype=ctypes.c_void_p;k.GetExitCodeProcess.argtypes=[ctypes.c_void_p,ctypes.POINTER(ctypes.c_ulong)];k.CloseHandle.argtypes=[ctypes.c_void_p]
 h=k.OpenProcess(0x1000,False,int(pid))
 if not h:return False
 try:
  value=ctypes.c_ulong();assert k.GetExitCodeProcess(h,ctypes.byref(value));return value.value==259
 finally:k.CloseHandle(h)
def prepare():
 assert not ROOT.exists();job=read(ENGINE/'RUN-RESULT.v1.json')
 assert (job['status']=='ten_texture_engine_binding_tests_running' and live(job['cargo_pid'])) or (job['status']=='ten_texture_engine_binding_tests_passed_inputs_unchanged' and job['actual_exit_code']==0)
 basispath=W/'native-capture-common-format-epoch-v1/RESULT.v1.json';basis=read(basispath)
 assert basis['status']=='common_source_change_is_canonical_formatting_only'
 for p,h in basis['input_pins'].items():assert sha(p)==h,p

 verified=read(W/'native-texture-identity-contract-check-v3/RESULT.v1.json');assert verified['test_exit_code']==0
 for p,h in verified['input_pins'].items():
  frozen=basis['historical_source_snapshots'].get(p)
  if frozen and sha(p)!=h:assert sha(frozen)==h,p
  else:assert sha(p)==h,p

 assert '33 passed; 0 failed' in (W/'native-texture-identity-contract-check-v3/tests.stdout.log').read_text(encoding='utf-8')
 expected=basis['expected_live_source_pins']
 for p,h in expected.items():assert sha(p)==h,p
 ROOT.mkdir();before=ROOT/'before';before.mkdir();snapshots={}
 for path,h in expected.items():
  target=before/Path(path).name;shutil.copy2(path,target);assert sha(target)==h;snapshots[path]=str(target)
 pins={str(p):sha(p) for p in CAND.glob('*.rs')};pins[str(Path(__file__))]=sha(__file__);pins[str(basispath)]=sha(basispath);pins[str(Path(__file__).with_name('queue_native_capture_common_install_v1.py'))]=sha(Path(__file__).with_name('queue_native_capture_common_install_v1.py'))
 save('PREPARED.v1.json',{'candidate_pins':pins,'expected_live_source_pins':expected,'historical_source_snapshots':basis['historical_source_snapshots'],'current_source_snapshots':snapshots,'upstream_cargo_pid':job['cargo_pid'],'full_import':False})
 with (ROOT/'controller.log').open('xb') as log:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',{'pid':child.pid});print(json.dumps({'pid':child.pid,'root':str(ROOT),'upstream_cargo_pid':job['cargo_pid']}))
def run():
 state={'status':'common_install_waiting_for_live_upstream_engine_tests','full_import':False,'complete_assets_delivered_increment':0}
 try:
  prep=read(ROOT/'PREPARED.v1.json');state['upstream_cargo_pid']=prep['upstream_cargo_pid'];save('RUN-RESULT.v1.json',state)
  while live(prep['upstream_cargo_pid']):time.sleep(3)
  # Process completion precedes the upstream controller's atomic final receipt.
  for attempt in range(10):
   upstream=read(ENGINE/'RUN-RESULT.v1.json')
   if upstream['status']!='ten_texture_engine_binding_tests_running':break
   time.sleep(2)
  assert upstream['actual_exit_code']==0 and upstream['status']=='ten_texture_engine_binding_tests_passed_inputs_unchanged',upstream
  for p,h in {**prep['candidate_pins'],**prep['expected_live_source_pins']}.items():assert sha(p)==h,p
  assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process cargo -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0','Other Cargo still active; installation not started'
  parent=R/'crates/forge_assets/src/source_graph_bundle.rs';storage=R/'crates/forge_assets/src/source_graph_bundle/imported_storage.rs'
  text=(CAND/'source_graph_bundle.candidate.rs').read_text(encoding='utf-8').replace('"imported_storage.candidate.rs"','"source_graph_bundle/imported_storage.rs"').replace('"scalar_parameters.rs"','"source_graph_bundle/scalar_parameters.rs"')
  payloads={parent:text.encode('utf-8'),storage:(CAND/'imported_storage.candidate.rs').read_bytes()}
  rustfmt=BIN/'rustfmt.exe'
  for path,data in list(payloads.items()):
   formatted=subprocess.run([str(rustfmt),'--edition','2024','--config','skip_children=true'],input=data,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW)
   assert formatted.returncode==0,(path,formatted.stderr.decode('utf-8',errors='replace'))
   payloads[path]=formatted.stdout
  for path,data in payloads.items():
   assert path.resolve().is_relative_to(R.resolve());temp=path.with_suffix('.native-capture.tmp');assert not temp.exists();temp.write_bytes(data);temp.replace(path)
  installed={str(p):sha(p) for p in payloads};save('INSTALLED.v1.json',{'installed_source_pins':installed,'historical_source_snapshots':prep['historical_source_snapshots'],'upstream_receipt_sha256':sha(ENGINE/'RUN-RESULT.v1.json'),'full_import':False})
  # Advance current resource evidence without rewriting any historic capture/receipt.
  oldpath=W/'townsmith-s-paint-native-resources-v1/RESOURCES.v1.json';resources=read(oldpath);currentpins={};remapped={}
  for p,h in resources['input_pins'].items():
   if p in prep['historical_source_snapshots']:
    frozen=prep['historical_source_snapshots'][p];assert sha(frozen)==h;currentpins[frozen]=h;remapped[p]={'snapshot':frozen,'historical_sha256':h}
   else:assert sha(p)==h,p;currentpins[p]=h
  currentpins.update(installed);currentpins[str(oldpath)]=sha(oldpath)
  resources.update(input_pins=currentpins,historical_contract_input_snapshots=remapped,prepared_against_current_common_source=True,renderer_material_contract_admission_verified=False,full_import=False)
  save('RESOURCES-CURRENT.v2.json',resources)
  env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
  for label,package,filtertext in [('assets','forge_assets','source_graph_bundle'),('engine','forge_engine','source_graph::'),('unlit','forge_engine','source_unlit_forward::'),('program','forge_engine','source_graph_program::'),('glb','forge_engine','source_graph_glb::')]:
   command=[str(BIN/'cargo.exe'),'test','--locked','--offline','-p',package,'--lib',filtertext];save(label+'.COMMAND.v1.json',{'command':command,'cwd':str(R)})
   with (ROOT/(label+'.stdout.log')).open('xb') as out,(ROOT/(label+'.stderr.log')).open('xb') as err:
    child=subprocess.Popen(command,cwd=R,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='installed_common_'+label+'_tests_running',cargo_pid=child.pid);save('RUN-RESULT.v1.json',state);state[label+'_exit_code']=child.wait();assert child.returncode==0,label
   for p,h in installed.items():assert sha(p)==h,p
  state.update(status='common_native_capture_installed_workspace_tests_passed',installed_source_pins=installed,historical_source_snapshots=prep['historical_source_snapshots'],actual_material_render_qualified=False,forge_editor_verified=False)
 except Exception as e:state.update(status='common_native_capture_install_or_tests_refusal_retained',error=repr(e))
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if len(sys.argv)==1 else run()
