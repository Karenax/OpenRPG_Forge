"""Capture the exact original door with explicit scene context and raw linear output."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, subprocess, sys

S=Path(__file__).parent
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'townsmith-door-source-render-context-v1'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
PRIOR=W/'townsmith-shadow-mode-controls-v1'
CS=S/'candidates/SourceTownsmithRenderContextV1.cs'
HELPER=PROJECT/'Assets/Editor'/CS.name
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,value):
    p=ROOT/name;t=p.with_suffix('.tmp');t.write_text(json.dumps(value,indent=2),encoding='utf-8');t.replace(p)

def prepare():
    assert not ROOT.exists() and not CS.exists() and not HELPER.exists()
    assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    prior=read(PRIOR/'PREPARED.v1.json')
    pins=dict(prior['input_pins'])
    for p,h in pins.items():assert sha(p)==h,p
    private={p:sha(p) for p in prior['private_before_pins']}
    text=(S/'candidates/SourceTownsmithShadowModesV1.cs').read_text(encoding='utf-8')
    text=text.replace('OpenRPGSourceTownsmithShadowModesV1','OpenRPGSourceTownsmithRenderContextV1')
    text=text.replace('request.cases.Length!=15','request.cases.Length!=2').replace('Fifteen shadow-mode controls required','Two fixed neutral/raw-linear reference controls required')
    text=text.replace('tone.mode.Override(TonemappingMode.Neutral);','tone.mode.Override(item.id.EndsWith("-linear")?TonemappingMode.None:TonemappingMode.Neutral);')
    anchor=' [Serializable] public class Result'
    assert text.count(anchor)==1
    context=''' [Serializable] public class Context {
  public string id,anisotropic_filtering,ambient_mode,tonemapping,skybox_identity;
  public Vector3 bounds_center,bounds_size,camera_position; public Quaternion camera_rotation;
  public Matrix4x4 camera_world_to_view,camera_projection; public Color ambient_light,ambient_sky,ambient_equator,ambient_ground,light_color;
  public float radius,fov,near,far,exposure_ev,light_intensity,ambient_intensity; public int msaa_samples,width=512,height=512;
  public bool source_prefab_transform_context=false,source_original_material=true,full_import=false;
 }
'''
    text=text.replace(anchor,context+anchor)
    anchor='    var prior=RenderTexture.active;Texture2D raw=null,png=null;'
    assert text.count(anchor)==1
    context='''    File.WriteAllText(Path.Combine(root,item.id+".context.json"),JsonUtility.ToJson(new Context{
      id=item.id,bounds_center=bounds.center,bounds_size=bounds.size,radius=radius,
      camera_position=camera.transform.position,camera_rotation=camera.transform.rotation,
      camera_world_to_view=camera.worldToCameraMatrix,camera_projection=camera.projectionMatrix,
      fov=camera.fieldOfView,near=camera.nearClipPlane,far=camera.farClipPlane,exposure_ev=8,
      light_intensity=light.intensity,light_color=light.color,
      anisotropic_filtering=QualitySettings.anisotropicFiltering.ToString(),msaa_samples=QualitySettings.antiAliasing,
      ambient_mode=RenderSettings.ambientMode.ToString(),ambient_intensity=RenderSettings.ambientIntensity,
      ambient_light=RenderSettings.ambientLight,ambient_sky=RenderSettings.ambientSkyColor,
      ambient_equator=RenderSettings.ambientEquatorColor,ambient_ground=RenderSettings.ambientGroundColor,
      skybox_identity=RenderSettings.skybox==null?"null":Identity(RenderSettings.skybox),
      tonemapping=item.id.EndsWith("-linear")?"None":"Neutral"},true));
'''
    text=text.replace(anchor,context+anchor)
    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())
    ROOT.mkdir()
    base=read(PRIOR/'REQUEST.v1.json')['cases'][0]
    cases=[dict(base,id='door-neutral'),dict(base,id='door-linear')]
    save('REQUEST.v1.json',dict(output=str(ROOT/'NATIVE-RESULT.v1.json'),cases=cases))
    for p in [Path(__file__),CS,HELPER,ROOT/'REQUEST.v1.json',PRIOR/'RUN-RESULT.v1.json']:
        pins[str(p)]=sha(p)
    save('PREPARED.v1.json',dict(input_pins=pins,private_before_pins=private,expected_cases=2,
        source_objects_preserved=True,purchased_scripts_executed=False,full_import=False))
    save('RUN-RESULT.v1.json',dict(status='source_render_context_prepared',full_import=False))
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,
            stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,root=str(ROOT))))

def run():
    state=read(ROOT/'RUN-RESULT.v1.json')
    try:
        prep=read(ROOT/'PREPARED.v1.json')
        for p,h in prep['input_pins'].items():assert sha(p)==h,p
        env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-upm-cache'),OPENRPG_TOWNSMITH_DRAW_OUT=str(ROOT))
        with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
            child=subprocess.Popen([str(UNITY),'-batchmode','-force-d3d11','-quit','-job-worker-count','2','-projectPath',str(PROJECT),
                '-executeMethod','OpenRPGSourceTownsmithRenderContextV1.Export','-logFile',str(ROOT/'Unity.log')],cwd=ROOT,env=env,
                stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='source_original_door_neutral_and_linear_context_running',unity_pid=child.pid)
            save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait();assert child.returncode==0
        native=read(ROOT/'NATIVE-RESULT.v1.json');assert native['backend']=='Direct3D11' and native['source_objects_unchanged']
        assert len(native['rows'])==2 and all(r['original_material_runtime_states_unchanged'] for r in native['rows'])
        for p,h in prep['input_pins'].items():assert sha(p)==h,p
        for p,h in prep['private_before_pins'].items():assert sha(p)==h,p
        assert sha(ROOT/'door-neutral.rgba16f')==sha(PRIOR/'case-00-source_default.rgba16f')
        state.update(status='original_door_neutral_reference_reproduced_and_linear_context_captured',
            neutral_reference_byte_equal=True,source_inputs_unchanged=True,cases_captured=2,
            native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),forge_editor_verified=False)
    except Exception as exc:state.update(status='source_render_context_refusal_retained',error=repr(exc),traceback=__import__('traceback').format_exc())
    state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__':prepare() if len(sys.argv)==1 else run()
