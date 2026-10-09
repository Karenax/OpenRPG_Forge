"""Create a runtime-owned empty scene without replacing Unity's untitled startup scene."""
from pathlib import Path
BASE_V4=Path(__file__).with_name('queue_firepit_shadow_reference_v4.py')
source=BASE_V4.read_text('utf-8').replace('firepit-shadow-source-reference-v4','firepit-shadow-source-reference-v5').replace('SourceShadowReferenceV4','SourceShadowReferenceV5')
source=source.replace('EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive)','UnityEngine.SceneManagement.SceneManager.CreateScene("OpenRPG source shadow reference")')
needle=" text=replace(text,'EditorSceneManager.ClosePreviewScene(preview);'"
assert source.count(needle)==1
source=source.replace(needle," text=replace(text,'new Vector3(.5f,-1,.35f).normalized','new Vector3(.8f,-.4f,.6f).normalized')\n text=replace(text,'directions[view]*radius*4','directions[view]*radius*6')\n"+needle)
exec(compile(source,str(BASE_V4),'exec'),globals())
