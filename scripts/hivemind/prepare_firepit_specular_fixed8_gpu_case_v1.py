"""Retest all cases using measured source interpolation, unchanged thresholds."""
from pathlib import Path
BASE_FIXED8=Path(__file__).with_name('prepare_firepit_specular_gpu_case_v1.py')
source=BASE_FIXED8.read_text('utf-8').replace('firepit-specular-gpu','firepit-specular-fixed8-gpu')
source=source.replace('extra=[sdk_path,',"extra=[W/'source-specular-lut-interpolation-v1/RESULT.v1.json',sdk_path,")
exec(compile(source,str(BASE_FIXED8),'exec'),globals())
