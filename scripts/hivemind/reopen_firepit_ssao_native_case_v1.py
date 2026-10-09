from pathlib import Path
BASE_SSAO=Path(__file__).with_name('reopen_firepit_specular_scene_case_v1.py')
source=BASE_SSAO.read_text('utf-8').replace('firepit-specular-scene-case-v1','firepit-ssao-native-case-v1')
source=source.replace('private_specular_scene_original_geometry_and_authored_bindings_preserved','private_ssao_scene_original_geometry_and_authored_bindings_preserved')
source=source.replace('source_specular_gpu_cases_verified=756,scene_render_qualified=False','native_ssao_settings_changed=False,scene_render_qualified=False')
exec(compile(source,str(BASE_SSAO),'exec'),globals())
