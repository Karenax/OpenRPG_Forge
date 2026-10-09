"""Use an unsaved Game-camera scene: HDRP sanitizes shadow maps out of Preview cameras."""
from pathlib import Path
BASE_SCRIPT=Path(__file__).with_name('queue_firepit_shadow_reference_v1.py')
source=BASE_SCRIPT.read_text('utf-8').replace('firepit-shadow-source-reference-v1','firepit-shadow-source-reference-v4').replace('SourceShadowReferenceV1','SourceShadowReferenceV4')
needle=' return text\ndef prepare():'
assert source.count(needle)==1
extra=r'''
 text=replace(text,'source.GetComponentsInChildren<Renderer>(true).Select','source.GetComponentsInChildren<Renderer>(true).Where(r=>r is MeshRenderer).Select')
 text=replace(text,'public string id,prefab_guid,unity_version,gpu,pipeline;','public string id,prefab_guid,unity_version,gpu,pipeline,camera_kind,native_light_unit;public bool private_unsaved_scene_isolated=true;')
 text=replace(text,'EditorSceneManager.NewPreviewScene()','EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive)')
 text=replace(text,'camera.enabled=false;camera.orthographic=false;','camera.enabled=false;camera.cameraType=CameraType.Game;camera.orthographic=false;')
 text=replace(text,'light.intensity=1;light.shadows=LightShadows.Hard;','light.lightUnit=LightUnit.Lux;light.intensity=10000;light.shadows=LightShadows.Hard;')
 text=replace(text,'source_object_unchanged=true,actual_directional_light_intensity','source_object_unchanged=true,preview_scene_isolated=false,camera_kind=camera.cameraType.ToString(),native_light_unit=light.lightUnit.ToString(),actual_directional_light_intensity')
 text=replace(text,'EditorSceneManager.ClosePreviewScene(preview);','EditorSceneManager.CloseScene(preview,true);')
 return text
def prepare():'''
source=source.replace(needle,extra)
source=source.replace("str(BASE):sha(BASE),","str(BASE):sha(BASE),str(BASE_SCRIPT):sha(BASE_SCRIPT),")
exec(compile(source,str(BASE_SCRIPT),'exec'),globals())
