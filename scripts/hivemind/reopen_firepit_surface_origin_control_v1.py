from pathlib import Path
BASE_ORIGIN=Path(__file__).with_name('reopen_firepit_surface_native_v1.py')
source=BASE_ORIGIN.read_text('utf-8').replace("CAP=W/'firepit-surface-native-v1'","CAP=W/'firepit-surface-origin-control-v1'").replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-surface-instance-fix-case-v1'").replace('firepit-surface-native-reopen-v1','firepit-surface-origin-control-reopen-v1')
source=source.replace('source_debug_encoding_applied=True,','source_debug_encoding_applied=True,world_origin_invariance_control=True,source_world_semantics_unchanged=True,target_root_origin_y=0,')
exec(compile(source,str(BASE_ORIGIN),'exec'),globals())
