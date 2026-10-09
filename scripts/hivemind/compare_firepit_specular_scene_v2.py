from pathlib import Path
BASE_SCENE_V2=Path(__file__).with_name('compare_firepit_specular_scene_v1.py')
source=BASE_SCENE_V2.read_text('utf-8').replace('firepit-specular-scene-v1','firepit-specular-scene-v2').replace('firepit-specular-scene-comparison-v1','firepit-specular-scene-comparison-v2').replace('firepit-specular-scene-case-v1','firepit-specular-scene-case-v2')
exec(compile(source,str(BASE_SCENE_V2),'exec'),globals())
