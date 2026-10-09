from pathlib import Path
BASE_AO=Path(__file__).with_name('compare_firepit_specular_scene_v1.py')
source=BASE_AO.read_text('utf-8').replace('firepit-specular-scene-v1','firepit-source-ao-scene-v1').replace('firepit-specular-scene-comparison-v1','firepit-source-ao-scene-comparison-v1').replace('firepit-specular-scene-case-v1','firepit-source-ao-scene-case-v1')
exec(compile(source,str(BASE_AO),'exec'),globals())
