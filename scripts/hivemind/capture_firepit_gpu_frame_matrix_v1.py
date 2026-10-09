from pathlib import Path
BASE_FRAME=Path(__file__).with_name('capture_firepit_surface_camera_native_v1.py')
source=BASE_FRAME.read_text('utf-8').replace('firepit-surface-camera-native-v1','firepit-gpu-frame-matrix-v1').replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-gpu-frame-matrix-case-v1'")
needle="    rust=(OLD/'src/main.rs').read_text('utf-8')"
assert source.count(needle)==1
source=source.replace(needle,needle+".replace('\"actual_source_surface_debug_diagnostic\":true','\"actual_source_surface_debug_diagnostic\":false,\"actual_gpu_frame_matrix_diagnostic\":true')")
exec(compile(source,str(BASE_FRAME),'exec'),globals())
