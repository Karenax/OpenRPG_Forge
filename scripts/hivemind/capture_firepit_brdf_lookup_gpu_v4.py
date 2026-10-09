"""Retry unchanged 35-case witness after strict scene texture intake admission."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_brdf_lookup_gpu_v3.py')
source=BASE.read_text('utf-8').replace('firepit-brdf-lookup-gpu-v3','firepit-brdf-lookup-gpu-v4')
exec(compile(source,str(BASE),'exec'),globals())
