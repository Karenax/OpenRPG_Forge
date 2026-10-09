from pathlib import Path
BASE_AO=Path(__file__).with_name('record_firepit_specular_scene_residual_v1.py')
source=BASE_AO.read_text('utf-8').replace('firepit-specular-scene-residual-v1','firepit-source-ao-scene-residual-v1').replace('firepit-specular-scene-comparison-v2','firepit-source-ao-scene-comparison-v1').replace('firepit-specular-scene-v2','firepit-source-ao-scene-v1').replace('compare_firepit_specular_scene_v2.py','compare_firepit_source_ao_scene_v1.py')
exec(compile(source,str(BASE_AO),'exec'),globals())
