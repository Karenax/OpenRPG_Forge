"""Apply SDK-native presentation exposure to unchanged source materials."""
from pathlib import Path
from datetime import datetime, timezone
import json, shutil, subprocess, sys, types

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'unity-library-source-thumbnail-house-bench-v8'
OLD=W/'unity-library-source-thumbnail-house-bench-v7'
PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
LIB=Path(__file__).with_name('queue_source_library_thumbnail_v1.py')
capture=types.ModuleType('native_exposure_original_capture');capture.__file__=str(LIB)
code=LIB.read_text('utf-8');assert code.count("'OpenRPGSourceLibraryThumbnailV1.Export'")==1
exec(compile(code.replace("'OpenRPGSourceLibraryThumbnailV1.Export'","'OpenRPGSourceLibraryThumbnailV6.Export'"),str(LIB),'exec'),capture.__dict__)
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'

def no_unity():
    assert subprocess.check_output([PS,'-NoProfile','-Command',"@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count"],text=True).strip()=='0','Another Unity is live'

def replace_once(text, before, after):
    assert text.count(before)==1, before
    return text.replace(before,after)

def prepare():
    assert not ROOT.exists();no_unity()
    assert capture.read(OLD/'RUN-RESULT.v1.json')['source_inputs_unchanged']
    ROOT.mkdir();native=capture.read(OLD/'NATIVE-RESULT.v1.json')
    review=OLD/'VISUAL-REVIEW.v1.json';assert not review.exists()
    review.write_text(json.dumps(dict(status='source_thumbnail_candidates_rejected_by_agent_visual_review',reviewed_utc=datetime.now(timezone.utc).isoformat(),reason='Both native views clip source HDR values when encoded as PNG; reported raw maxima 200 and 168 exceed the display range. Perspective and synchronous source passes did not repair the capture.',agent_review=True,human_review=False,images=native['images'],native_receipt_sha256=capture.sha(OLD/'NATIVE-RESULT.v1.json'),images_installed_in_vault=False,full_import=False),indent=2),'utf-8')
    source=Path(__file__).parent/'candidates/SourceLibraryThumbnailV5.cs'
    helper=source.with_name('SourceLibraryThumbnailV6.cs');assert not helper.exists()
    text=source.read_text('utf-8').replace('OpenRPGSourceLibraryThumbnailV5','OpenRPGSourceLibraryThumbnailV6')
    text=replace_once(text,'public float actual_directional_light_intensity;','public float actual_directional_light_intensity;public float presentation_fixed_ev=8;public string presentation_tonemapping="Neutral";public bool presentation_only=true;public int warmup_render_requests_per_view=3;')
    text=replace_once(text,'lightObject=null;RenderTexture target=null;','lightObject=null,volumeObject=null;VolumeProfile presentationProfile=null;RenderTexture target=null;')
    text=replace_once(text,'extra.renderingPathCustomFrameSettings.SetEnabled(field,false);','extra.renderingPathCustomFrameSettings.SetEnabled(field,true);')
    needle='lightObject=new GameObject("Thumbnail directional light");'
    text=replace_once(text,needle,'extra.volumeLayerMask=1<<31;extra.volumeAnchorOverride=camera.transform;extra.hasPersistentHistory=true;\n   volumeObject=new GameObject("Native thumbnail exposure");volumeObject.hideFlags=HideFlags.HideAndDontSave;volumeObject.layer=31;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(volumeObject,preview);\n   var volume=volumeObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=float.MaxValue;volume.weight=1;presentationProfile=ScriptableObject.CreateInstance<VolumeProfile>();presentationProfile.hideFlags=HideFlags.HideAndDontSave;volume.sharedProfile=presentationProfile;\n   var exposure=presentationProfile.Add<Exposure>(true);exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(8);exposure.compensation.Override(0);\n   var tone=presentationProfile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.Neutral);\n   '+needle)
    text=replace_once(text,'RenderPipeline.SubmitRenderRequest(camera,render);','for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,render);')
    text=replace_once(text,'if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);','if(volumeObject!=null)UnityEngine.Object.DestroyImmediate(volumeObject);if(presentationProfile!=null)UnityEngine.Object.DestroyImmediate(presentationProfile);if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);')
    helper.write_text(text,'utf-8');dst=PROJECT/'Assets/Editor/SourceLibraryThumbnailV6.cs';assert not dst.exists();shutil.copyfile(helper,dst)
    sdk=PROJECT/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime'
    sdk_files=[sdk/'PostProcessing/Components/Exposure.cs',sdk/'PostProcessing/Components/Tonemapping.cs',sdk/'RenderPipeline/HDRenderPipeline.LookDev.cs']
    prep=capture.read(OLD/'PREPARED.v1.json')
    prep['input_pins'].update({str(p):capture.sha(p) for p in [Path(__file__).resolve(),LIB,source,helper,review,OLD/'NATIVE-RESULT.v1.json',*sdk_files]})
    prep.update(presentation_fixed_ev=8,presentation_tonemapping='Neutral',presentation_only=True,source_shader_substituted=False,source_materials_changed=False,reused_project=str(PROJECT),warmup_render_requests_per_view=3,cpu_image_relighting=False)
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8')
    request=capture.read(OLD/'REQUEST.v1.json');request['output']=str(ROOT/'NATIVE-RESULT.v1.json');(ROOT/'REQUEST.v1.json').write_text(json.dumps(request,indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()),indent=2),'utf-8')
    print(json.dumps(dict(pid=proc.pid,presentation_fixed_ev=8,source_materials_changed=False)))

def run():
    capture.ROOT=ROOT;capture.PROJECT=PROJECT
    try:no_unity()
    except Exception as exc:
        capture.save(dict(status='source_thumbnail_capture_refusal_retained',error=repr(exc),unity_launched=False,conversion_qualified=False,images_installed_in_vault=False));return
    capture.run()

if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
