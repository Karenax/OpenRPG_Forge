from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys,os,shutil
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-s-paint-compressed-storage-v1';PROJECT=ROOT/'project'
BASE=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project';S=Path(__file__).parent
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,data):
 p=ROOT/name;t=p.with_suffix('.tmp');t.write_text(json.dumps(data,indent=2),encoding='utf-8');t.replace(p)
def prepare():
 assert not ROOT.exists()
 assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
 proof_path=W/'townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json';proof=read(proof_path)
 pins=dict(proof['input_pins']);pins[str(proof_path)]=sha(proof_path)
 for p,h in pins.items():assert sha(p)==h,p
 inventory_path=W/'townsmith-s-paint-texture-inventory-v1/RESULT.v1.json';inventory=read(inventory_path)
 selected=[r for r in inventory['rows'] if r['guid'] in proof['refused_texture_guids']];assert len(selected)==8
 ROOT.mkdir();PROJECT.mkdir();copies={}
 for folder in ['ProjectSettings','Packages']:
  (PROJECT/folder).mkdir()
  for p in (BASE/folder).iterdir():
   if p.is_file():
    target=PROJECT/folder/p.name;shutil.copy2(p,target);pins[str(p)]=sha(p);copies[str(target)]=sha(target)
 for row in selected:
  for source,target in [(Path(row['source_file']),PROJECT/row['source_path']),(Path(row['original_meta']),PROJECT/(row['source_path']+'.meta'))]:
   target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target);assert sha(target)==sha(source);copies[str(target)]=sha(target)
 for name in ['SourceTownsmithCompressedStorageV1.cs','SourceImportedTextureMipLoadV1.compute']:
  p=S/'candidates'/name;target=PROJECT/'Assets/Editor'/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target);pins[str(p)]=sha(p);pins[str(target)]=sha(target)
 input_path=ROOT/'INPUT.v1.json';input_path.write_text(json.dumps({'rows':[{'guid':r['guid'],'authored_texture_index':r['authored_texture_index']} for r in selected]},indent=2),encoding='utf-8')
 for p in [inventory_path,input_path,Path(__file__),UNITY]:pins[str(p)]=sha(p)
 save('PREPARED.v1.json',{'input_pins':pins,'isolated_copy_before_pins':copies,'source_texture_rows':selected,'full_import':False})
 with (ROOT/'controller.log').open('xb') as log:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',{'pid':child.pid});print(json.dumps({'pid':child.pid,'root':str(ROOT)}))
def run():
 state={'status':'isolated_townsmith_compressed_texture_extraction_running','full_import':False}
 try:
  prep=read(ROOT/'PREPARED.v1.json')
  for p,h in {**prep['input_pins'],**prep['isolated_copy_before_pins']}.items():assert sha(p)==h,p
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-upm-cache'),OPENRPG_COMPRESSED_OUT=str(ROOT))
  with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
   child=subprocess.Popen([str(UNITY),'-batchmode','-force-d3d11','-quit','-job-worker-count','2','-projectPath',str(PROJECT),'-executeMethod','OpenRPGSourceTownsmithCompressedStorageV1.Export','-logFile',str(ROOT/'Unity.log')],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   state['unity_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait();assert child.returncode==0
  native=read(ROOT/'NATIVE-RESULT.v1.json');assert len(native['rows'])==8
  for p,h in prep['input_pins'].items():assert sha(p)==h,p
  for p,h in prep['isolated_copy_before_pins'].items():
   if '/Assets/' in p.replace('\\','/') and not p.endswith('.meta'):assert sha(p)==h,p
  artifact_pins={}
  for row in native['rows']:
   for p in [row.get('whole_file'),row.get('source_samples'),row.get('readable_samples')]+[m['file'] for m in row.get('mips',[]) or []]:
    if p and Path(p).is_file():artifact_pins[p]=sha(p)
  state.update(status='isolated_townsmith_compressed_texture_capture_finished_originals_unchanged',originals_unchanged=True,native_status=native['status'],native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),artifact_pins=artifact_pins,complete_assets_delivered_increment=0)
 except Exception as e:state.update(status='isolated_townsmith_compressed_texture_extraction_refusal_retained',error=repr(e))
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if len(sys.argv)==1 else run()
