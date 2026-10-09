from pathlib import Path
BASE_SURFACE=Path(__file__).with_name('capture_firepit_surface_camera_native_v1.py')
source=BASE_SURFACE.read_text('utf-8').replace('firepit-surface-camera-native-v1','firepit-surface-instance-fix-v1').replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-surface-instance-fix-case-v1'")
exec(compile(source,str(BASE_SURFACE),'exec'),globals())
