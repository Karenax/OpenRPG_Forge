from pathlib import Path
BASE_SURFACE=Path(__file__).with_name('prepare_firepit_gpu_frame_instance_fix_v1.py')
source=BASE_SURFACE.read_text('utf-8').replace("OLD=W/'firepit-gpu-frame-matrix-case-v1'","OLD=W/'firepit-surface-native-case-v1'").replace('firepit-gpu-frame-instance-fix-case-v1','firepit-surface-instance-fix-case-v1')
exec(compile(source,str(BASE_SURFACE),'exec'),globals())
