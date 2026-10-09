from pathlib import Path
BASE_SCENE=Path(__file__).with_name('prepare_firepit_gpu_frame_instance_fix_v1.py')
source=BASE_SCENE.read_text('utf-8').replace("OLD=W/'firepit-gpu-frame-matrix-case-v1'","OLD=W/'firepit-source-ao-scene-case-v1'").replace('firepit-gpu-frame-instance-fix-case-v1','firepit-source-scene-instance-fix-case-v1')
exec(compile(source,str(BASE_SCENE),'exec'),globals())
