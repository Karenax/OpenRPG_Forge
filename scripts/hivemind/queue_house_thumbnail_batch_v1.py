"""Capture eight explicitly inventoried source cards, retaining each native refusal."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, shutil, subprocess, sys

R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust'); W=Path('F:/OpenRPG_ImportWork'); V=Path('F:/OpenRPG_AssetVault')
ROOT=W/'house-source-thumbnail-batch-0001-0008-v1'
PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
BASE=R/'scripts/hivemind/candidates/SourceLibraryThumbnailV6.cs'
HELPER=BASE.with_name('SourceLibraryThumbnailBatchV1.cs')
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
def write(p,v): Path(p).write_text(json.dumps(v,indent=2,ensure_ascii=False,allow_nan=False),'utf-8')
def save(v):
    v['updated_utc']=datetime.now(timezone.utc).isoformat(); p=ROOT/'RUN-RESULT.tmp';write(p,v);p.replace(ROOT/'RUN-RESULT.v1.json')
def no_unity():
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
def prepare():
    no_unity();assert not ROOT.exists() and not HELPER.exists();ROOT.mkdir()
    registry=read(V/'metadata/asset-source-support-registry.v1.json');catalog_path=V/registry['path'];assert sha(catalog_path)==registry['sha256'];catalog=read(catalog_path)
    selected=sorted([a for a in catalog['assets'] if a.get('pack_id')=='hivemind.house-on-a-hill' and not a.get('preview')],key=lambda a:a['id'])[:8];assert len(selected)==8
    text=BASE.read_text('utf-8').replace('OpenRPGSourceLibraryThumbnailV6','OpenRPGSourceLibraryThumbnailBatchV1')
    before='public static void Export() {\n  var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"--openrpg-manifest");if(arg<0)throw new Exception("Manifest absent");\n  var request=JsonUtility.FromJson<Request>(File.ReadAllText(args[arg+1]));if(File.Exists(request.output))throw new Exception("Preserve previous capture");'
    assert text.count(before)==1
    wrapper='''[Serializable] public class BatchRequest { public Request[] cases; public string output; }
 [Serializable] public class Outcome { public string id,receipt,error; public bool captured; }
 [Serializable] public class BatchReport { public Outcome[] cases; public bool full_import=false; }
 public static void Export() {
  var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"--openrpg-manifest");if(arg<0)throw new Exception("Manifest absent");
  var batch=JsonUtility.FromJson<BatchRequest>(File.ReadAllText(args[arg+1]));if(File.Exists(batch.output))throw new Exception("Preserve previous batch");
  var outcomes=new System.Collections.Generic.List<Outcome>();
  foreach(var request in batch.cases) {
   bool priorAsync=ShaderUtil.allowAsyncCompilation;
   try { Capture(request);outcomes.Add(new Outcome{id=request.id,receipt=request.output,captured=true}); }
   catch(Exception exc) { outcomes.Add(new Outcome{id=request.id,receipt=request.output,error=exc.ToString(),captured=false}); }
   finally { ShaderUtil.allowAsyncCompilation=priorAsync; }
   File.WriteAllText(batch.output+".progress",JsonUtility.ToJson(new BatchReport{cases=outcomes.ToArray()},true));
  }
  File.WriteAllText(batch.output,JsonUtility.ToJson(new BatchReport{cases=outcomes.ToArray()},true));
 }
 static void Capture(Request request) {
  if(File.Exists(request.output))throw new Exception("Preserve previous capture");'''
    text=text.replace(before,wrapper);HELPER.write_text(text,'utf-8');dst=PROJECT/'Assets/Editor'/HELPER.name;assert not dst.exists();shutil.copyfile(HELPER,dst)
    pins=read(W/'unity-library-source-thumbnail-house-bench-v8/PREPARED.v1.json')['input_pins']
    pins.update({str(p):sha(p) for p in [BASE,HELPER,dst,Path(__file__).resolve(),catalog_path]})
    cases=[]
    for i,a in enumerate(selected):
        assert a['source_archive_sha256']=='d476a7102427cb5ae7bc8ab8c828c4dbc9815ba94650d9b3b829205ca5f02958'
        assert '/HDRP/' in a['source_asset_path'];folder=ROOT/f'case-{i:02d}';folder.mkdir()
        for field,digest in [('source_file','source_sha256'),('source_meta_file','source_meta_sha256')]:
            p=V/a[field];assert sha(p)==a[digest];pins[str(p)]=a[digest]
        cases.append(dict(id=a['id'],prefab_path=a['source_asset_path'],prefab_guid=a['source_guid'],output=str(folder/'NATIVE-RESULT.v1.json')))
    request=ROOT/'REQUEST.v1.json';write(request,dict(cases=cases,output=str(ROOT/'NATIVE-RESULT.v1.json')));pins[str(request)]=sha(request)
    write(ROOT/'PREPARED.v1.json',dict(sub_batch='L08.IMAGE.HOUSE.02',expected_cards=8,expected_images=16,cases=cases,input_pins=pins,catalog_revision=registry['revision'],original_version_parity=False,presentation_only=True,source_assets_executed=False,full_import=False))
    write(ROOT/'LOT.md.json',dict(lot='L08.IMAGE.HOUSE.02',ids=[a['id'] for a in selected],phases=['capture native originale','SHA et PNG','revue de chaque image','indexation transactionnelle','recherche et lecture native'],images_expected=16,output=str(ROOT),vault=str(V)))
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    write(ROOT/'JOB.v1.json',dict(pid=child.pid,created_utc=datetime.now(timezone.utc).isoformat()));print(json.dumps(dict(pid=child.pid,cards=8,output=str(ROOT))))
def run():
    state=dict(status='house_eight_card_native_thumbnail_batch_starting',full_import=False,images_installed_in_vault=False);save(state)
    try:
        no_unity();prep=read(ROOT/'PREPARED.v1.json')
        for p,d in prep['input_pins'].items():assert sha(p)==d,p
        env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-upm-cache'))
        args=[str(UNITY),'-batchmode','-quit','-job-worker-count','2','-projectPath',str(PROJECT),'-executeMethod','OpenRPGSourceLibraryThumbnailBatchV1.Export','--openrpg-manifest',str(ROOT/'REQUEST.v1.json'),'-logFile',str(ROOT/'Unity.log')]
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            child=subprocess.Popen(args,cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='house_eight_card_native_thumbnail_batch_running',unity_pid=child.pid);save(state);state['actual_exit_code']=child.wait();assert child.returncode==0
        batch=read(ROOT/'NATIVE-RESULT.v1.json');assert [c['id'] for c in batch['cases']]==[c['id'] for c in prep['cases']]
        captured=[];refused=[]
        for case in batch['cases']:
            if not case['captured']:refused.append(case);continue
            native=read(case['receipt']);assert native['id']==case['id'] and native['source_object_unchanged'] and native['preview_scene_isolated'] and native['gpu']!='Null';assert len(native['images'])==2
            for image in native['images']:assert sha(image['path'])==image['sha256']
            captured.append(case)
        for p,d in prep['input_pins'].items():assert sha(p)==d,p
        state.update(status='house_native_thumbnail_batch_captured_review_pending',cards_captured=len(captured),images_captured=2*len(captured),cards_refused=len(refused),refusals=refused,source_inputs_unchanged=True,native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),visual_review_qualified=False)
    except Exception as exc:state.update(status='house_thumbnail_batch_refusal_retained',error=repr(exc))
    finally:save(state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
