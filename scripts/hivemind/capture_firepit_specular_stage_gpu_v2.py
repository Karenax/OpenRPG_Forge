from pathlib import Path
BASE_V2=Path(__file__).with_name('capture_firepit_specular_stage_gpu_v1.py')
source=BASE_V2.read_text('utf-8').replace('firepit-specular-stage-gpu-v1','firepit-specular-stage-gpu-v2').replace('firepit-specular-stage-gpu-case-v1','firepit-specular-stage-gpu-case-v2')
exec(compile(source,str(BASE_V2),'exec'),globals())
