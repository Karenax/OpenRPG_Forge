from pathlib import Path
BASE_FIXED8=Path(__file__).with_name('reopen_firepit_specular_gpu_v1.py')
source=BASE_FIXED8.read_text('utf-8').replace('firepit-specular-gpu','firepit-specular-fixed8-gpu')
exec(compile(source,str(BASE_FIXED8),'exec'),globals())
