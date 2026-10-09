from pathlib import Path
BASE_AO=Path(__file__).with_name('reopen_firepit_ssao_native_case_v1.py')
source=BASE_AO.read_text('utf-8').replace('firepit-ssao-native-case-v1','firepit-source-ao-scene-case-v1').replace('private_ssao_scene_original_geometry_and_authored_bindings_preserved','private_source_ao_scene_original_geometry_and_authored_bindings_preserved')
exec(compile(source,str(BASE_AO),'exec'),globals())
