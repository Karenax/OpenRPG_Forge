from pathlib import Path
BASE_V3=Path(__file__).with_name('prepare_firepit_specular_stage_gpu_case_v2.py')
source=BASE_V3.read_text('utf-8').replace('firepit-specular-stage-gpu-case-v2','firepit-specular-stage-gpu-case-v3')
needle="exec(compile(source,str(BASE_V2),'exec'),globals())"
assert source.count(needle)==1
source=source.replace(needle,"source=source.replace('let final=','let reflected_stage=').replace('value=final;','value=reflected_stage;')\n"+needle)
exec(compile(source,str(BASE_V3),'exec'),globals())
