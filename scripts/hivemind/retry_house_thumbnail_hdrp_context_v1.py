"""Repair a missing private HDRP dependency; retain the failed capture and cache."""
from pathlib import Path
from datetime import datetime,timezone
import importlib.util,json,os,shutil,subprocess,sys
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'unity-library-source-thumbnail-house-bench-v3'
OLD=W/'unity-library-source-thumbnail-house-bench-v2';PROJECT=OLD/'project'
PREVIOUS=Path(__file__).with_name('queue_source_library_thumbnail_v1.py')
spec=importlib.util.spec_from_file_location('existing_thumbnail_capture',PREVIOUS);capture=importlib.util.module_from_spec(spec);spec.loader.exec_module(capture)
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def require_no_unity():
    result=subprocess.check_output([PS,'-NoProfile','-Command',"@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count"],text=True)
    assert result.strip()=='0','Preserve another live Unity process'
def prepare():
    assert not ROOT.exists();assert capture.read(OLD/'RUN-RESULT.v1.json')['actual_exit_code']==1
    require_no_unity();ROOT.mkdir()
    prep=capture.read(OLD/'PREPARED.v1.json');pins=dict(prep['input_pins']);copied=list(prep['copied_data'])
    template=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
    repairs=[]
    for rel in ['Assets/OpenRPGProbe/SourcePassHDRPContext.asset','Assets/OpenRPGProbe/SourcePassHDRPContext.asset.meta']:
        src=template/rel;dst=PROJECT/rel;assert src.is_file() and not dst.exists();dst.parent.mkdir(parents=True,exist_ok=True)
        digest=capture.sha(src);shutil.copyfile(src,dst);assert capture.sha(dst)==digest
        pins[str(src)]=digest;copied.append(dict(source=str(src),target=str(dst),sha256=digest));repairs.append(dict(source=str(src),target=str(dst),sha256=digest))
    assert 'f47c5e2aa6e6d3c4d973aa1ff5fa7867' in (PROJECT/'Assets/OpenRPGProbe/SourcePassHDRPContext.asset.meta').read_text('utf-8')
    pins[str(Path(__file__).resolve())]=capture.sha(Path(__file__).resolve());pins[str(PREVIOUS)]=capture.sha(PREVIOUS)
    request=capture.read(OLD/'REQUEST.v1.json');request['output']=str(ROOT/'NATIVE-RESULT.v1.json')
    (ROOT/'REQUEST.v1.json').write_text(json.dumps(request,indent=2),'utf-8')
    prep.update(input_pins=pins,copied_data=copied,repairs=repairs,reused_project=str(PROJECT),prior_refusal=str(OLD/'RUN-RESULT.v1.json'),prior_refusal_sha256=capture.sha(OLD/'RUN-RESULT.v1.json'),prior_log_sha256=capture.sha(OLD/'Unity.log'),original_project_version_qualification=False)
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()),indent=2),'utf-8');print(json.dumps(dict(pid=proc.pid,repair_files=2,reused_project=str(PROJECT))))
def run():
    capture.ROOT=ROOT;capture.PROJECT=PROJECT
    try:require_no_unity()
    except Exception as exc:
        capture.save(dict(status='source_thumbnail_capture_refusal_retained',error=repr(exc),unity_launched=False,conversion_qualified=False,forge_editor_qualified=False,images_installed_in_vault=False));return
    capture.run()
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
