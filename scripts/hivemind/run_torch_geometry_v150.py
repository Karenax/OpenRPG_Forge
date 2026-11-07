"""Inspect persistent native mesh buffers, LOD and collider references on F."""
from pathlib import Path
from datetime import datetime,timezone
import ast,base64,ctypes,hashlib,json,os,shutil,subprocess,traceback
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'medieval-torch-native-geometry-v150';PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project';PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def save(state):
 state.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False);p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(state,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def main():
 assert not O.exists();O.mkdir();(O/'temp').mkdir();broker=None;namespace=None
 state=dict(status='preparing_native_torch_geometry',native_geometry_verified=False)
 try:
  helper=S/'candidates/TorchGeometryV150.cs';copy=O/helper.name;shutil.copyfile(helper,copy)
  prior=read(W/'medieval-torch-native-prefab-import-v137/manifest.json');request={k:prior[k] for k in ['prefab_path','prefab_guid']};request['output']=str(O/'NATIVE-RESULT.v1.json');(O/'manifest.json').write_text(json.dumps(request,indent=2),'utf-8')
  pins={str(p):sha(p) for p in (PROJECT/'Assets/OpenRPGTorchOriginalVariantV134').rglob('*') if p.is_file()};assert len(pins)>=50
  for p in [Path(__file__),helper,copy,O/'manifest.json',W/'medieval-torch-full-hierarchy-contract-v139/RESULT.v1.json',W/'medieval-torch-private-material-classification-v149/RESULT.v1.json']:pins[str(p)]=sha(p)
  (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins),indent=2),'utf-8')
  command=subprocess.check_output([PS,'-NoProfile','-Command','(Get-CimInstance Win32_Process -Filter "ProcessId=38332").CommandLine'],text=True).strip()
  if command:
   assert 'run_browser_source_glb_v141.py run' in command
   kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.OpenProcess.argtypes=[ctypes.c_ulong,ctypes.c_int,ctypes.c_ulong];kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_ulong];kernel.CloseHandle.argtypes=[ctypes.c_void_p];handle=kernel.OpenProcess(0x100000,False,38332);assert handle
   state.update(status='waiting_for_exact_browser_gpu_controller',waiting_pid=38332);save(state)
   try:assert kernel.WaitForSingleObject(handle,0xffffffff)==0
   finally:kernel.CloseHandle(handle)
  browser=read(W/'browser-source-glb-v141/RUN-RESULT.v1.json');assert 'finished_utc' in browser,'Require terminal browser receipt'
  count=subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip();assert count=='0','Preserve existing Unity session'
  for p,h in pins.items():assert sha(p)==h,p
  target=PROJECT/'Assets/Editor/TorchGeometryV150.cs';assert not target.exists();shutil.copyfile(copy,target)
  broker_helper=S/'queue_delta_firepit_particle_behavior_v4.py';tree=ast.parse(broker_helper.read_text('utf-8-sig'));selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}];namespace=dict(ROOT=O,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[]);exec(compile(ast.Module(body=selected,type_ignores=[]),str(broker_helper),'exec'),namespace);broker,label=namespace['start_broker'](state)
  env=os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
  with (O/'unity.stdout.log').open('xb') as out,(O/'unity.stderr.log').open('xb') as err:
   child=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-quit','-nographics','-upmIpcPath',label,'-projectPath',str(PROJECT),'-executeMethod','OpenRPGTorchGeometryV150.Export','--openrpg-manifest',str(O/'manifest.json'),'-logFile',str(O/'unity.log')],cwd=O,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='extracting_complete_native_torch_geometry',unity_pid=child.pid);save(state);state['unity_exit_code']=child.wait()
  for p,h in pins.items():assert sha(p)==h,p
  assert state['unity_exit_code']==0
  native=read(O/'NATIVE-RESULT.v1.json');assert native['source_components_unchanged'] and native['prefab_guid']==request['prefab_guid'];assert len(native['bindings'])==5 and len(native['lod_groups'])==1
  sizes=[]
  for mesh in native['meshes']:
   assert mesh['vertices']>0 and mesh['submeshes'] and any(a['semantic']=='Position' for a in mesh['attributes'])
   buffers={s['index']:base64.b64decode(s['bytes_base64'],validate=True) for s in mesh['streams']}
   for s in mesh['streams']:assert len(buffers[s['index']])==mesh['vertices']*s['stride']
   index=base64.b64decode(mesh['index_bytes_base64'],validate=True);stride={'UInt16':2,'UInt32':4}[mesh['index_format']];assert len(index)%stride==0
   for sub in mesh['submeshes']:assert sub['index_start']>=0 and sub['index_start']+sub['index_count']<=len(index)//stride
   sizes.append(dict(identity=mesh['identity'],vertices=mesh['vertices'],vertex_bytes=sum(map(len,buffers.values())),index_bytes=len(index)))
  state.update(status='complete_native_torch_geometry_buffers_lod_and_collider_exported',native_geometry_verified=True,geometry=sizes,native_receipt_sha256=sha(O/'NATIVE-RESULT.v1.json'),input_pins=pins,runtime_converted=False,studio_verified=False)
 except Exception as exc:state.update(status='native_torch_geometry_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
 finally:
  if broker is not None:namespace['stop_broker'](broker,state)
  state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)
if __name__=='__main__':main()
