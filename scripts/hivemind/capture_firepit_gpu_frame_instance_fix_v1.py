from pathlib import Path
BASE_INSTANCE=Path(__file__).with_name('capture_firepit_gpu_frame_matrix_v1.py')
source=BASE_INSTANCE.read_text('utf-8').replace('firepit-gpu-frame-matrix-v1','firepit-gpu-frame-instance-fix-v1').replace('firepit-gpu-frame-matrix-case-v1','firepit-gpu-frame-instance-fix-case-v1')
exec(compile(source,str(BASE_INSTANCE),'exec'),globals())
