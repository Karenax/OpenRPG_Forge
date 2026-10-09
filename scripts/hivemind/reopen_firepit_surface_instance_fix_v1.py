from pathlib import Path
BASE_SURFACE=Path(__file__).with_name('reopen_firepit_surface_native_v1.py')
source=BASE_SURFACE.read_text('utf-8').replace("CAP=W/'firepit-surface-native-v1'","CAP=W/'firepit-surface-instance-fix-v1'").replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-surface-instance-fix-case-v1'").replace('firepit-surface-native-reopen-v1','firepit-surface-instance-fix-reopen-v1')
source=source.replace('source_debug_encoding_applied=True,','source_debug_encoding_applied=True,explicit_entrypoint_instance_transport=True,')
exec(compile(source,str(BASE_SURFACE),'exec'),globals())
