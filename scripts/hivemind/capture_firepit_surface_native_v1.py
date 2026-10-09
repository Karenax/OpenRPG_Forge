from pathlib import Path
BASE_SURFACE=Path(__file__).with_name('capture_firepit_ssao_native_v1.py')
source=BASE_SURFACE.read_text('utf-8').replace("OLD=W/'firepit-specular-scene-v2'","OLD=W/'firepit-source-ao-scene-v1'").replace('firepit-ssao-native-v1','firepit-surface-native-v1').replace('firepit-ssao-native-case-v1','firepit-surface-native-case-v1')
source=source.replace(".replace('openrpg-firepit-specular-scene-v2','openrpg-firepit-surface-native-v1')",".replace('openrpg-firepit-source-ao-scene-v1','openrpg-firepit-surface-native-v1')")
source=source.replace('actual_native_ssao_visibility_diagnostic','actual_source_surface_debug_diagnostic')
needle="    marker='\"shared_sky_materials\":5,';"
assert source.count(needle)==1
source=source.replace(needle,"    rust=rust.replace('\"source_ao_intensity_applied\":true','\"source_ao_intensity_applied\":false')\n"+needle)
exec(compile(source,str(BASE_SURFACE),'exec'),globals())
