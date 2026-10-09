"""Retain v1 shader refusal; fix its reserved WGSL local name in a new snapshot."""
from pathlib import Path
BASE_V2=Path(__file__).with_name('prepare_firepit_specular_stage_gpu_case_v1.py')
source=BASE_V2.read_text('utf-8').replace('firepit-specular-stage-gpu-case-v1','firepit-specular-stage-gpu-case-v2')
source=source.replace('diagnostic=', 'stage_case=').replace('diagnostic/', 'stage_case/').replace('diagnostic%', 'stage_case%')
exec(compile(source,str(BASE_V2),'exec'),globals())
