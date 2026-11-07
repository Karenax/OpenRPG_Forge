from pathlib import Path
from datetime import datetime,timezone
import ast,ctypes,hashlib,json,os,shutil,subprocess,sys,traceback
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');O=W/'medieval-torch-native-prefab-import-v137';PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project';PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def save(state):
 state.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False);tmp=O/'RUN-RESULT.tmp';tmp.write_text(json.dumps(state,indent=2),'utf-8');tmp.replace(O/'RUN-RESULT.v1.json')
state={'status':'waiting_for_exact_native_tint_variant_controller','lot':'L06.MEDIEVAL.TORCH.001','native_import_verified':False};broker=None;namespace=None
try:
 kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.OpenProcess.argtypes=[ctypes.c_ulong,ctypes.c_int,ctypes.c_ulong];kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_ulong];kernel.CloseHandle.argtypes=[ctypes.c_void_p]
 cmd=subprocess.check_output([PS,'-NoProfile','-Command','(Get-CimInstance Win32_Process -Filter "ProcessId=41532").CommandLine'],text=True).strip()
 if cmd:
  assert 'run_medieval_torch_tint_variant_v129.py' in cmd;handle=kernel.OpenProcess(0x100000,False,41532);assert handle;state['waiting_pid']=41532;save(state)
  try:assert kernel.WaitForSingleObject(handle,0xffffffff)==0
  finally:kernel.CloseHandle(handle)
 prior=read(W/'medieval-torch-native-tint-variant-v129/QUEUE.v1.json');assert prior['status'] in ['native_variant_capture_terminal','queued_native_variant_refusal_retained']
 count=subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip();assert count=='0','Preserve existing Unity session'
 prep=read(O/'PREPARED.v1.json')
 for path,h in prep['input_pins'].items():assert sha(path)==h,path
 clone=read(W/'medieval-torch-private-prefab-clone-v134/RESULT.v1.json');fragment=W/'medieval-torch-private-prefab-clone-v134/private-source-fragment';destination=PROJECT/'Assets/OpenRPGTorchOriginalVariantV134';assert destination.resolve().is_relative_to((PROJECT/'Assets').resolve()) and not destination.exists();destination.mkdir();installed={}
 for original,h in clone['output_pins'].items():
  source=Path(original);assert sha(source)==h;target=destination/source.relative_to(fragment);assert target.resolve().is_relative_to(destination.resolve());target.parent.mkdir(parents=True,exist_ok=True)
  with source.open('rb') as src,target.open('xb') as dst:shutil.copyfileobj(src,dst)
  assert sha(target)==h;installed[str(target)]=h
 helper=O/'TorchPrefabImportV137.cs';target=PROJECT/'Assets/Editor/TorchPrefabImportV137.cs';assert not target.exists();shutil.copyfile(helper,target);installed[str(target)]=sha(helper)
 (O/'INSTALLED-BEFORE.v1.json').write_text(json.dumps(dict(installed_pins=installed),indent=2),'utf-8')
 broker_helper=S/'queue_delta_firepit_particle_behavior_v4.py';tree=ast.parse(broker_helper.read_text('utf-8-sig'));selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}];namespace=dict(ROOT=O,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[]);exec(compile(ast.Module(body=selected,type_ignores=[]),str(broker_helper),'exec'),namespace);broker,label=namespace['start_broker'](state)
 env=os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
 with (O/'unity.stdout.log').open('xb') as out,(O/'unity.stderr.log').open('xb') as err:
  child=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-quit','-force-d3d11','-upmIpcPath',label,'-projectPath',str(PROJECT),'-executeMethod','OpenRPGTorchPrefabImportV137.Export','--openrpg-manifest',str(O/'manifest.json'),'-logFile',str(O/'unity.log')],cwd=O,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='importing_and_inspecting_complete_private_torch_prefab',unity_pid=child.pid);save(state);state['unity_exit_code']=child.wait()
 changes=[dict(path=p,before=h,after=sha(p)) for p,h in installed.items() if sha(p)!=h];state['private_sdk_import_changed_files']=changes
 for path,h in prep['input_pins'].items():assert sha(path)==h,path
 assert state['unity_exit_code']==0,'Native import refused; preserve installed copy and logs'
 native_path=O/'NATIVE-RESULT.v1.json';native=read(native_path);request=read(O/'manifest.json');assert native['prefab_guid']==request['prefab_guid'] and native['original_prefab_components_unchanged'];assert len(native['nodes'])==8 and native['particle_systems']==2;assert all(not x['asset_path'] for x in native['unresolved']);state.update(status='complete_private_torch_prefab_native_import_and_hierarchy_inspected',native_import_verified=True,native_receipt_sha256=sha(native_path),private_sdk_import_changed_files=changes,original_authoring_version_parity=False,material_rendering_verified=False,temporal_behavior_verified=False,studio_verified=False)
except Exception as e:state.update(status='native_prefab_import_refusal_retained',error=repr(e),traceback=traceback.format_exc())
finally:
 if broker is not None:namespace['stop_broker'](broker,state)
 save(state)
