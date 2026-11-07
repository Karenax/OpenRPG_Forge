"""Capture the next eight unattempted Pirate cards against a frozen current epoch."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,os,shutil,subprocess,sys,traceback
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault')
O=W/'pirate-source-thumbnail-batch-0145-0152-v296';PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False,images_installed_in_vault=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    assert not O.exists()
    last=W/'pirate-p10-thumbnail-delivery-v294/RESULT.v1.json';proof=read(last)
    assert proof['cards_delivered']==8 and proof['current_native_reader_verified']
    pointer=read(V/'metadata/asset-source-support-registry.v1.json');catalogpath=V/pointer['path']
    assert sha(catalogpath)==pointer['sha256'];catalog=read(catalogpath)
    previous=set();priorfiles=[]
    for name in ['0001-0024-v2','0025-0048-v1','0049-0072-v1','0073-0096-v1','0097-0104-v1','0105-0112-v1','0113-0120-v163']:
        p=W/('pirate-source-thumbnail-batch-'+name)/'PREPARED.v1.json';previous.update(c['id'] for c in read(p)['cases']);priorfiles.append(p)
    previous_capture=W/'pirate-source-thumbnail-batch-0121-0128-v201/PREPARED.v1.json';previous.update(c['id'] for c in read(previous_capture)['cases']);priorfiles.append(previous_capture)
    previous_capture=W/'pirate-source-thumbnail-batch-0129-0136-v277/PREPARED.v1.json';previous.update(c['id'] for c in read(previous_capture)['cases']);priorfiles.append(previous_capture)
    previous_capture=W/'pirate-source-thumbnail-batch-0137-0144-v286/PREPARED.v1.json';previous.update(c['id'] for c in read(previous_capture)['cases']);priorfiles.append(previous_capture)
    eligible=sorted([a for a in catalog['assets'] if a.get('pack_id')=='hivemind.pirate-island' and not a.get('preview') and a['id'] not in previous and a.get('original_lod_level')==0 and '/HDRP' in a.get('source_asset_path','') and a['source_asset_path'].endswith('.prefab')],key=lambda a:a['id'])
    selected=eligible[:8];assert len(selected)==8
    base=S/'candidates/SourceLibraryThumbnailBatchV1.cs';installed=PROJECT/'Assets/Editor'/base.name
    assert sha(base)==sha(installed)
    O.mkdir();(O/'temp').mkdir();(O/'private-input-epoch').mkdir()
    pins={str(p):sha(p) for p in (PROJECT/'Assets/Hivemind/PirateIsland').rglob('*') if p.is_file()}
    snapshots=[]
    for name,h in list(pins.items()):
        p=Path(name)
        if p.suffix in {'.mat','.meta','.shadergraph','.asset'}:
            copy=O/'private-input-epoch'/p.relative_to(PROJECT);copy.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,copy);assert sha(copy)==h
            snapshots.append(dict(live_path=name,snapshot=str(copy),sha256=h));pins[str(copy)]=h
    cases=[]
    for i,a in enumerate(selected):
        for field,hfield in [('source_file','source_sha256'),('source_meta_file','source_meta_sha256')]:
            p=V/a[field];assert sha(p)==a[hfield];pins[str(p)]=a[hfield]
        assert (PROJECT/a['source_asset_path']).is_file()
        folder=O/f'case-{i:02d}';folder.mkdir();cases.append(dict(id=a['id'],prefab_path=a['source_asset_path'],prefab_guid=a['source_guid'],output=str(folder/'NATIVE-RESULT.v1.json')))
    (O/'REQUEST.v1.json').write_text(json.dumps(dict(cases=cases,output=str(O/'NATIVE-RESULT.v1.json')),indent=2),'utf-8')
    (O/'PRIVATE-EPOCH.v1.json').write_text(json.dumps(dict(private_input_snapshots=snapshots),indent=2),'utf-8')
    for p in [Path(__file__),base,installed,last,catalogpath,O/'REQUEST.v1.json',O/'PRIVATE-EPOCH.v1.json',S/'queue_delta_firepit_particle_behavior_v4.py',PROJECT/'Packages/manifest.json',PROJECT/'ProjectSettings/ProjectSettings.asset',*priorfiles]:pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(sub_batch='L08.IMAGE.PIRATE.11',expected_cards=8,expected_images=16,cases=cases,input_pins=pins,original_lod_level=0,catalog_revision=pointer['revision'],source_version_parity_qualified=False,full_import=False),indent=2),'utf-8')
    (O/'SELECTION.v1.json').write_text(json.dumps(dict(eligible_remaining_before_selection=len(eligible),selected_cards=selected),indent=2),'utf-8')
    save(dict(status='eight_new_pirate_source_cards_prepared'))
    with (O/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8')
    print(json.dumps(dict(controller_pid=proc.pid,cards=8,images=16,eligible=len(eligible),names=[Path(a['source_asset_path']).stem for a in selected])))
def run():
    state=read(O/'RUN-RESULT.v1.json');broker=None;namespace=None
    try:
        assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0','Preserve live Unity'
        pins=read(O/'PREPARED.v1.json')['input_pins']
        for p,h in pins.items():assert sha(p)==h,p
        recipe=S/'queue_delta_firepit_particle_behavior_v4.py';tree=ast.parse(recipe.read_text('utf-8-sig'))
        selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        namespace=dict(ROOT=O,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[])
        exec(compile(ast.Module(body=selected,type_ignores=[]),str(recipe),'exec'),namespace);broker,label=namespace['start_broker'](state)
        env=os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (O/'unity.stdout.log').open('xb') as out,(O/'unity.stderr.log').open('xb') as err:
            proc=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-quit','-force-d3d11','-job-worker-count','2','-upmIpcPath',label,'-projectPath',str(PROJECT),'-executeMethod','OpenRPGSourceLibraryThumbnailBatchV1.Export','--openrpg-manifest',str(O/'REQUEST.v1.json'),'-logFile',str(O/'unity.log')],env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='eight_pirate_source_cards_native_capture_running',unity_pid=proc.pid);save(state);state['unity_exit_code']=proc.wait()
        state['input_changes']=[dict(path=p,before=h,after=sha(p)) for p,h in pins.items() if sha(p)!=h]
        assert not state['input_changes'],'Retain private importer mutations for classification'
        assert state['unity_exit_code']==0
        batch=read(O/'NATIVE-RESULT.v1.json');assert len(batch['cases'])==8
        state.update(status='eight_pirate_source_cards_captured_review_pending',cards_captured=sum(c['captured'] for c in batch['cases']),refusals=[c for c in batch['cases'] if not c['captured']],native_receipt_sha256=sha(O/'NATIVE-RESULT.v1.json'),source_inputs_unchanged=True)
    except Exception as exc:state.update(status='pirate_batch_capture_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
    finally:
        if broker is not None:namespace['stop_broker'](broker,state)
        state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
