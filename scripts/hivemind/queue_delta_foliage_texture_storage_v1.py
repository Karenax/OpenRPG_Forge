"""Capture the exact imported storage of eighteen foliage textures after the URP queue."""
from pathlib import Path
import datetime,hashlib,json,os,re,subprocess,sys,runpy
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'unity-delta-foliage-texture-storage-v1'
OLD=W/'unity-villa-texture-source-probe-linear-v2';PROJECT=OLD/'project'
SOURCE=W/'hivemind-run/native-american-village/source'
PS=Path('C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe')
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,d):
 p=ROOT/name;t=p.with_suffix('.tmp');t.write_text(json.dumps(d,indent=2),encoding='utf-8');t.replace(p)
def check(pins):
 for p,h in pins.items():assert sha(p)==h,'Pinned source changed: '+p
def ps(command):return subprocess.check_output([str(PS),'-NoProfile','-Command',command],text=True,creationflags=subprocess.CREATE_NO_WINDOW).strip()
def prepare():
 assert not ROOT.exists()
 kernel_path=W/'delta-vector-node-wgsl-v3/RESULT.v1.json';kernels=read(kernel_path);assert kernels['naga_validated_materials']==6
 source_manifest_path=SOURCE.parent/'source.v1.json';manifest=read(source_manifest_path);files={f['stored_path'].replace('\\','/'):f for f in manifest['files']}
 pins={str(p):sha(p) for p in [kernel_path,source_manifest_path,Path(__file__).resolve()]};requests=[];bindings=[];textures={};material_pins={}
 for row in kernels['rows']:
  mat=SOURCE/manifest['guid_paths'][row['material_guid']];record=files['source/'+mat.relative_to(SOURCE).as_posix()];assert sha(mat)==record['sha256'];material_pins[str(mat)]=sha(mat);text=mat.read_text('utf-8-sig')
  for b in row['source_texture_bindings']:
   guid=b['guid'];png,meta=[Path(x['path']) for x in b['source_files']]
   assert png.resolve().is_relative_to(SOURCE.resolve()) and meta==Path(str(png)+'.meta') and png.suffix.lower()=='.png'
   for f,p in zip(b['source_files'],[png,meta]):assert sha(p)==f['sha256'];pins[str(p)]=sha(p)
   assert re.search(r'(?m)^guid:\s*'+guid+r'\s*$',meta.read_text('utf-8-sig'))
   ids=set(re.findall(r'm_Texture:\s*\{fileID:\s*(-?\d+),\s*guid:\s*'+re.escape(guid)+r',\s*type:\s*3\}',text));assert len(ids)==1,'Original material texture fileID not unique: '+guid
   file_id=ids.pop();identity=(str(png),sha(png),sha(meta),file_id)
   if guid in textures:assert textures[guid]['identity']==identity
   else:textures[guid]=dict(identity=identity,png=str(png),meta=str(meta),file_id=file_id)
   bindings.append(dict(material_guid=row['material_guid'],texture_guid=guid,texture_file_id=file_id,requires_normal_decode=b['requires_normal_decode']))
 assert len(textures)==18
 old=read(OLD/'PREPARED.v1.json');helper=PROJECT/'Assets/Editor/TextureSourceSnapshot.cs';verifier=OLD/'verify_texture_export.py'
 assert sha(helper)==old['exporter_sha256'] and sha(verifier)==old['verifier_sha256']
 pins[str(helper)]=sha(helper);pins[str(verifier)]=sha(verifier)
 # Existing private project resources stay intact; no duplicate GUIDs may be added.
 existing={}
 for p in (PROJECT/'Assets').rglob('*.meta'):
  match=re.search(r'(?m)^guid:\s*([0-9a-f]{32})\s*$',p.read_text('utf-8-sig'))
  if match:existing.setdefault(match[1],[]).append(p)
 assert not (set(existing)&set(textures)),'Existing project GUID collision; preserve existing assets'
 upstream=W/'unity-villa-prepared-native-urp-v2';job=read(upstream/'JOB.v2.json');pid=job['controller_pid']
 info=json.loads(ps(f'Get-CimInstance Win32_Process -Filter "ProcessId={pid}" | Select-Object ProcessId,CommandLine | ConvertTo-Json -Compress'))
 assert info['ProcessId']==pid and 'resume_villa_urp_prepared_v2.py run' in info['CommandLine']
 ticks=ps(f'(Get-Process -Id {pid}).StartTime.ToUniversalTime().Ticks');assert ticks.isdigit()
 ROOT.mkdir();copies=[]
 for guid,row in sorted(textures.items()):
  png,meta=Path(row['png']),Path(row['meta']);rel=Path('Assets/DeltaFoliageTextures')/guid/png.name;target=PROJECT/rel;assert not target.exists() and not Path(str(target)+'.meta').exists();target.parent.mkdir(parents=True)
  for source,dest in [(png,target),(meta,Path(str(target)+'.meta'))]:
   with dest.open('xb') as f:f.write(source.read_bytes())
   assert sha(dest)==pins[str(source)]
  copies.append(dict(original=str(png),original_sha256=sha(png),original_meta=str(meta),original_meta_sha256=sha(meta),project_copy=str(target),project_copy_sha256=sha(target),project_meta_copy=str(target)+'.meta',project_meta_copy_sha256=sha(str(target)+'.meta')))
  requests.append(dict(asset_id='hivemind.native-american-village.texture-'+guid,asset_path=rel.as_posix(),expected_guid=guid,expected_file_id=row['file_id'],expected_name=png.stem))
 new_helper=PROJECT/'Assets/Editor/DeltaFoliageTextureSourceSnapshotV1.cs';assert not new_helper.exists();text=helper.read_text('utf-8-sig');assert text.count('namespace OpenRPGImportProbe')==1;new_helper.write_text(text.replace('namespace OpenRPGImportProbe','namespace OpenRPGDeltaFoliageTexturesV1',1),encoding='utf-8');pins[str(new_helper)]=sha(new_helper)
 # Reuse actual installed-Unity references, retaining only compiler flags.
 rsp_source=W/'unity-villa-prepared-native-urp-v2/OriginalGraphMaterialSnapshotURP.rsp';pins[str(rsp_source)]=sha(rsp_source)
 flags=[x for x in rsp_source.read_text('utf-8-sig').splitlines() if x.startswith('-') and not x.startswith('-out:')]
 rsp=ROOT/'TextureSourceSnapshot.api.rsp';rsp.write_text('\n'.join(flags+[f'-out:"{ROOT / "TextureSourceSnapshot.api.dll"}"',f'"{new_helper}"']),encoding='utf-8')
 sdk=Path('F:/Unity/6000.5.8f1/Editor/Data/DotNetSdk');compilers=list((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'));assert len(compilers)==1
 with (ROOT/'api.log').open('xb') as log:code=subprocess.run([str(sdk/'dotnet.exe'),str(compilers[0]),'@'+str(rsp)],cwd=ROOT,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW).returncode
 assert code==0,'Texture helper API compilation refused; preserve api.log'
 save('manifest.json',dict(schema_version=1,output_root=str(ROOT/'export'),cases=requests))
 check(pins)
 prepared=dict(schema='openrpg.unity-texture-probe-prepared.v1',manifest_sha256=sha(ROOT/'manifest.json'),program_manifest=str(kernel_path),program_manifest_sha256=sha(kernel_path),copies=copies,material_pins=material_pins,bindings=bindings,texture_cases=18,original_materials=6,exporter_sha256=sha(new_helper),verifier_sha256=sha(verifier),project=str(PROJECT),helper=str(new_helper),input_pins=pins,wait_pid=pid,wait_birth_ticks=ticks,upstream_root=str(upstream),api_compile_exit_code=code,full_import=False,texture_importer_parity_qualified=False)
 save('PREPARED.v1.json',prepared)
 with (ROOT/'controller.log').open('xb') as log:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',dict(controller_pid=child.pid,created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()));print(json.dumps(dict(queued_controller_pid=child.pid,textures=18,api_compile_exit_code=code)))
def run():
 state=dict(status='waiting_for_exact_urp_controller',textures_verified=0,full_import=False,gpu_sampling_qualified=False,normal_decode_qualified=False,studio_verified=False);save('RUN-RESULT.v1.json',state)
 try:
  prep=read(ROOT/'PREPARED.v1.json');pid=prep['wait_pid'];ticks=prep['wait_birth_ticks'];check(prep['input_pins'])
  wait=f'while($true){{$p=Get-Process -Id {pid} -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne {ticks}L){{break}};$p.WaitForExit(45000)|Out-Null}}'
  subprocess.run([str(PS),'-NoProfile','-Command',wait],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
  upstream=read(Path(prep['upstream_root'])/'RUN-RESULT.v2.json');assert upstream.get('finished_utc'),'Upstream controller absent without a terminal receipt'
  state['upstream_terminal_status']=upstream['status'];assert int(ps('@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'))==0,'Another Unity job is active'
  check(prep['input_pins']);assert not (ROOT/'export').exists()
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
  with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
   child=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-force-d3d11','-quit','-projectPath',prep['project'],'-executeMethod','OpenRPGDeltaFoliageTexturesV1.TextureSourceSnapshot.Export','--openrpg-manifest',str(ROOT/'manifest.json'),'-logFile',str(ROOT/'unity.log')],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   state.update(status='capturing_eighteen_native_foliage_textures',unity_pid=child.pid);save('RUN-RESULT.v1.json',state);code=child.wait()
  state['unity_exit_code']=code;assert code==0,'Native texture capture refused; retained unity.log'
  native=read(ROOT/'export/RESULT.json');manifest=read(ROOT/'manifest.json');assert native['unity_version']=='6000.5.8f1' and native['color_space']=='Linear' and native['graphics_device_type']=='Direct3D11'
  assert native['total']==native['matched']==len(native['cases'])==18 and native['failed']==0
  verifier=runpy.run_path(str(OLD/'verify_texture_export.py'),run_name='trusted_texture_verifier');rows=[]
  for index,(request,row) in enumerate(zip(manifest['cases'],native['cases'])):rows.append(verifier['check_case'](request,row,ROOT/'export'/f'{index:03}'))
  check(prep['input_pins'])
  for copy in prep['copies']:
   assert sha(copy['original'])==copy['original_sha256'] and sha(copy['original_meta'])==copy['original_meta_sha256']
   assert sha(copy['project_copy'])==copy['project_copy_sha256']
  save('STORAGE-REOPEN.v1.json',dict(rows=rows,textures_verified=18,source_originals_unchanged=True,helper_sha256=prep['exporter_sha256'],native_result_sha256=sha(ROOT/'export/RESULT.json'),full_import=False,normal_decode_qualified=False,gpu_sampling_qualified=False))
  state.update(status='eighteen_native_texture_storage_captures_reopened',textures_verified=18,mips_verified=sum(r['mip_count'] for r in rows),native_result_sha256=sha(ROOT/'export/RESULT.json'),source_originals_unchanged=True,independent_reopen_still_required=True)
 except Exception as e:state.update(status='refused_or_interrupted',error=type(e).__name__+': '+str(e))
 state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
