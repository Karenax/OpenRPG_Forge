from pathlib import Path
BASE_SCENE=Path(__file__).with_name('capture_firepit_surface_camera_native_v1.py')
source=BASE_SCENE.read_text('utf-8').replace('firepit-surface-camera-native-v1','firepit-source-scene-instance-fix-v1').replace("OLD=W/'firepit-surface-native-v1'","OLD=W/'firepit-source-ao-scene-v1'").replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-source-scene-instance-fix-case-v1'")
exec(compile(source,str(BASE_SCENE),'exec'),globals())
