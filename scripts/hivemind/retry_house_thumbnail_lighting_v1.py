"""Retain saturated images and recapture unchanged source assets with unit lighting."""
from pathlib import Path
from datetime import datetime,timezone
import importlib.util,json,shutil,subprocess,sys,types
W=Path('F:/OpenRPG_ImportWork');OLD=W/'unity-library-source-thumbnail-house-bench-v3';ROOT=W/'unity-library-source-thumbnail-house-bench-v4';PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');LIB=Path(__file__).with_name('queue_source_library_thumbnail_v1.py')
capture=types.ModuleType('capture_with_unit_lighting');capture.__file__=str(LIB)
code=LIB.read_text('utf-8');assert code.count("'OpenRPGSourceLibraryThumbnailV1.Export'")==1
exec(compile(code.replace("'OpenRPGSourceLibraryThumbnailV1.Export'","'OpenRPGSourceLibraryThumbnailV2.Export'"),str(LIB),'exec'),capture.__dict__)
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def no_unity():assert subprocess.check_output([PS,'-NoProfile','-Command',"@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count"],text=True).strip()=='0','Another Unity is live'
def prepare():
    assert not ROOT.exists();no_unity();old=capture.read(OLD/'RUN-RESULT.v1.json');assert old['source_inputs_unchanged'] and old['actual_exit_code']==0
    ROOT.mkdir();native=capture.read(OLD/'NATIVE-RESULT.v1.json')
    review=dict(status='source_thumbnail_candidates_rejected_by_agent_visual_review',reviewed_utc=datetime.now(timezone.utc).isoformat(),agent_review=True,human_review=False,reason='Both bench views show an all-white clipped surface; wood colour and texture are not discernible.',images=[dict(path=r['path'],sha256=capture.sha(r['path'])) for r in native['images']],native_receipt_sha256=capture.sha(OLD/'NATIVE-RESULT.v1.json'),images_installed_in_vault=False,conversion_qualified=False,full_import=False)
    review_path=OLD/'VISUAL-REVIEW.v1.json';assert not review_path.exists();review_path.write_text(json.dumps(review,indent=2),'utf-8')
    helper=R/'scripts/hivemind/candidates/SourceLibraryThumbnailV2.cs';assert not helper.exists()
    original=R/'scripts/hivemind/candidates/SourceLibraryThumbnailV1.cs';text=original.read_text('utf-8');assert text.count('light.intensity=10000;')==1
    helper.write_text(text.replace('OpenRPGSourceLibraryThumbnailV1','OpenRPGSourceLibraryThumbnailV2').replace('light.intensity=10000;','light.intensity=1;'),'utf-8')
    dst=PROJECT/'Assets/Editor/SourceLibraryThumbnailV2.cs';assert not dst.exists();shutil.copyfile(helper,dst)
    prep=capture.read(OLD/'PREPARED.v1.json');prep['input_pins'].update({str(p):capture.sha(p) for p in [Path(__file__).resolve(),LIB,original,helper,review_path,OLD/'NATIVE-RESULT.v1.json']})
    prep.update(lighting_change=dict(previous_directional_intensity=10000,current_directional_intensity=1,source_materials_changed=False,exposure_disabled=True),reused_project=str(PROJECT),original_project_version_qualification=False)
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8')
    request=capture.read(OLD/'REQUEST.v1.json');request['output']=str(ROOT/'NATIVE-RESULT.v1.json');(ROOT/'REQUEST.v1.json').write_text(json.dumps(request,indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()),indent=2),'utf-8');print(json.dumps(dict(pid=proc.pid,prior_images_rejected=2,source_materials_changed=False)))
def run():
    capture.ROOT=ROOT;capture.PROJECT=PROJECT
    try:no_unity()
    except Exception as exc:
        capture.save(dict(status='source_thumbnail_capture_refusal_retained',error=repr(exc),unity_launched=False,images_installed_in_vault=False,conversion_qualified=False,forge_editor_qualified=False));return
    capture.run()
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
