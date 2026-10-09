"""Read corrected material-guard harness with the unchanged 35-case protocol."""
from pathlib import Path
BASE = Path(__file__).with_name('reopen_firepit_brdf_lookup_gpu_v2.py')
source=BASE.read_text('utf-8').replace('firepit-brdf-lookup-gpu-v2','firepit-brdf-lookup-gpu-v3').replace('firepit-brdf-lookup-gpu-reopen-v2','firepit-brdf-lookup-gpu-reopen-v3')
exec(compile(source,str(BASE),'exec'),globals())
