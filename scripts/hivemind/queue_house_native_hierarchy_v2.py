"""Capture all source prefab hierarchies after the live private thumbnail job."""
from pathlib import Path
from datetime import datetime,timezone
import ctypes,ctypes.wintypes as wt,hashlib,json,os,shutil,subprocess,sys

R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'house-native-prefab-hierarchy-v2';PRIOR=W/'unity-library-source-thumbnail-house-bench-v3'
SOURCE=W/'hivemind-run/house-on-a-hill';PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project';UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
HELPER=W/'hivemind-prefab-instance-source-closure-v1/PrefabHierarchySnapshot.cs'
API=HELPER.parent/'API-COMPILE.v3.json'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v['updated_utc']=datetime.now(timezone.utc).isoformat();p=ROOT/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(ROOT/'RUN-RESULT.v1.json')
def process_api():
    k=ctypes.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[wt.DWORD,wt.BOOL,wt.DWORD];k.OpenProcess.restype=wt.HANDLE;k.GetProcessTimes.argtypes=[wt.HANDLE,ctypes.POINTER(wt.FILETIME),ctypes.POINTER(wt.FILETIME),ctypes.POINTER(wt.FILETIME),ctypes.POINTER(wt.FILETIME)];k.WaitForSingleObject.argtypes=[wt.HANDLE,wt.DWORD];k.WaitForSingleObject.restype=wt.DWORD;k.CloseHandle.argtypes=[wt.HANDLE];return k
def identity(k,h):
    times=[wt.FILETIME() for _ in range(4)];assert k.GetProcessTimes(h,*[ctypes.byref(t) for t in times]);return (times[0].dwHighDateTime<<32)|times[0].dwLowDateTime
def prepare():
    assert not ROOT.exists();assert read(API)['helper_sha256']==sha(HELPER) and read(API)['actual_compiler_exit_code']==0
    m=read(SOURCE/'source.v1.json');files={r['stored_path']:r for r in m['files']};cases=[];pins={str(p):sha(p) for p in [HELPER,API,SOURCE/'source.v1.json',Path(__file__).resolve()]}
    for guid,path in sorted(m['guid_paths'].items()):
        if not path.endswith('.prefab') or '/HouseOnaHill/' not in path:continue
        assert not Path(path).is_absolute() and '..' not in Path(path).parts
        for rel in ['source/'+path,'source/'+path+'.meta']:
            p=SOURCE/rel;assert sha(p)==files[rel]['sha256'];pins[str(p)]=files[rel]['sha256']
        cases.append(dict(asset_id='hivemind.house-on-a-hill.source-prefab-'+guid,asset_path=path,expected_guid=guid,source_sha256=files['source/'+path]['sha256']))
    assert len(cases)==50;pid=read(PRIOR/'JOB.v1.json')['pid'];k=process_api();h=k.OpenProcess(0x101000,False,pid);assert h,'Existing thumbnail controller must be observed alive'
    try:birth=identity(k,h)
    finally:k.CloseHandle(h)
    ROOT.mkdir();request=ROOT/'REQUEST.v1.json';request.write_text(json.dumps(dict(schema_version=1,cases=cases),indent=2),'utf-8');pins[str(request)]=sha(request)
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,expected_prefab_roots=50,wait_pid=pid,wait_creation_filetime=birth,private_project=str(PROJECT),purchased_scripts_executed=False,geometry_converted=False,studio_verified=False,full_import=False),indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()),indent=2),'utf-8');print(json.dumps(dict(pid=proc.pid,expected_prefab_roots=50,wait_pid=pid)))
def run():
    state=dict(status='waiting_for_live_private_thumbnail_controller',captured_prefab_roots=0,geometry_converted=False,gpu_rendered=False,studio_verified=False,full_import=False);save(state)
    try:
        prep=read(ROOT/'PREPARED.v1.json');k=process_api();h=k.OpenProcess(0x101000,False,prep['wait_pid'])
        if h:
            try:
                if identity(k,h)==prep['wait_creation_filetime']:assert k.WaitForSingleObject(h,3*60*60*1000)==0,'Existing capture did not terminate'
            finally:k.CloseHandle(h)
        prior=read(PRIOR/'RUN-RESULT.v1.json');assert prior['status']=='two_native_source_thumbnail_candidates_captured_visual_review_pending',prior['status']
        for p,digest in prep['input_pins'].items():assert sha(p)==digest,p
        ps=Path('C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe')
        editors=json.loads(subprocess.check_output([str(ps),'-NoProfile','-Command',"@(Get-Process Unity -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id) | ConvertTo-Json -AsArray -Compress"],text=True).strip() or '[]')
        assert not editors,'Another Unity editor is active; preserve it and defer this capture'
        # Install only the already API-checked, trusted snapshot helper, after Unity closes.
        dst=PROJECT/'Assets/Editor/PrefabHierarchySnapshot.cs';assert not dst.exists();shutil.copyfile(HELPER,dst);assert sha(dst)==sha(HELPER)
        env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-upm-cache'))
        args=[str(UNITY),'-batchmode','-quit','-job-worker-count','2','-projectPath',str(PROJECT),'-executeMethod','OpenRPGPrefabHierarchyProbe.PrefabHierarchySnapshot.Export','--openrpg-manifest',str(ROOT/'REQUEST.v1.json'),'--openrpg-hierarchy-output',str(ROOT/'NATIVE-RESULT.v1.json'),'-logFile',str(ROOT/'Unity.log')]
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            proc=subprocess.Popen(args,cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='capturing_all_house_source_prefab_hierarchies',unity_pid=proc.pid);save(state);state['actual_exit_code']=proc.wait();assert proc.returncode==0,'Hierarchy capture failed'
        capture=read(ROOT/'NATIVE-RESULT.v1.json');requests=read(ROOT/'REQUEST.v1.json')['cases'];assert capture['total']==capture['captured']==50 and capture['failed']==0 and len(capture['cases'])==50
        actual={c['expected_guid']:c for c in capture['cases']};assert len(actual)==50 and set(actual)=={r['expected_guid'] for r in requests}
        for p,digest in prep['input_pins'].items():assert sha(p)==digest,p
        for row in requests:assert actual[row['expected_guid']]['root']['guid']==row['expected_guid'] and not actual[row['expected_guid']].get('error')
        state.update(status='fifty_house_native_prefab_hierarchies_captured_independent_reopen_pending',captured_prefab_roots=50,source_inputs_unchanged=True,native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),nodes=sum(len(c['nodes']) for c in capture['cases']),renderers=sum(c['renderer_count'] for c in capture['cases']),missing_script_components=sum(c['missing_script_components'] for c in capture['cases']))
    except Exception as exc:state.update(status='house_native_hierarchy_capture_refusal_retained',error=repr(exc))
    finally:save(state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
