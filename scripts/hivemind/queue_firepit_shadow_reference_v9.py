"""Repeat the exact EV10 source pair with effective camera environment evidence."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_shadow_reference_v8.py')
loaded=runpy.run_path(str(BASE),run_name='source_environment_probe')
g=loaded['prepare'].__globals__
original=g['helper']
def helper():
    text=original().replace('SourceShadowReferenceV8','SourceShadowReferenceV9')
    marker=' [Serializable] public class Row'
    assert text.count(marker)==1
    fields='light,hd_light,camera,hd_camera,hd_pipeline,camera_frame_settings,fog,visual_environment,hdri_sky,physical_sky,indirect_lighting,shadow_settings,ambient_mode'
    declaration=' [Serializable] public class EnvironmentState { public string '+fields+';public Color ambient_light,ambient_sky,ambient_equator,ambient_ground;public float ambient_intensity;public float[] ambient_probe; }\n'
    text=text.replace(marker,declaration+marker)
    marker='    var prior=RenderTexture.active;Texture2D raw=null,png=null;'
    assert text.count(marker)==1
    capture=r'''
    var hc=HDCamera.GetOrCreate(camera);var stack=hc.volumeStack;
    var probe=new float[27];for(int channel=0;channel<3;channel++)for(int coefficient=0;coefficient<9;coefficient++)probe[channel*9+coefficient]=RenderSettings.ambientProbe[channel,coefficient];
    var environment=new EnvironmentState {light=EditorJsonUtility.ToJson(light,true),hd_light=EditorJsonUtility.ToJson(light.GetComponent<HDAdditionalLightData>(),true),camera=EditorJsonUtility.ToJson(camera,true),hd_camera=EditorJsonUtility.ToJson(extra,true),hd_pipeline=EditorJsonUtility.ToJson(pipeline,true),camera_frame_settings=JsonUtility.ToJson(hc.frameSettings,true),fog=EditorJsonUtility.ToJson(stack.GetComponent<Fog>(),true),visual_environment=EditorJsonUtility.ToJson(stack.GetComponent<VisualEnvironment>(),true),hdri_sky=EditorJsonUtility.ToJson(stack.GetComponent<HDRISky>(),true),physical_sky=EditorJsonUtility.ToJson(stack.GetComponent<PhysicallyBasedSky>(),true),indirect_lighting=EditorJsonUtility.ToJson(stack.GetComponent<IndirectLightingController>(),true),shadow_settings=EditorJsonUtility.ToJson(stack.GetComponent<HDShadowSettings>(),true),ambient_mode=RenderSettings.ambientMode.ToString(),ambient_light=RenderSettings.ambientLight,ambient_sky=RenderSettings.ambientSkyColor,ambient_equator=RenderSettings.ambientEquatorColor,ambient_ground=RenderSettings.ambientGroundColor,ambient_intensity=RenderSettings.ambientIntensity,ambient_probe=probe};
    var environmentPath=Path.Combine(Path.GetDirectoryName(request.output),"SOURCE-STATE-"+view+".json");if(File.Exists(environmentPath))throw new Exception("Preserve environment evidence");File.WriteAllText(environmentPath,JsonUtility.ToJson(environment,true));
'''
    return text.replace(marker,capture+'\n'+marker)
g.update(ROOT=g['W']/'firepit-shadow-source-reference-v9',HELPER=g['PROJECT']/'Assets/Editor/SourceShadowReferenceV9.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':g['prepare']() if sys.argv[-1]=='prepare' else g['run']()
