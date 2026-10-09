from pathlib import Path
BASE_CONTROL=Path(__file__).with_name('capture_firepit_surface_camera_native_v1.py')
source=BASE_CONTROL.read_text('utf-8').replace('firepit-surface-camera-native-v1','firepit-surface-cull-control-v1').replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-surface-cull-control-case-v1'")
exec(compile(source,str(BASE_CONTROL),'exec'),globals())
