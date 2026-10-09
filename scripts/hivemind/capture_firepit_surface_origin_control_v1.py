"""World-origin invariance control; no authored source pose or material changes."""
from pathlib import Path
BASE_ORIGIN=Path(__file__).with_name('capture_firepit_surface_camera_native_v1.py')
source=BASE_ORIGIN.read_text('utf-8').replace('firepit-surface-camera-native-v1','firepit-surface-origin-control-v1').replace("OLD=W/'firepit-surface-native-v1'","OLD=W/'firepit-surface-instance-fix-v1'").replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-surface-instance-fix-case-v1'")
source=source.replace(".replace('openrpg-firepit-surface-native-v1','openrpg-firepit-surface-origin-control-v1')",".replace('openrpg-firepit-surface-instance-fix-v1','openrpg-firepit-surface-origin-control-v1')")
marker="    rust=(OLD/'src/main.rs').read_text('utf-8')";assert source.count(marker)==1
source=source.replace(marker,marker+"\n    original_offset='let offset=Vec3::new(0.,1024.,0.);';assert rust.count(original_offset)==1\n    rust=rust.replace(original_offset,'let offset=Vec3::ZERO;')\n    rust=rust.replace('\"actual_source_surface_debug_diagnostic\":true','\"actual_source_surface_debug_diagnostic\":true,\"world_origin_invariance_control\":true,\"target_root_origin_y\":0')")
exec(compile(source,str(BASE_ORIGIN),'exec'),globals())
