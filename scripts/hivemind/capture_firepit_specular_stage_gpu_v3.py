from pathlib import Path
BASE_V3=Path(__file__).with_name('capture_firepit_specular_stage_gpu_v2.py')
source=BASE_V3.read_text('utf-8').replace('firepit-specular-stage-gpu-v2','firepit-specular-stage-gpu-v3').replace('firepit-specular-stage-gpu-case-v2','firepit-specular-stage-gpu-case-v3')
exec(compile(source,str(BASE_V3),'exec'),globals())
