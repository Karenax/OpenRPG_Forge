from pathlib import Path
BASE_SURFACE=Path(__file__).with_name('reopen_firepit_ssao_native_case_v1.py')
source=BASE_SURFACE.read_text('utf-8').replace('firepit-ssao-native-case-v1','firepit-surface-native-case-v1').replace('private_ssao_scene_original_geometry_and_authored_bindings_preserved','private_surface_scene_original_geometry_and_authored_bindings_preserved')
exec(compile(source,str(BASE_SURFACE),'exec'),globals())
