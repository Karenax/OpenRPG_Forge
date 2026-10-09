from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys,os,shutil
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-s-paint-native-normal-decode-v1';PROJECT=W/'townsmith-s-paint-compressed-storage-v1/project';S=Path(__file__).parent
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,data):
 p=ROOT/name;t=p.with_suffix('.tmp');t.write_text(json.dumps(data,indent=2),encoding='utf-8');t.replace(p)
def prepare():
 assert not ROOT.exists()
 assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
 inventory_path=W/'townsmith-s-paint-texture-inventory-v1/RESULT.v1.json';inventory=read(inventory_path)
 proof_path=W/'townsmith-s-paint-native-resources-v1/RESOURCES.v1.json';proof=read(proof_path)
 pins=dict(proof['input_pins']);pins[str(proof_path)]=sha(proof_path);pins[str(inventory_path)]=sha(inventory_path)
 for p,h in pins.items():assert sha(p)==h,p
 selected=[r for r in inventory['rows'] if r['normal_profile_index'] is not None];assert sorted(r['normal_profile_index'] for r in selected)==[0,1,2]
 ROOT.mkdir();copies={}
 for row in selected:
  target=PROJECT/row['source_path'];assert sha(target)==row['source_sha256'];copies[str(target)]=sha(target);copies[str(target.with_suffix(target.suffix+'.meta'))]=sha(target.with_suffix(target.suffix+'.meta'))
 for folder in ['ProjectSettings','Packages']:
  for p in (PROJECT/folder).iterdir():
   if p.is_file():pins[str(p)]=sha(p)
 core=list((PROJECT/'Library/PackageCache').glob('com.unity.render-pipelines.core@*/ShaderLibrary/Packing.hlsl'));assert len(core)==1
 for p in [core[0],core[0].with_name('Common.hlsl')]:pins[str(p)]=sha(p)
 source_packing=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.core@a4c9a9793789/ShaderLibrary/Packing.hlsl'
 assert sha(source_packing)==sha(core[0]);pins[str(source_packing)]=sha(source_packing)
 for name in ['SourceTownsmithNormalDecodeV1.cs','SourceNormalMipDecodeV1.compute']:
  p=S/'candidates'/name;target=PROJECT/'Assets/Editor'/name;shutil.copy2(p,target);pins[str(p)]=sha(p);pins[str(target)]=sha(target)
 input_path=ROOT/'INPUT.v1.json';input_path.write_text(json.dumps({'rows':[{'guid':r['guid'],'authored_texture_index':r['authored_texture_index'],'normal_profile_index':r['normal_profile_index']} for r in selected]},indent=2),encoding='utf-8')
 for p in [input_path,Path(__file__),UNITY]:pins[str(p)]=sha(p)
 save('PREPARED.v1.json',{'input_pins':pins,'isolated_copy_pins':copies,'normal_texture_rows':selected,'full_import':False})
 with (ROOT/'controller.log').open('xb') as log:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',{'pid':child.pid});print(json.dumps({'pid':child.pid,'root':str(ROOT)}))
def run():
 state={'status':'native_normal_decode_capture_running','full_import':False}
 try:
  prep=read(ROOT/'PREPARED.v1.json')
  for p,h in {**prep['input_pins'],**prep['isolated_copy_pins']}.items():assert sha(p)==h,p
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-upm-cache'),OPENRPG_NORMAL_OUT=str(ROOT))
  with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
   child=subprocess.Popen([str(UNITY),'-batchmode','-force-d3d11','-quit','-job-worker-count','2','-projectPath',str(PROJECT),'-executeMethod','OpenRPGSourceTownsmithNormalDecodeV1.Export','-logFile',str(ROOT/'Unity.log')],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   state['unity_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait();assert child.returncode==0
  native=read(ROOT/'NATIVE-RESULT.v1.json');assert native['status']=='native_normal_decode_capture_complete' and len(native['rows'])==3
  for p,h in {**prep['input_pins'],**prep['isolated_copy_pins']}.items():assert sha(p)==h,p
  state.update(status='native_normal_decode_capture_finished_originals_unchanged',originals_unchanged=True,native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),artifact_pins={r['samples_file']:sha(r['samples_file']) for r in native['rows']},complete_assets_delivered_increment=0)
 except Exception as e:state.update(status='native_normal_decode_capture_refusal_retained',error=repr(e))
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if len(sys.argv)==1 else run()
