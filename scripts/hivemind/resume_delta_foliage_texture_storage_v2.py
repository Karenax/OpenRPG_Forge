"""Capture the exact imported storage of eighteen foliage textures after the URP queue."""
from pathlib import Path
import datetime,hashlib,json,os,re,subprocess,sys,runpy
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'unity-delta-foliage-texture-storage-v2'
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
 prior=W/'unity-delta-foliage-texture-storage-v1';prep=read(prior/'PREPARED.v1.json');check(prep['input_pins'])
 assert prep['texture_cases']==18 and prep['original_materials']==6 and prep['api_compile_exit_code']==0
 assert sha(prior/'manifest.json')==prep['manifest_sha256']
 state=read(prior/'RUN-RESULT.v1.json');job=read(prior/'JOB.v1.json');pid=job['controller_pid']
 info=ps(f'Get-CimInstance Win32_Process -Filter "ProcessId={pid}" | Select-Object ProcessId,CommandLine | ConvertTo-Json -Compress')
 if info:
  record=json.loads(info);assert record['ProcessId']==pid and 'queue_delta_foliage_texture_storage_v1.py run' in record['CommandLine']
  assert state['status']=='waiting_for_exact_urp_controller' and not (prior/'export').exists()
  ticks=ps(f'(Get-Process -Id {pid}).StartTime.ToUniversalTime().Ticks');assert ticks.isdigit()
 else:
  assert state.get('finished_utc');ticks=''
 ROOT.mkdir()
 manifest=read(prior/'manifest.json');manifest['output_root']=str(ROOT/'export');save('manifest.json',manifest)
 pins=dict(prep['input_pins']);pins[str(Path(__file__).resolve())]=sha(Path(__file__).resolve());pins[str(prior/'PREPARED.v1.json')]=sha(prior/'PREPARED.v1.json');pins[str(prior/'manifest.json')]=sha(prior/'manifest.json')
 prep.update(manifest_sha256=sha(ROOT/'manifest.json'),input_pins=pins,prior_root=str(prior),prior_wait_pid=pid,prior_wait_birth_ticks=ticks,existing_copies_reused=True,powershell_normal_process_exit_repaired=True)
 save('PREPARED.v1.json',prep)
 with (ROOT/'controller.log').open('xb') as log:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',dict(controller_pid=child.pid,created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()));print(json.dumps(dict(queued_controller_pid=child.pid,textures=18,existing_copies_reused=True)))

def run():
 state=dict(status='waiting_for_exact_urp_controller',textures_verified=0,full_import=False,gpu_sampling_qualified=False,normal_decode_qualified=False,studio_verified=False);save('RUN-RESULT.v1.json',state)
 try:
  prep=read(ROOT/'PREPARED.v1.json');check(prep['input_pins']);prior=Path(prep['prior_root']);previous_pid=prep['prior_wait_pid'];previous_ticks=prep['prior_wait_birth_ticks']
  if previous_ticks:
   prior_wait=f'while($true){{$p=Get-Process -Id {previous_pid} -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne {previous_ticks}L){{break}};$p.WaitForExit(45000)|Out-Null}};exit 0'
   subprocess.run([str(PS),'-NoProfile','-Command',prior_wait],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
  previous=read(prior/'RUN-RESULT.v1.json');assert previous.get('finished_utc'),'Prior controller absent without a terminal receipt'
  if previous.get('textures_verified')==18:
   preserved=read(prior/'STORAGE-REOPEN.v1.json');assert preserved['textures_verified']==len(preserved['rows'])==18 and preserved['source_originals_unchanged'];assert sha(prior/'export/RESULT.json')==preserved['native_result_sha256'];check(prep['input_pins'])
   state.update(status='prior_successful_capture_preserved',textures_verified=18,capture_root=str(prior),native_result_sha256=preserved['native_result_sha256'],previous_storage_reopen_sha256=sha(prior/'STORAGE-REOPEN.v1.json'),independent_reopen_still_required=True,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat());save('RUN-RESULT.v1.json',state);return
  assert previous['status']=='refused_or_interrupted' and not (prior/'export').exists(),'Partial native exports require case reconciliation before retry'
  state['prior_terminal_receipt_sha256']=sha(prior/'RUN-RESULT.v1.json');state['prior_error_preserved']=previous.get('error')
  pid=prep['wait_pid'];ticks=prep['wait_birth_ticks'];check(prep['input_pins'])
  wait=f'while($true){{$p=Get-Process -Id {pid} -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne {ticks}L){{break}};$p.WaitForExit(45000)|Out-Null}};exit 0'
  subprocess.run([str(PS),'-NoProfile','-Command',wait],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
  upstream=read(Path(prep['upstream_root'])/'RUN-RESULT.v2.json');assert upstream.get('finished_utc'),'Upstream controller absent without a terminal receipt'
  state['upstream_terminal_status']=upstream['status'];assert int(ps("@(Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\").Count"))==0,'Another Unity job is active'
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
