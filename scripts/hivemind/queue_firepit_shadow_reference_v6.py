"""Replace only the blank private batch startup scene, refusing any loaded authored scene."""
from pathlib import Path
BASE_V4=Path(__file__).with_name('queue_firepit_shadow_reference_v4.py')
source=BASE_V4.read_text('utf-8').replace('firepit-shadow-source-reference-v4','firepit-shadow-source-reference-v6').replace('SourceShadowReferenceV4','SourceShadowReferenceV6').replace('NewSceneMode.Additive','NewSceneMode.Single')
needle=" text=replace(text,'EditorSceneManager.ClosePreviewScene(preview);'"
assert source.count(needle)==1
extra=r'''
 text=replace(text,'var preview=EditorSceneManager.NewScene', 'var startupLines=new System.Collections.Generic.List<string>();for(int si=0;si<UnityEngine.SceneManagement.SceneManager.sceneCount;si++){var initial=UnityEngine.SceneManagement.SceneManager.GetSceneAt(si);var roots=initial.GetRootGameObjects();if(!string.IsNullOrEmpty(initial.path)||roots.Length>2||roots.Any(o=>o.transform.childCount!=0||o.GetComponents<Component>().Any(c=>!(c is Transform||c is Camera||c is Light||c is AudioListener||c is HDAdditionalCameraData||c is HDAdditionalLightData))))throw new Exception("Refuse to replace an authored or unexpected startup scene");startupLines.Add("scene="+initial.name+" dirty="+initial.isDirty+" roots="+string.Join(",",roots.Select(o=>o.name)));}File.WriteAllLines(Path.Combine(Path.GetDirectoryName(request.output),"STARTUP-SCENE.txt"),startupLines);var preview=EditorSceneManager.NewScene')
 text=replace(text,'new Vector3(.5f,-1,.35f).normalized','new Vector3(.8f,-.4f,.6f).normalized')
 text=replace(text,'directions[view]*radius*4','directions[view]*radius*6')
'''
source=source.replace(needle,extra+needle)
source=source.replace("'EditorSceneManager.CloseScene(preview,true);'","'// The guarded private unsaved scene is discarded when this batch process exits.'")
exec(compile(source,str(BASE_V4),'exec'),globals())
