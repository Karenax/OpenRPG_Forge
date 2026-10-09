"""Capture one missing source thumbnail in a private data-only Unity project on F."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,shutil,subprocess,sys

R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'unity-library-source-thumbnail-house-bench-v2';PROJECT=ROOT/'project'
TEMPLATE=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
SOURCE=W/'hivemind-run/house-on-a-hill';UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
PROBE=R/'scripts/hivemind/candidates/SourceLibraryThumbnailV1.cs'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v['updated_utc']=datetime.now(timezone.utc).isoformat();p=ROOT/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(ROOT/'RUN-RESULT.v1.json')
def prepare():
    assert UNITY.is_file() and not ROOT.exists();ROOT.mkdir();PROJECT.mkdir()
    manifest=SOURCE/'source.v1.json';m=read(manifest)
    assert m['archive_sha256']=='d476a7102427cb5ae7bc8ab8c828c4dbc9815ba94650d9b3b829205ca5f02958'
    pins={str(manifest):sha(manifest),str(PROBE):sha(PROBE),str(Path(__file__).resolve()):sha(__file__)};copied=[];skipped=[]
    # No purchased scripts, assemblies, nested packages or startup scenes execute.
    forbidden={'.cs','.dll','.exe','.asmdef','.asmref','.unitypackage','.unity','.js','.boo'}
    for row in m['files']:
        rel=Path(row['stored_path']);assert not rel.is_absolute() and '..' not in rel.parts
        path=SOURCE/rel;assert sha(path)==row['sha256'],str(path);pins[str(path)]=row['sha256']
        if row['kind']=='preview.png':
            assert rel.parts[0]=='previews';skipped.append(str(rel));continue
        assert rel.parts[0]=='source' and row['kind'] in ['asset','asset.meta']
        suffix_path=rel.with_suffix('') if rel.suffix.lower()=='.meta' else rel
        if suffix_path.suffix.lower() in forbidden or not rel.as_posix().startswith('source/Assets/Hivemind/HouseOnaHill'):
            skipped.append(str(rel));continue
        dest=PROJECT/Path(*rel.parts[1:]);assert dest.resolve().is_relative_to(PROJECT.resolve());dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,dest);assert sha(dest)==row['sha256'];copied.append(dict(source=str(path),target=str(dest),sha256=row['sha256']))
    for folder in ['ProjectSettings','Packages']:
        for p in (TEMPLATE/folder).rglob('*'):
            if p.is_file():
                dest=PROJECT/folder/p.relative_to(TEMPLATE/folder);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest);pins[str(p)]=sha(p)
    for p in (TEMPLATE/'Assets').rglob('*'):
        if p.is_file() and (p.relative_to(TEMPLATE/'Assets').parts[0]=='HDRPDefaultResources' or p.name in ['HDRPDefaultResources.meta','DefaultVolumeProfile.asset','DefaultVolumeProfile.asset.meta','UniversalRenderPipelineGlobalSettings.asset','UniversalRenderPipelineGlobalSettings.asset.meta']):
            dest=PROJECT/'Assets'/p.relative_to(TEMPLATE/'Assets');dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest);pins[str(p)]=sha(p)
    dest=PROJECT/'Assets/Editor/SourceLibraryThumbnailV1.cs';dest.parent.mkdir(parents=True);shutil.copyfile(PROBE,dest)
    prefab='Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Bench_01.prefab'
    guid=next(g for g,p in m['guid_paths'].items() if p==prefab)
    request=dict(id='hivemind.house-on-a-hill.prefab-sm-bench-01-'+guid,prefab_path=prefab,prefab_guid=guid,output=str(ROOT/'NATIVE-RESULT.v1.json'))
    (ROOT/'REQUEST.v1.json').write_text(json.dumps(request,indent=2),'utf-8')
    proof=dict(input_pins=pins,copied_data=copied,excluded_files=skipped,source_assets_executed=False,private_sdk_version='17.5.0',unity_editor='6000.5.8f1',original_project_version_qualification=False,conversion_qualified=False,images_installed_in_vault=False)
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(proof,indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(pid=child.pid,created_utc=datetime.now(timezone.utc).isoformat()),indent=2),'utf-8')
    print(json.dumps(dict(pid=child.pid,copied_files=len(copied),source_files_checked=len(m['files']),output=str(ROOT))))
def run():
    state=dict(status='starting_private_source_thumbnail_capture',conversion_qualified=False,forge_editor_qualified=False,images_installed_in_vault=False);save(state)
    try:
        prep=read(ROOT/'PREPARED.v1.json')
        for p,digest in prep['input_pins'].items():assert sha(p)==digest,p
        env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-upm-cache'))
        args=[str(UNITY),'-batchmode','-quit','-job-worker-count','2','-projectPath',str(PROJECT),'-executeMethod','OpenRPGSourceLibraryThumbnailV1.Export','--openrpg-manifest',str(ROOT/'REQUEST.v1.json'),'-logFile',str(ROOT/'Unity.log')]
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            child=subprocess.Popen(args,cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='private_unity_source_thumbnail_capture_running',unity_pid=child.pid);save(state);state['actual_exit_code']=child.wait();assert child.returncode==0,'Unity capture failed'
        native=read(ROOT/'NATIVE-RESULT.v1.json');assert native['source_object_unchanged'] and native['preview_scene_isolated'] and native['gpu']!='Null' and len(native['images'])==2
        for row in native['images']:assert sha(row['path'])==row['sha256'] and (row['width'],row['height'])==(512,512)
        for p,digest in prep['input_pins'].items():assert sha(p)==digest,p
        changed=[dict(target=row['target'],original_sha256=row['sha256'],current_sha256=sha(row['target'])) for row in prep['copied_data'] if sha(row['target'])!=row['sha256']]
        state.update(status='two_native_source_thumbnail_candidates_captured_visual_review_pending',images_captured=2,private_importer_changed_files=changed,source_inputs_unchanged=True,native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),visual_review_qualified=False)
    except Exception as exc:state.update(status='source_thumbnail_capture_refusal_retained',error=repr(exc))
    finally:save(state)
if __name__=='__main__':
    if sys.argv[-1]=='prepare':
        try:prepare()
        except Exception as exc:
            if ROOT.exists() and not (ROOT/'RUN-RESULT.v1.json').exists():save(dict(status='source_thumbnail_preparation_refusal_retained',error=repr(exc),unity_launched=False,vault_modified=False))
            raise
    else:run()
