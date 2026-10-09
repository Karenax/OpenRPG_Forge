"""Measure unclipped native pixels with an explicit perspective presentation camera."""
from pathlib import Path
from datetime import datetime,timezone
import json,shutil,subprocess,sys,types
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'unity-library-source-thumbnail-house-bench-v7';OLD=W/'unity-library-source-thumbnail-house-bench-v6';PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
LIB=Path(__file__).with_name('queue_source_library_thumbnail_v1.py');capture=types.ModuleType('perspective_original_capture');capture.__file__=str(LIB)
code=LIB.read_text('utf-8');assert code.count("'OpenRPGSourceLibraryThumbnailV1.Export'")==1
exec(compile(code.replace("'OpenRPGSourceLibraryThumbnailV1.Export'","'OpenRPGSourceLibraryThumbnailV5.Export'"),str(LIB),'exec'),capture.__dict__)
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def no_unity():assert subprocess.check_output([PS,'-NoProfile','-Command',"@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count"],text=True).strip()=='0','Another Unity is live'
def prepare():
    assert not ROOT.exists();no_unity();assert capture.read(OLD/'RUN-RESULT.v1.json')['source_inputs_unchanged'];ROOT.mkdir();native=capture.read(OLD/'NATIVE-RESULT.v1.json')
    review=OLD/'VISUAL-REVIEW.v1.json';assert not review.exists();review.write_text(json.dumps(dict(status='source_thumbnail_candidates_rejected_by_agent_visual_review',reviewed_utc=datetime.now(timezone.utc).isoformat(),reason='Both views remain all-white; eight original passes compiled synchronously and seven source texture bindings are present.',agent_review=True,human_review=False,images=native['images'],native_receipt_sha256=capture.sha(OLD/'NATIVE-RESULT.v1.json'),images_installed_in_vault=False,full_import=False),indent=2),'utf-8')
    source=Path(__file__).parent/'candidates/SourceLibraryThumbnailV4.cs';helper=source.with_name('SourceLibraryThumbnailV5.cs');assert not helper.exists();text=source.read_text('utf-8');assert text.count('camera.orthographic=true;')==1
    text=text.replace('OpenRPGSourceLibraryThumbnailV4','OpenRPGSourceLibraryThumbnailV5').replace('camera.orthographic=true;','camera.orthographic=false;camera.fieldOfView=30;')
    text=text.replace('public int view,width=512,height=512;','public int view,width=512,height=512,clipped_rgb_pixels;public float linear_max_channel;')
    text=text.replace('public bool synchronous_source_passes=true;','public bool camera_orthographic=false;public float camera_field_of_view=30;public bool synchronous_source_passes=true;')
    needle='for(int i=0;i<colors.Length;i++){';assert text.count(needle)==1
    text=text.replace(needle,'float linearMax=colors.Max(c=>Math.Max(c.r,Math.Max(c.g,c.b)));int clipped=colors.Count(c=>c.r>=1&&c.g>=1&&c.b>=1);\n     '+needle)
    text=text.replace('view=view,camera_position=','view=view,linear_max_channel=linearMax,clipped_rgb_pixels=clipped,camera_position=');helper.write_text(text,'utf-8')
    dst=PROJECT/'Assets/Editor/SourceLibraryThumbnailV5.cs';assert not dst.exists();shutil.copyfile(helper,dst)
    prep=capture.read(OLD/'PREPARED.v1.json');prep['input_pins'].update({str(p):capture.sha(p) for p in [Path(__file__).resolve(),LIB,source,helper,review,OLD/'NATIVE-RESULT.v1.json']});prep.update(camera_projection='perspective',camera_field_of_view=30,linear_pixel_statistics=True,source_shader_substituted=False,source_materials_changed=False,reused_project=str(PROJECT))
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8');request=capture.read(OLD/'REQUEST.v1.json');request['output']=str(ROOT/'NATIVE-RESULT.v1.json');(ROOT/'REQUEST.v1.json').write_text(json.dumps(request,indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()),indent=2),'utf-8');print(json.dumps(dict(pid=proc.pid,linear_pixel_statistics=True,camera_projection='perspective')))
def run():
    capture.ROOT=ROOT;capture.PROJECT=PROJECT
    try:no_unity()
    except Exception as exc:
        capture.save(dict(status='source_thumbnail_capture_refusal_retained',error=repr(exc),unity_launched=False,conversion_qualified=False,images_installed_in_vault=False));return
    capture.run()
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
