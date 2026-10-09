from pathlib import Path
BASE_SCENE=Path(__file__).with_name('compare_firepit_source_ao_scene_v1.py')
source=BASE_SCENE.read_text('utf-8').replace('firepit-source-ao-scene-v1','firepit-source-scene-instance-fix-v1').replace('firepit-source-ao-scene-comparison-v1','firepit-source-scene-instance-fix-comparison-v1').replace('firepit-source-ao-scene-case-v1','firepit-source-scene-instance-fix-case-v1')
exec(compile(source,str(BASE_SCENE),'exec'),globals())
