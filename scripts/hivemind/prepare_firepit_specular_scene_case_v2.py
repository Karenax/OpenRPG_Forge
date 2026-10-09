from pathlib import Path
BASE_SCENE_V2=Path(__file__).with_name('prepare_firepit_specular_scene_case_v1.py')
source=BASE_SCENE_V2.read_text('utf-8').replace('firepit-specular-scene-case-v1','firepit-specular-scene-case-v2').replace('vec4<f32>(p.V,0.0)','vec4<f32>(in.V,0.0)')
exec(compile(source,str(BASE_SCENE_V2),'exec'),globals())
