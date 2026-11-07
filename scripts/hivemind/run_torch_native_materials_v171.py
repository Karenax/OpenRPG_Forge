"""Capture native material values, SDK shader and exact compressed mip chains."""
from pathlib import Path
from datetime import datetime,timezone
import ast,ctypes,hashlib,json,os,shutil,subprocess,sys,traceback
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'medieval-torch-native-materials-v171';PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project';PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe';PRIVATE=PROJECT/'Assets/OpenRPGTorchOriginalVariantV134'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(state):
 state.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False);p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(state,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
 assert not O.exists();O.mkdir();(O/'temp').mkdir();(O/'textures').mkdir();(O/'private-before').mkdir()
 material_helper=S/'candidates/TorchMaterialExportV171.cs';shutil.copyfile(material_helper,O/material_helper.name)
 shutil.copyfile(S/'candidates/SourceImportedTextureMipLoadV1.compute',O/'SourceImportedTextureMipLoadV1.compute')
 original=S/'candidates/SourceTownsmithCompressedStorageV2.cs';text=original.read_text('utf-8-sig');text=text.replace('OpenRPGSourceTownsmithCompressedStorageV2','OpenRPGTorchCompressedStorageV159').replace('input.rows.Length!=8','input.rows.Length!=9').replace('Eight compressed textures required','Nine compressed textures required').replace('eight_compressed_native_mip_chains_extracted_in_isolated_readable_copy','nine_compressed_native_mip_chains_extracted_in_isolated_readable_copy');assert 'input.rows.Length!=9' in text
 (O/'TorchCompressedStorageV159.cs').write_text(text,'utf-8')
 bindings=read(W/'medieval-torch-active-texture-bindings-v156/RESULT.v1.json');sources=Path('F:/OpenRPG_ImportWork/hivemind-run/medieval-kingdom/source');rows=[];expected_meta=[]
 for material in bindings['materials']:
  for binding in material['active_textures']:
   native=binding['native_binding'];rows.append(dict(guid=native['texture']['guid'],authored_texture_index=len(rows)));meta=PRIVATE/Path(binding['meta']).relative_to(sources);assert meta.is_file();expected_meta.append(str(meta))
 assert len(rows)==9 and len({r['guid'] for r in rows})==9
 (O/'textures/INPUT.v1.json').write_text(json.dumps(dict(rows=rows),indent=2),'utf-8')
 request=dict(output=str(O/'MATERIALS-NATIVE.v1.json'),generated_shader=str(O/'source-generated.shader'),graph_path='Assets/OpenRPGTorchOriginalVariantV134/Assets/Hivemind/MedievalKingdom/HDRP(Default)/Art/Shaders/S_BasicTextured.shadergraph',graph_guid='fc8e31a3d73f370561906623bcff4912',graph_file_id='-6465566751694194690',material_guids=[m['native_material_identity']['guid'] for m in bindings['materials']]);(O/'manifest.json').write_text(json.dumps(request,indent=2),'utf-8')
 pins={};snapshots={}
 for p in PRIVATE.rglob('*'):
  if not p.is_file():continue
  pins[str(p)]=sha(p);snap=O/'private-before'/p.relative_to(PRIVATE);snap.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,snap);assert sha(snap)==pins[str(p)];snapshots[str(p)]=str(snap)
 for p in [Path(__file__),material_helper,original,O/'TorchCompressedStorageV159.cs',O/'TorchMaterialExportV171.cs',O/'manifest.json',O/'textures/INPUT.v1.json',W/'medieval-torch-active-texture-bindings-v156/RESULT.v1.json',S/'candidates/SourceImportedTextureMipLoadV1.compute',O/'SourceImportedTextureMipLoadV1.compute']:
  pins[str(p)]=sha(p)
 (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_snapshots=snapshots,expected_private_texture_meta=expected_meta,native_expected=bindings),indent=2),'utf-8');save(dict(status='native_torch_material_export_prepared',native_materials_verified=False,compressed_mips_verified=False))
 with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=proc.pid,materials=3,textures=9,root=str(O))))
def run():
 state=read(O/'RUN-RESULT.v1.json');broker=None;namespace=None
 try:
  prep=read(O/'PREPARED.v1.json');pins=prep['input_pins'];command=subprocess.check_output([PS,'-NoProfile','-Command','(Get-CimInstance Win32_Process -Filter "ProcessId=11984").CommandLine'],text=True).strip()
  if command:
   assert 'run_browser_source_glb_v170.py run' in command;kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.OpenProcess.argtypes=[ctypes.c_ulong,ctypes.c_int,ctypes.c_ulong];kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_ulong];kernel.CloseHandle.argtypes=[ctypes.c_void_p];handle=kernel.OpenProcess(0x100000,False,11984);assert handle;state.update(status='waiting_for_exact_browser_glb_v170_controller',waiting_pid=11984);save(state)
   try:assert kernel.WaitForSingleObject(handle,0xffffffff)==0
   finally:kernel.CloseHandle(handle)
  assert 'finished_utc' in read(W/'browser-source-glb-v170/RUN-RESULT.v1.json')
  assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0','Preserve existing Unity session'
  for p,h in pins.items():assert sha(p)==h,p
  for name in ['TorchMaterialExportV171.cs','TorchCompressedStorageV159.cs','SourceImportedTextureMipLoadV1.compute']:
   target=PROJECT/'Assets/Editor'/name
   if target.exists():assert sha(target)==sha(O/name),'Existing trusted helper differs: '+str(target)
   else:shutil.copyfile(O/name,target)
  helper=S/'queue_delta_firepit_particle_behavior_v4.py';tree=ast.parse(helper.read_text('utf-8-sig'));selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}];namespace=dict(ROOT=O,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[]);exec(compile(ast.Module(body=selected,type_ignores=[]),str(helper),'exec'),namespace);broker,label=namespace['start_broker'](state)
  env=os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'),OPENRPG_COMPRESSED_OUT=str(O/'textures'))
  with (O/'unity.stdout.log').open('xb') as out,(O/'unity.stderr.log').open('xb') as err:
   proc=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-quit','-force-d3d11','-upmIpcPath',label,'-projectPath',str(PROJECT),'-executeMethod','OpenRPGTorchMaterialExportV171.Export','--openrpg-manifest',str(O/'manifest.json'),'-logFile',str(O/'unity.log')],env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='exporting_native_torch_material_values_shader_and_compressed_mips',unity_pid=proc.pid);save(state);state['unity_exit_code']=proc.wait()
  changes=[]
  for p,h in pins.items():
   actual=sha(p)
   if actual!=h:
    assert p in prep['expected_private_texture_meta'],'Unexpected input mutation: '+p
    assert sha(prep['private_snapshots'][p])==h;changes.append(dict(path=p,before_sha256=h,after_sha256=actual,before_snapshot=prep['private_snapshots'][p],classification_pending=True))
  state['private_meta_changes']=changes;assert state['unity_exit_code']==0
  material=read(O/'MATERIALS-NATIVE.v1.json');assert material['source_materials_unchanged'] and len(material['materials'])==3
  texture=read(O/'textures/NATIVE-RESULT.v1.json');assert texture['status']=='nine_compressed_native_mip_chains_extracted_in_isolated_readable_copy' and texture['graphics_api']=='Direct3D11';assert len(texture['rows'])==9
  output={str(O/'MATERIALS-NATIVE.v1.json'):sha(O/'MATERIALS-NATIVE.v1.json'),str(O/'source-generated.shader'):sha(O/'source-generated.shader'),str(O/'textures/NATIVE-RESULT.v1.json'):sha(O/'textures/NATIVE-RESULT.v1.json')};mips=0
  expected={t['native_binding']['texture']['guid']:t['native_binding'] for m in prep['native_expected']['materials'] for t in m['active_textures']}
  for row in texture['rows']:
   assert not row['error'] and row['gpu_samples_bit_exact'] and row['settings_restored_equal'];before=expected[row['guid']];assert row['graphics_format']==before['format'] and row['width']==before['width'] and row['height']==before['height'] and row['mip_count']==before['mips']
   whole=Path(row['whole_file']).read_bytes();combined=b''
   for mip in row['mips']:
    p=Path(mip['file']);assert p.resolve().is_relative_to((O/'textures').resolve());data=p.read_bytes();assert len(data)==mip['bytes'];combined+=data;output[str(p)]=sha(p);mips+=1
   assert combined==whole and len(whole)==row['whole_bytes'];assert Path(row['source_samples']).read_bytes()==Path(row['readable_samples']).read_bytes()
   for field in ['whole_file','source_samples','readable_samples']:output[row[field]]=sha(row[field])
  state.update(status='three_native_materials_shader_and_nine_compressed_mip_chains_verified',native_materials_verified=True,compressed_mips_verified=True,mip_count=mips,output_pins=output,private_metadata_byte_parity=not changes,private_importer_semantic_restoration_verified=True,full_shader_render_parity_qualified=False,studio_verified=False)
 except Exception as exc:state.update(status='native_torch_material_export_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
 finally:
  if broker is not None:namespace['stop_broker'](broker,state)
  state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)
if __name__=='__main__':
 if len(sys.argv)==1:prepare()
 else:assert sys.argv[1:] == ['run'];run()
