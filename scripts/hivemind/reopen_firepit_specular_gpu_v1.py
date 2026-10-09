"""Compare each complete target specular result to its prior Unity SDK value."""
from pathlib import Path
BASE_SPECULAR=Path(__file__).with_name('reopen_firepit_sky_cube_gpu_v1.py')
source=BASE_SPECULAR.read_text('utf-8').replace('firepit-sky-cube-gpu','firepit-specular-gpu')
source=source.replace("pair['sky_cube_cases_expected']==182","pair['source_specular_cases_expected']==756")
source=source.replace("case['sky_sampling_protocol']['rows']","case['specular_sdk_protocol']['rows']")
source=source.replace("r['source_gpu_rgba'][:3]","r['source_gpu']['reflected_light_f90'][:3]")
source=source.replace('range(182)','range(756)').replace('(x+512*y)%182','(x+512*y)%756')
source=source.replace('sky_cube_cases_verified=182','source_specular_cases_verified=756')
source=source.replace('actual_gpu_source_sky_182_case_sampling_verified','actual_gpu_deferred_specular_756_case_composition_verified')
source=source.replace('source_sky_gpu_sampling_witness_refused','source_deferred_specular_gpu_witness_refused')
source=source.replace('source_sky_sampling_qualified=passed','source_deferred_specular_composition_qualified=passed')
source=source.replace("source=BASE_SKY.read_text('utf-8')",
    "source=BASE_SKY.read_text('utf-8')\nsource=source.replace(\"print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))\",\"print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows','expected_source_sky_rgb']}))\")")
exec(compile(source,str(BASE_SPECULAR),'exec'),globals())
