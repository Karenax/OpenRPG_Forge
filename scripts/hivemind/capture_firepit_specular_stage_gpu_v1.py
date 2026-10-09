from pathlib import Path
BASE_STAGE=Path(__file__).with_name('capture_firepit_specular_gpu_v1.py')
source=BASE_STAGE.read_text('utf-8').replace('firepit-specular-gpu-v1','firepit-specular-stage-gpu-v1').replace('firepit-specular-gpu-case-v1','firepit-specular-stage-gpu-case-v1').replace('source_specular_cases_expected\\\":756','source_specular_cases_expected\\\":8')
exec(compile(source,str(BASE_STAGE),'exec'),globals())
